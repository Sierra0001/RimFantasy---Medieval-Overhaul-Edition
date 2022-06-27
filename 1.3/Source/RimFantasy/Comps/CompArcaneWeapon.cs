using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using System.Reflection;
using Verse;
using UnityEngine;
using HarmonyLib;
using Verse.AI.Group;
using Verse.Sound;

namespace RimFantasy
{
	public class RimFantasyExtension : DefModExtension
    {
		public string stuffKey;
    }
	public class CompProperties_ArcaneWeapon : CompProperties_Biocodable
	{
		public List<WeaponTraitDef> weaponTraitsPool;
		public int minWeaponTraits;
		public int maxWeaponTraits;
		public CompProperties_ArcaneWeapon()
		{
			compClass = typeof(CompArcaneWeapon);
		}
	}
	public class CompArcaneWeapon : CompBladelinkWeapon
	{
		public new CompProperties_ArcaneWeapon Props => base.props as CompProperties_ArcaneWeapon;
		public HashSet<Projectile> releasedProjectiles = new HashSet<Projectile>();
		private static HashSet<CompArcaneWeapon> compArcaneWeapons = new HashSet<CompArcaneWeapon>();
		public static CompArcaneWeapon GetLinkedCompFor(Projectile projectle)
        {
			foreach (var comp in compArcaneWeapons)
            {
				if (comp.releasedProjectiles.Contains(projectle))
                {
					return comp;
                }
            }
			return null;
        }

        public override bool Biocodable => Props.biocodeOnEquip;
        public Pawn Wearer
        {
            get
            {
				if (this.parent.ParentHolder is Pawn_EquipmentTracker equipmentTracker)
                {
					return equipmentTracker.pawn;
                }
				return null;
            }
        }
		public CompArcaneWeapon()
        {
			compArcaneWeapons.Add(this);
			if (!RimFantasyManager.Instance.compsToTickNormal.Contains(this))
            {
				RimFantasyManager.Instance.compsToTickNormal.Add(this);
            }
		}
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
			if (!RimFantasyManager.Instance.compsToTickNormal.Contains(this))
			{
				RimFantasyManager.Instance.compsToTickNormal.Add(this);
			}
		}
		public override void PostPostMake()
		{
			InitializeTraitsCustom();
			if (!RimFantasyManager.Instance.compsToTickNormal.Contains(this))
			{
				RimFantasyManager.Instance.compsToTickNormal.Add(this);
			}
		}
		public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
			if (RimFantasyManager.Instance.compsToTickNormal.Contains(this))
			{
				RimFantasyManager.Instance.compsToTickNormal.Remove(this);
			}
		}
		public void InitializeTraitsCustom()
        {
			List<WeaponTraitDef> traits = Traverse.Create(this).Field("traits").GetValue<List<WeaponTraitDef>>();
            IEnumerable<WeaponTraitDef> allDefs = Props.weaponTraitsPool;
			if (traits == null)
			{
				traits = new List<WeaponTraitDef>();
			}
			Rand.PushState(parent.HashOffset());
			int randomInRange = new IntRange(Props.minWeaponTraits, Props.maxWeaponTraits).RandomInRange;
			for (int i = 0; i < randomInRange; i++)
			{
				IEnumerable<WeaponTraitDef> source = allDefs.Where((WeaponTraitDef x) => CanAddTrait(x, traits));
				if (source.Any())
				{
					var trait = source.RandomElementByWeight((WeaponTraitDef x) => x.commonality);
					traits.Add(trait);
					if (trait is ArcaneWeaponTraitDef arcaneWeaponTraitDef && arcaneWeaponTraitDef.isShield)
                    {
						this.shieldTraitDef = arcaneWeaponTraitDef;
					}
				}
			}
			Rand.PopState();
		}
		private bool CanAddTrait(WeaponTraitDef trait, List<WeaponTraitDef> traits)
		{
			if (!traits.NullOrEmpty())
			{
				for (int i = 0; i < traits.Count; i++)
				{
					if (trait.Overlaps(traits[i]))
					{
						return false;
					}
				}
			}
			return true;
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
			releasedProjectiles.RemoveWhere(x => x is null || x.Destroyed);
			Scribe_Collections.Look(ref releasedProjectiles, "releasedProjectiles", LookMode.Reference);
			Scribe_Values.Look(ref energy, "energy", 0f);
			Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
			Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick", 0);
			Scribe_Defs.Look(ref shieldTraitDef, "shieldTraitDef");
			if (!RimFantasyManager.Instance.compsToTickNormal.Contains(this))
			{
				RimFantasyManager.Instance.compsToTickNormal.Add(this);
			}
		}

		private float energy;

		private int ticksToReset = -1;

		private int lastKeepDisplayTick = -9999;

		private Vector3 impactAngleVect;

		private int lastAbsorbDamageTick = -9999;
		private int StartingTicksToReset => shieldTraitDef.ignoreRechargeDelay ? 0 : 3200;

		private float EnergyOnReset = 0.2f;

		private float EnergyLossPerDamage = 0.033f;

		private int KeepDisplayingTicks = 1000;

		public ArcaneWeaponTraitDef shieldTraitDef;

		private Material shieldMat;

		private Material ShieldMat
        {
            get
            {
				if (shieldMat is null)
                {
					var texPath = shieldTraitDef.shieldTexPath;
					if (shieldTraitDef.shieldTexStuffPostfix)
                    {
						string stuffKey = "";
						if (this.parent.Stuff != null)
                        {
							stuffKey = this.parent.Stuff.defName;
						}
                        else
                        {
							var extension = this.parent.def.GetModExtension<RimFantasyExtension>();
							stuffKey = extension.stuffKey;
						}
						texPath += "_" + stuffKey;
					}
					shieldMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent);
				}
				return shieldMat;
			}
		}
		private float EnergyMax => shieldTraitDef.shieldEnergyMax;
		private float EnergyGainPerTick => shieldTraitDef.shieldRechargeRate / 60f;
		public float Energy => energy;
		public ShieldState ShieldState
		{
			get
			{
				if (ticksToReset > 0)
				{
					return ShieldState.Resetting;
				}
				return ShieldState.Active;
			}
		}

		private bool ShouldDisplay
		{
			get
			{
				if (this.shieldTraitDef is null)
                {
					return false;
                }
				Pawn wearer = this.Wearer;
				if (!wearer.Spawned || wearer.Dead || wearer.Downed)
				{
					return false;
				}
				if (wearer.InAggroMentalState)
				{
					return true;
				}
				if (wearer.Drafted)
				{
					return true;
				}
				if (wearer.Faction.HostileTo(Faction.OfPlayer) && !wearer.IsPrisoner)
				{
					return true;
				}
				if (Find.TickManager.TicksGame < lastKeepDisplayTick + KeepDisplayingTicks)
				{
					return true;
				}
				return false;
			}
		}
        public override void CompTick()
        {
			base.CompTick();
			if (this.shieldTraitDef is null)
			{
				return;
			}
			if (this.Wearer == null)
			{
				energy = 0f;
			}
			else if (ShieldState == ShieldState.Resetting)
			{
				ticksToReset--;
				if (ticksToReset <= 0)
				{
					Reset();
				}
			}
			else if (ShieldState == ShieldState.Active)
			{
				Pawn wearer = this.Wearer;
				if (!wearer.Spawned || wearer.Dead || wearer.Downed)
				{
					return;
				}
				if (!shieldTraitDef.shieldCombatRecovery)
                {
					if (wearer.InAggroMentalState)
					{
						return;
					}
					if (wearer.Drafted)
					{
						return;
					}
				}
				energy += EnergyGainPerTick;
				if (energy > EnergyMax)
				{
					energy = EnergyMax;
				}
			}
		}

		public bool CheckPreAbsorbDamage(DamageInfo dinfo)
		{
			foreach (var def in this.TraitsListForReading)
			{
				if (def is ArcaneWeaponTraitDef arcaneTraitDef && !arcaneTraitDef.isShield)
				{
					if ((dinfo.Def.isRanged || dinfo.Def.isExplosive) && arcaneTraitDef.deflectRangeChance.HasValue && Rand.Chance(arcaneTraitDef.deflectRangeChance.Value))
					{
						if (arcaneTraitDef.fleckDefOnDeflect != null)
						{
							FleckMaker.Static(Wearer.Position, Wearer.Map, arcaneTraitDef.fleckDefOnDeflect, arcaneTraitDef.fleckDefOnDeflectScale);
						}
						return true;
					}
					if ((dinfo.Weapon == null || dinfo.Weapon.race != null || dinfo.Weapon.IsMeleeWeapon)
							&& arcaneTraitDef.deflectMeleeChance.HasValue && Rand.Chance(arcaneTraitDef.deflectMeleeChance.Value))
					{
						if (arcaneTraitDef.fleckDefOnDeflect != null)
						{
							FleckMaker.Static(Wearer.Position, Wearer.Map, arcaneTraitDef.fleckDefOnDeflect, arcaneTraitDef.fleckDefOnDeflectScale);
						}
						return true;
					}
				}
			}
			if (this.shieldTraitDef != null)
			{
				if (ShieldState != 0)
				{
					return false;
				}
				if (dinfo.Def == DamageDefOf.EMP)
				{
					energy = 0f;
					Break();
					return false;
				}
				if (((dinfo.Def.isRanged || dinfo.Def.isExplosive) && shieldTraitDef.deflectRangeChance.HasValue && Rand.Chance(shieldTraitDef.deflectRangeChance.Value)) ||
						(dinfo.Weapon == null || dinfo.Weapon.race != null || dinfo.Weapon.IsMeleeWeapon) && shieldTraitDef.deflectMeleeChance.HasValue && Rand.Chance(shieldTraitDef.deflectMeleeChance.Value))
				{
					energy -= dinfo.Amount * EnergyLossPerDamage;
					if (energy < 0f)
					{
						Break();
					}
					else
					{
						AbsorbedDamage(dinfo);
					}
					if (shieldTraitDef.fleckDefOnDeflect != null)
                    {
						FleckMaker.Static(Wearer.Position, Wearer.Map, shieldTraitDef.fleckDefOnDeflect, shieldTraitDef.fleckDefOnDeflectScale);
                    }
					return true;
				}
			}
			return false;
		}


		public void KeepDisplaying()
		{
			lastKeepDisplayTick = Find.TickManager.TicksGame;
		}

		private void AbsorbedDamage(DamageInfo dinfo)
		{
			SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(this.Wearer.Position, this.Wearer.Map));
			impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
			Vector3 loc = this.Wearer.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
			float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
			FleckMaker.Static(loc, this.Wearer.Map, FleckDefOf.ExplosionFlash, num);
			int num2 = (int)num;
			for (int i = 0; i < num2; i++)
			{
				FleckMaker.ThrowDustPuff(loc, this.Wearer.Map, Rand.Range(0.8f, 1.2f));
			}
			lastAbsorbDamageTick = Find.TickManager.TicksGame;
			KeepDisplaying();
		}

		private void Break()
		{
			SoundDefOf.EnergyShield_Broken.PlayOneShot(new TargetInfo(this.Wearer.Position, this.Wearer.Map));
			FleckMaker.Static(this.Wearer.TrueCenter(), this.Wearer.Map, FleckDefOf.ExplosionFlash, 12f);
			for (int i = 0; i < 6; i++)
			{
				FleckMaker.ThrowDustPuff(this.Wearer.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), this.Wearer.Map, Rand.Range(0.8f, 1.2f));
			}
			energy = 0f;
			ticksToReset = StartingTicksToReset;
		}

		private void Reset()
		{
			if (this.Wearer.Spawned)
			{
				SoundDefOf.EnergyShield_Reset.PlayOneShot(new TargetInfo(this.Wearer.Position, this.Wearer.Map));
				FleckMaker.ThrowLightningGlow(this.Wearer.TrueCenter(), this.Wearer.Map, 3f);
			}
			ticksToReset = -1;
			energy = EnergyOnReset;
		}

        public void DrawWornExtras()
		{
			if (ShieldState == ShieldState.Active && ShouldDisplay)
			{
				float num = Mathf.Lerp(1.2f, 1.55f, energy);
				Vector3 drawPos = this.Wearer.Drawer.DrawPos;
				drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
				int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
				if (num2 < 8)
				{
					float num3 = (float)(8 - num2) / 8f * 0.05f;
					drawPos += impactAngleVect * num3;
					num -= num3;
				}
				float angle = Rand.Range(0, 360);
				Vector3 s = new Vector3(num, 1f, num);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
				Graphics.DrawMesh(MeshPool.plane10, matrix, ShieldMat, 0);
			}
		}
	}

	[StaticConstructorOnStartup]
	public class Gizmo_EnergyShieldStatus : Gizmo
	{
		public CompArcaneWeapon shield;

		private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));

		private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

		public Gizmo_EnergyShieldStatus()
		{
			order = -100f;
		}

		public override float GetWidth(float maxWidth)
		{
			return 140f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(6f);
			Widgets.DrawWindowBackground(rect);
			Rect rect3 = rect2;
			rect3.height = rect.height / 2f;
			Text.Font = GameFont.Tiny;
			Widgets.Label(rect3, shield.parent.LabelCap);
			Rect rect4 = rect2;
			rect4.yMin = rect2.y + rect2.height / 2f;
			float fillPercent = shield.Energy / Mathf.Max(1f, shield.shieldTraitDef.shieldEnergyMax);
			Widgets.FillableBar(rect4, fillPercent, FullShieldBarTex, EmptyShieldBarTex, doBorder: false);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect4, (shield.Energy * 100f).ToString("F0") + " / " + (shield.shieldTraitDef.shieldEnergyMax * 100f).ToString("F0"));
			Text.Anchor = TextAnchor.UpperLeft;
			return new GizmoResult(GizmoState.Clear);
		}
	}
}
