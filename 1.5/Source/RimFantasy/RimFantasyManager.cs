using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimFantasy
{
    public class RimFantasyManager : GameComponent
    {
        public List<ThingComp> compsToTickNormal = new List<ThingComp>();
        public List<CompGlowerStuffable> compGlowerToTick = new List<CompGlowerStuffable>();
        public static RimFantasyManager Instance;
        public RimFantasyManager(Game game)
        {
            Init();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Init();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Init();
        }
        void Init()
        {
            Instance = this;
            if (compsToTickNormal is null)
            {
                compsToTickNormal = new List<ThingComp>();
            }
        }
        public override void GameComponentTick()
        {
            for (int num = compGlowerToTick.Count - 1; num >= 0; num--)
            {
                compGlowerToTick[num].Tick();
            }
            for (int num = compsToTickNormal.Count - 1; num >= 0; num--)
            {
                compsToTickNormal[num].CompTick();
            }
        }
    }
}
