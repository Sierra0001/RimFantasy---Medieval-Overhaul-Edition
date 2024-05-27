using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Rest : CompProperties_Aura
    {
        public float minRest;
        public float maxRest;
        public CompProperties_Aura_Rest()
        {
            this.compClass = typeof(CompAuraRest);
        }
    }
    public class CompAuraRest : CompAura
    {
        public static Dictionary<Map, HashSet<CompAuraRest>> cachedComps = new Dictionary<Map, HashSet<CompAuraRest>>();

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            if (this.parent.MapHeld != null)
            {
                if (cachedComps.ContainsKey(this.parent.MapHeld))
                {
                    cachedComps[this.parent.MapHeld].Add(this);
                }
                else
                {
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraRest> { this };
                }
            }
        }
        public override void PostDeSpawn(Map map)
        {
            if (cachedComps.ContainsKey(map))
            {
                cachedComps[map].Remove(this);
            }
            base.PostDeSpawn(map);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (previousMap != null && cachedComps.ContainsKey(previousMap))
            {
                cachedComps[previousMap].Remove(this);
            }
            base.PostDestroy(mode, previousMap);
        }
        public new CompProperties_Aura_Rest Props => base.props as CompProperties_Aura_Rest;
        public override bool CanApplyOn(Pawn pawn)
        {
            if (!base.CanApplyOn(pawn))
            {
                return false;
            }
            if (pawn.needs?.rest != null)
            {
                var pct = pawn.needs.rest.CurLevelPercentage;
                return pct >= Props.minRest && pct <= Props.maxRest;
            }
            return false;
        }
    }
}
