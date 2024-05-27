using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimFantasy
{
    public class CompScheduleTickNormal : CompSchedule
    {
        public override void CompTick()
        {
            base.CompTick();
            RecalculateAllowed();
        }
    }
}
