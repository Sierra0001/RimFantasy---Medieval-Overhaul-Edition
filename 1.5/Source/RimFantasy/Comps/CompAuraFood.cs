using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Food : CompProperties_Aura
    {
        public float minFood;
        public float maxFood;
        public CompProperties_Aura_Food()
        {
            this.compClass = typeof(CompAuraFood);
        }
    }
    public class CompAuraFood : CompAura
    {
        public static Dictionary<Map, HashSet<CompAuraFood>> cachedComps = new Dictionary<Map, HashSet<CompAuraFood>>();

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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraFood> { this };
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
        public new CompProperties_Aura_Food Props => base.props as CompProperties_Aura_Food;
        public override bool CanApplyOn(Pawn pawn)
        {
            if (!base.CanApplyOn(pawn))
            {
                return false;
            }
            if (pawn.needs?.food != null)
            {
                var pct = pawn.needs.food.CurLevelPercentage;
                return pct >= Props.minFood && pct <= Props.maxFood;
            }
            return false;
        }
    }
}
