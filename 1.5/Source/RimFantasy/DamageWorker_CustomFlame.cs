using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimFantasy
{
    public class DamageWorker_CustomFlame : DamageWorker_AddInjury
	{
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			Pawn pawn = victim as Pawn;
			if (pawn != null && pawn.Faction == Faction.OfPlayer)
			{
				Find.TickManager.slower.SignalForceNormalSpeedShort();
			}
			Map map = victim.Map;
			DamageResult damageResult = base.Apply(dinfo, victim);
			if (!damageResult.deflected && !dinfo.InstantPermanentInjury && Rand.Chance(FireUtility.ChanceToAttachFireFromEvent(victim)))
			{
				var attachComp = victim.TryGetComp<CompAttachBase>();
				var fire = attachComp.attachments.OfType<Fire>().First();
				CustomFire.TryAttachFire(victim, fire.def, Rand.Range(0.15f, 0.25f));
			}
			if (victim.Destroyed && map != null && pawn == null)
			{
				foreach (IntVec3 item in victim.OccupiedRect())
				{
					FilthMaker.TryMakeFilth(item, map, ThingDefOf.Filth_Ash);
				}
				Plant plant = victim as Plant;
				if (plant != null && plant.LifeStage != 0 && victim.def.plant.burnedThingDef != null)
				{
					((DeadPlant)GenSpawn.Spawn(victim.def.plant.burnedThingDef, victim.Position, map)).Growth = plant.Growth;
				}
			}
			return damageResult;
		}

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
			if (def == RF_DefOf.RF_FlameCustom && Rand.Chance(FireUtility.ChanceToStartFireIn(c, explosion.Map)))
			{
				CustomFire.TryStartFireIn(explosion.instigator.def, c, explosion.Map, Rand.Range(0.2f, 0.6f));
			}
		}
	}
}
