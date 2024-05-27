using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimFantasy
{
	public class CompProperties_ExplosiveStuffed : CompProperties_Explosive
	{
		public string postExplosionSpawnBaseDefName;
		public string explosiveDamageTypeBaseDefName;
		public CompProperties_ExplosiveStuffed()
		{
			this.compClass = typeof(CompExplosiveStuffed);
		}
	}
	public class CompExplosiveStuffed : CompExplosive
	{
		public bool wickStarted;

		protected int wickTicksLeft;

		private Thing instigator;

		private int countdownTicksLeft = -1;

		public bool destroyedThroughDetonation;

		private List<Thing> thingsIgnoredByExplosion;

		public float? customExplosiveRadius;

		protected Sustainer wickSoundSustainer;

		private OverlayHandle? overlayBurningWick;

		public CompProperties_ExplosiveStuffed Props => (CompProperties_ExplosiveStuffed)props;

		protected int StartWickThreshold => Mathf.RoundToInt(Props.startWickHitPointsPercent * (float)parent.MaxHitPoints);

		private bool CanEverExplodeFromDamage
		{
			get
			{
				if (Props.chanceNeverExplodeFromDamage < 1E-05f)
				{
					return true;
				}
				Rand.PushState();
				Rand.Seed = parent.thingIDNumber.GetHashCode();
				bool result = Rand.Value > Props.chanceNeverExplodeFromDamage;
				Rand.PopState();
				return result;
			}
		}

		public void AddThingsIgnoredByExplosion(List<Thing> things)
		{
			if (thingsIgnoredByExplosion == null)
			{
				thingsIgnoredByExplosion = new List<Thing>();
			}
			thingsIgnoredByExplosion.AddRange(things);
		}

		public override void PostExposeData()
		{
			Scribe_References.Look(ref instigator, "instigator");
			Scribe_Collections.Look(ref thingsIgnoredByExplosion, "thingsIgnoredByExplosion", LookMode.Reference);
			Scribe_Values.Look(ref wickStarted, "wickStarted", defaultValue: false);
			Scribe_Values.Look(ref wickTicksLeft, "wickTicksLeft", 0);
			Scribe_Values.Look(ref destroyedThroughDetonation, "destroyedThroughDetonation", defaultValue: false);
			Scribe_Values.Look(ref countdownTicksLeft, "countdownTicksLeft", 0);
			Scribe_Values.Look(ref customExplosiveRadius, "explosiveRadius");
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (Props.countdownTicks.HasValue)
			{
				countdownTicksLeft = Props.countdownTicks.Value.RandomInRange;
			}
			UpdateOverlays();
		}

		public override void CompTick()
		{
			if (countdownTicksLeft > 0)
			{
				countdownTicksLeft--;
				if (countdownTicksLeft == 0)
				{
					StartWick();
					countdownTicksLeft = -1;
				}
			}
			if (!wickStarted)
			{
				return;
			}
			if (wickSoundSustainer == null)
			{
				StartWickSustainer();
			}
			else
			{
				wickSoundSustainer.Maintain();
			}
			if (Props.wickMessages != null)
			{
				foreach (WickMessage wickMessage in Props.wickMessages)
				{
					if (wickMessage.ticksLeft == wickTicksLeft && wickMessage.wickMessagekey != null)
					{
						Messages.Message(wickMessage.wickMessagekey.Translate(parent, wickTicksLeft.ToStringSecondsFromTicks()), parent, wickMessage.messageType ?? MessageTypeDefOf.NeutralEvent, historical: false);
					}
				}
			}
			wickTicksLeft--;
			if (wickTicksLeft <= 0)
			{
				Detonate(parent.MapHeld);
			}
		}

		private void StartWickSustainer()
		{
			SoundDefOf.MetalHitImportant.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
			SoundInfo info = SoundInfo.InMap(parent, MaintenanceType.PerTick);
			wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
		}

		private void EndWickSustainer()
		{
			if (wickSoundSustainer != null)
			{
				wickSoundSustainer.End();
				wickSoundSustainer = null;
			}
		}

		private void UpdateOverlays()
		{
			if (parent.Spawned)
			{
				parent.Map.overlayDrawer.Disable(parent, ref overlayBurningWick);
				if (wickStarted)
				{
					overlayBurningWick = parent.Map.overlayDrawer.Enable(parent, OverlayTypes.BurningWick);
				}
			}
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			if (mode == DestroyMode.KillFinalize && Props.explodeOnKilled)
			{
				Detonate(previousMap, ignoreUnspawned: true);
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (!CanEverExplodeFromDamage)
			{
				return;
			}
			if (dinfo.Def.ExternalViolenceFor(parent) && dinfo.Amount >= (float)parent.HitPoints && CanExplodeFromDamageType(dinfo.Def))
			{
				if (parent.MapHeld != null)
				{
					instigator = dinfo.Instigator;
					Detonate(parent.MapHeld);
					if (parent.Destroyed)
					{
						absorbed = true;
					}
				}
			}
			else if (!wickStarted && Props.startWickOnDamageTaken != null && Props.startWickOnDamageTaken.Contains(dinfo.Def))
			{
				StartWick(dinfo.Instigator);
			}
		}

		public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			if (CanEverExplodeFromDamage && CanExplodeFromDamageType(dinfo.Def) && !parent.Destroyed)
			{
				if (wickStarted && dinfo.Def == DamageDefOf.Stun)
				{
					StopWick();
				}
				else if (!wickStarted && parent.HitPoints <= StartWickThreshold && dinfo.Def.ExternalViolenceFor(parent))
				{
					StartWick(dinfo.Instigator);
				}
			}
		}

		public void StartWick(Thing instigator = null)
		{
			if (!wickStarted && !(ExplosiveRadius() <= 0f))
			{
				this.instigator = instigator;
				wickStarted = true;
				wickTicksLeft = Props.wickTicks.RandomInRange;
				StartWickSustainer();
				GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(parent, Props.explosiveDamageType, null, instigator);
				UpdateOverlays();
			}
		}

		public void StopWick()
		{
			wickStarted = false;
			instigator = null;
			UpdateOverlays();
		}

		public float ExplosiveRadius()
		{
			CompProperties_Explosive compProperties_Explosive = Props;
			float num = customExplosiveRadius ?? Props.explosiveRadius;
			if (parent.stackCount > 1 && compProperties_Explosive.explosiveExpandPerStackcount > 0f)
			{
				num += Mathf.Sqrt((float)(parent.stackCount - 1) * compProperties_Explosive.explosiveExpandPerStackcount);
			}
			if (compProperties_Explosive.explosiveExpandPerFuel > 0f && parent.GetComp<CompRefuelable>() != null)
			{
				num += Mathf.Sqrt(parent.GetComp<CompRefuelable>().Fuel * compProperties_Explosive.explosiveExpandPerFuel);
			}
			return num;
		}

		protected void Detonate(Map map, bool ignoreUnspawned = false)
		{
			if (!ignoreUnspawned && !parent.SpawnedOrAnyParentSpawned)
			{
				return;
			}
			CompProperties_ExplosiveStuffed explProps = Props;
			float num = ExplosiveRadius();
			if (explProps.explosiveExpandPerFuel > 0f && parent.GetComp<CompRefuelable>() != null)
			{
				parent.GetComp<CompRefuelable>().ConsumeFuel(parent.GetComp<CompRefuelable>().Fuel);
			}
			if (explProps.destroyThingOnExplosionSize <= num && !parent.Destroyed)
			{
				destroyedThroughDetonation = true;
				parent.Kill();
			}
			EndWickSustainer();
			wickStarted = false;
			if (map == null)
			{
				Log.Warning("Tried to detonate CompExplosive in a null map.");
				return;
			}
			if (explProps.explosionEffect != null)
			{
				Effecter effecter = explProps.explosionEffect.Spawn();
				effecter.Trigger(new TargetInfo(parent.PositionHeld, map), new TargetInfo(parent.PositionHeld, map));
				effecter.Cleanup();
			}
			var stuff = this.parent.Stuff;
			var damType = stuff != null && !explProps.explosiveDamageTypeBaseDefName.NullOrEmpty() ? 
				DefDatabase<DamageDef>.GetNamed(explProps.explosiveDamageTypeBaseDefName + stuff.defName)
				: explProps.explosiveDamageType;
			var postExplosionSpawnThingDef = stuff != null && !explProps.postExplosionSpawnBaseDefName.NullOrEmpty() ? 
				ThingDef.Named(explProps.postExplosionSpawnBaseDefName + stuff.defName) : explProps.postExplosionSpawnThingDef;

			GenExplosion.DoExplosion(instigator: (instigator == null || (instigator.HostileTo(parent.Faction) && parent.Faction != Faction.OfPlayer)) ? 
				parent : instigator, center: parent.PositionHeld, map: map, radius: num, damType: damType, 
				damAmount: explProps.damageAmountBase, armorPenetration: explProps.armorPenetrationBase, 
				explosionSound: explProps.explosionSound, weapon: null, projectile: null, intendedTarget: null, postExplosionSpawnThingDef: postExplosionSpawnThingDef
				, postExplosionSpawnChance: explProps.postExplosionSpawnChance, 
				postExplosionSpawnThingCount: explProps.postExplosionSpawnThingCount, 
				applyDamageToExplosionCellsNeighbors: explProps.applyDamageToExplosionCellsNeighbors, 
				preExplosionSpawnThingDef: explProps.preExplosionSpawnThingDef, 
				preExplosionSpawnChance: explProps.preExplosionSpawnChance, preExplosionSpawnThingCount: explProps.preExplosionSpawnThingCount, 
				chanceToStartFire: explProps.chanceToStartFire, damageFalloff: explProps.damageFalloff, direction: null, 
				ignoredThings: thingsIgnoredByExplosion);
		}

		private bool CanExplodeFromDamageType(DamageDef damage)
		{
			if (Props.requiredDamageTypeToExplode != null)
			{
				return Props.requiredDamageTypeToExplode == damage;
			}
			return true;
		}

		public override string CompInspectStringExtra()
		{
			string text = "";
			if (countdownTicksLeft != -1)
			{
				text += "DetonationCountdown".Translate(countdownTicksLeft.TicksToDays().ToString("0.0"));
			}
			if (Props.extraInspectStringKey != null)
			{
				text += ((text != "") ? "\n" : "") + Props.extraInspectStringKey.Translate();
			}
			return text;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (countdownTicksLeft > 0)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "DEV: Trigger countdown";
				command_Action.action = delegate
				{
					countdownTicksLeft = 1;
				};
				yield return command_Action;
			}
		}
	}
}