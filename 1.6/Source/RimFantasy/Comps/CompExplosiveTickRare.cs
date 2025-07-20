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
	public class CompProperties_ExplosiveTickRare : CompProperties_Explosive
	{
		public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
		{
			yield break;
		}

		public CompProperties_ExplosiveTickRare()
		{
			this.compClass = typeof(CompExplosiveTickRare);
		}
	}
	public class CompExplosiveTickRare : CompExplosive
	{
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RimFantasyManager.Instance.compsToTickNormal.Add(this);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (RimFantasyManager.Instance.compsToTickNormal.Contains(this))
            {
                RimFantasyManager.Instance.compsToTickNormal.Remove(this);
            }
            base.PostDestroy(mode, previousMap);
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map);
            if (RimFantasyManager.Instance.compsToTickNormal.Contains(this))
            {
                RimFantasyManager.Instance.compsToTickNormal.Remove(this);
            }
        }
        public override void PostPostMake()
        {
            base.PostPostMake();
            RimFantasyManager.Instance.compsToTickNormal.Add(this);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            RimFantasyManager.Instance.compsToTickNormal.Add(this);
        }
    }
}
