using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Joy : CompProperties_Aura
    {
        public float minJoy;
        public float maxJoy;
        public CompProperties_Aura_Joy()
        {
            this.compClass = typeof(CompAuraJoy);
        }
    }
    public class CompAuraJoy : CompAura
    {
        public static Dictionary<Map, HashSet<CompAuraJoy>> cachedComps = new Dictionary<Map, HashSet<CompAuraJoy>>();
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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraJoy> { this };
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
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
        public new CompProperties_Aura_Joy Props => base.props as CompProperties_Aura_Joy;
        public override bool CanApplyOn(Pawn pawn)
        {
            if (!base.CanApplyOn(pawn))
            {
                return false;
            }
            if (pawn.needs?.joy != null)
            {
                var pct = pawn.needs.joy.CurLevelPercentage;
                return pct >= Props.minJoy && pct <= Props.maxJoy;
            }
            return false;
        }
    }
}
