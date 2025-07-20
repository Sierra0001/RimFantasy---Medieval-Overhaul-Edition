using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimFantasy
{
	public class FireExtension : DefModExtension
	{
		public float spreadRateMultiplier = 1f;
	}
    public class CustomFire : Fire, ISizeReporter
	{
		public override string Label
		{
			get
			{
				if (parent != null)
				{
					return "FireOn".Translate(parent.LabelCap, parent);
				}
				return def.label;
			}
		}

		public override string InspectStringAddon => "Burning".Translate() + " (" + "FireSizeLower".Translate((fireSize * 100f).ToString("F0")) + ")";

		private float SpreadIntervalOverride
		{
			get
			{
				float num = base.SpreadInterval;
				var extension = this.def.GetModExtension<FireExtension>();
				if (extension != null)
				{
					num *= extension.spreadRateMultiplier;
				}
				return num;
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			ticksSinceSpread = (int)(SpreadIntervalOverride * Rand.Value);
			this.graphicInt = def.graphicData.GraphicColoredFor(this);
		}

		public override void Tick()
		{
			ticksSinceSpawn++;
			if (lastFireCountUpdateTick != Find.TickManager.TicksGame)
			{
				fireCount = base.Map.listerThings.ThingsOfDef(def).Count;
				lastFireCountUpdateTick = Find.TickManager.TicksGame;
			}
			if (sustainer != null)
			{
				sustainer.Maintain();
			}
			else if (!base.Position.Fogged(base.Map))
			{
				SoundInfo info = SoundInfo.InMap(new TargetInfo(base.Position, base.Map), MaintenanceType.PerTick);
				sustainer = SustainerAggregatorUtility.AggregateOrSpawnSustainerFor(this, SoundDefOf.FireBurning, info);
			}
			ticksUntilSmoke--;
			if (ticksUntilSmoke <= 0)
			{
				SpawnSmokeParticlesOverride();
			}
			if (fireCount < 15 && fireSize > 0.7f && Rand.Value < fireSize * 0.01f)
			{
				FleckMaker.ThrowMicroSparks(DrawPos, base.Map);
			}
			if (fireSize > 1f)
			{
				ticksSinceSpread++;
				if ((float)ticksSinceSpread >= SpreadIntervalOverride)
				{
					TrySpreadOverride();
					ticksSinceSpread = 0;
				}
			}
			if (this.IsHashIntervalTick(150))
			{
				DoComplexCalcsOverride();
			}
			if (ticksSinceSpawn >= 7500)
			{
				TryBurnFloor();
			}
		}

        private void SpawnSmokeParticlesOverride()
        {
            if (fireCount < 15)
            {
                FleckMaker.ThrowSmoke(DrawPos, base.Map, fireSize);
            }
            if (fireSize > 0.5f && parent == null)
            {
                var fireGlow = DefDatabase<FleckDef>.GetNamedSilentFail(this.def.defName + "Glow") ?? FleckDefOf.FireGlow;
                ThrowFireGlow(fireGlow, base.Position.ToVector3Shifted(), base.Map, fireSize);
            }
            float num = fireSize / 2f;
            if (num > 1f)
            {
                num = 1f;
            }
            num = 1f - num;
            ticksUntilSmoke = SmokeIntervalRange.Lerped(num) + (int)(10f * Rand.Value);
        }
        public static void ThrowFireGlow(FleckDef fireGlow, Vector3 c, Map map, float size)
		{
			Vector3 vector = c;
			if (vector.ShouldSpawnMotesAt(map))
			{
				vector += size * new Vector3(Rand.Value - 0.5f, 0f, Rand.Value - 0.5f);
				if (vector.InBounds(map))
				{
					FleckCreationData dataStatic = FleckMaker.GetDataStatic(vector, map, fireGlow, Rand.Range(4f, 6f) * size);
					dataStatic.rotationRate = Rand.Range(-3f, 3f);
					dataStatic.velocityAngle = Rand.Range(0, 360);
					dataStatic.velocitySpeed = 0.12f;
					map.flecks.CreateFleck(dataStatic);
				}
			}
		}

		private void DoComplexCalcsOverride()
		{
			bool flag = false;
			flammableList.Clear();
			flammabilityMax = 0f;
			if (!base.Position.GetTerrain(base.Map).extinguishesFire)
			{
				if (parent == null)
				{
					if (base.Position.TerrainFlammableNow(base.Map))
					{
						flammabilityMax = base.Position.GetTerrain(base.Map).GetStatValueAbstract(StatDefOf.Flammability);
					}
					List<Thing> list = base.Map.thingGrid.ThingsListAt(base.Position);
					for (int i = 0; i < list.Count; i++)
					{
						Thing thing = list[i];
						if (thing is Building_Door)
						{
							flag = true;
						}
						float statValue = thing.GetStatValue(StatDefOf.Flammability);
						if (!(statValue < 0.01f))
						{
							flammableList.Add(list[i]);
							if (statValue > flammabilityMax)
							{
								flammabilityMax = statValue;
							}
							if (parent == null && fireSize > 0.4f && list[i].def.category == ThingCategory.Pawn 
								&& Rand.Chance(FireUtility.ChanceToAttachFireCumulative(list[i], 150f)))
							{
								TryAttachFire(list[i], this.def, fireSize * 0.2f);
							}
						}
					}
				}
				else
				{
					flammableList.Add(parent);
					flammabilityMax = parent.GetStatValue(StatDefOf.Flammability);
				}
			}
			if (flammabilityMax < 0.01f)
			{
				Destroy();
				return;
			}
			Thing thing2 = ((parent != null) ? parent : ((flammableList.Count <= 0) ? null : flammableList.RandomElement()));
			if (thing2 != null && (!(fireSize < 0.4f) || thing2 == parent || thing2.def.category != ThingCategory.Pawn))
			{
				DoFireDamageOverride(thing2);
			}
			if (base.Spawned)
			{
				float num = fireSize * 160f;
				if (flag)
				{
					num *= 0.15f;
				}
				GenTemperature.PushHeat(base.Position, base.Map, num);
				if (Rand.Value < 0.4f)
				{
					float radius = fireSize * 3f;
                    WeatherBuildupUtility.AddSnowRadial(base.Position, base.Map, radius, 0f - fireSize * 0.1f);
				}
				fireSize += 0.00055f * flammabilityMax * 150f;
				if (fireSize > 1.75f)
				{
					fireSize = 1.75f;
				}
				if (base.Map.weatherManager.RainRate > 0.01f && VulnerableToRain() && Rand.Value < 6f)
				{
					TakeDamage(new DamageInfo(DamageDefOf.Extinguish, 10f));
				}
			}
		}

		public static void TryAttachFire(Thing t, ThingDef fireDef, float fireSize)
		{
			if (t.CanEverAttachFire() && !t.HasAttachment(fireDef))
			{
				Fire obj = (Fire)ThingMaker.MakeThing(fireDef);
				obj.fireSize = fireSize;
				obj.AttachTo(t);
				GenSpawn.Spawn(obj, t.Position, t.Map, Rot4.North);
				Pawn pawn = t as Pawn;
				if (pawn != null)
				{
					pawn.jobs.StopAll();
					pawn.records.Increment(RecordDefOf.TimesOnFire);
				}
			}
		}
        
		private void DoFireDamageOverride(Thing targ)
		{
			int num = GenMath.RoundRandom(Mathf.Clamp(0.0125f + 0.0036f * fireSize, 0.0125f, 0.05f) * 150f);
			if (num < 1)
			{
				num = 1;
			}
			Pawn pawn = targ as Pawn;
			if (pawn != null)
			{
				BattleLogEntry_DamageTaken battleLogEntry_DamageTaken = new BattleLogEntry_DamageTaken(pawn, RulePackDefOf.DamageEvent_Fire);
				Find.BattleLog.Add(battleLogEntry_DamageTaken);
				DamageInfo dinfo = new DamageInfo(RF_DefOf.RF_FlameCustom, num, 0f, -1f, this);
				dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
				targ.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_DamageTaken);
				if (pawn.apparel != null && pawn.apparel.WornApparel.TryRandomElement(out var result))
				{
					result.TakeDamage(new DamageInfo(RF_DefOf.RF_FlameCustom, num, 0f, -1f, this));
				}
			}
			else
			{
				targ.TakeDamage(new DamageInfo(RF_DefOf.RF_FlameCustom, num, 0f, -1f, this));
			}
		}

		private void TrySpreadOverride()
		{
			IntVec3 position = base.Position;
			bool flag;
			if (Rand.Chance(0.8f))
			{
				position = base.Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(1, 8)];
				flag = true;
			}
			else
			{
				position = base.Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(10, 20)];
				flag = false;
			}
			if (!position.InBounds(base.Map) || !Rand.Chance(FireUtility.ChanceToStartFireIn(position, base.Map)))
			{
				return;
			}
			if (!flag)
			{
				CellRect startRect = CellRect.SingleCell(base.Position);
				CellRect endRect = CellRect.SingleCell(position);
				if (GenSight.LineOfSight(base.Position, position, base.Map, startRect, endRect))
				{
					((SparkCustom)GenSpawn.Spawn(RF_DefOf.RF_SparkCustom, base.Position, base.Map)).Launch(this, position, position, ProjectileHitFlags.All);
				}
			}
			else
			{
				TryStartFireIn(this.def, position, base.Map, 0.1f);
			}
		}

		public static bool TryStartFireIn(ThingDef fireDef, IntVec3 c, Map map, float fireSize)
		{
			if (FireUtility.ChanceToStartFireIn(c, map) <= 0f)
			{
				return false;
			}
			Fire obj = (Fire)ThingMaker.MakeThing(fireDef);
			obj.fireSize = fireSize;
			GenSpawn.Spawn(obj, c, map, Rot4.North);
			return true;
		}
	}

	public class SparkCustom : Projectile
	{
		public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            base.Impact(hitThing, blockedByShield);
            CustomFire.TryStartFireIn(this.launcher.def, base.Position, map, 0.1f);
        }
	}
}
