using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Mood : CompProperties_Aura
    {
        public float minMood;
        public float maxMood;
        public CompProperties_Aura_Mood()
        {
            this.compClass = typeof(CompAuraMood);
        }
    }
    public class CompAuraMood : CompAura
    {
        public static Dictionary<Map, HashSet<CompAuraMood>> cachedComps = new Dictionary<Map, HashSet<CompAuraMood>>();
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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraMood> { this };
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
        public new CompProperties_Aura_Mood Props => base.props as CompProperties_Aura_Mood;
        public override bool CanApplyOn(Pawn pawn)
        {
            if (!base.CanApplyOn(pawn))
            {
                return false;
            }
            if (pawn.needs?.mood != null)
            {
                var pct = pawn.needs.mood.CurLevelPercentage;
                return pct >= Props.minMood && pct <= Props.maxMood;
            }
            return false;
        }
    }
}
