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
    public class AuraManager : MapComponent
	{
        public bool dirty = false;
        public Dictionary<IntVec3, List<CompTemperatureSource>> temperatureSources = new Dictionary<IntVec3, List<CompTemperatureSource>>();
        public List<CompAura> compAuras = new List<CompAura>();
        public HashSet<CompAura> compAurasToTick = new HashSet<CompAura>();
        private List<CompAura> dirtyComps = new List<CompAura>();
        public AuraManager(Map map) : base(map)
		{
            HarmonyPatches.areaTemperatureManagers[map] = this;
            PreInit();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PreInit();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            PreInit();
        }
        public void PreInit()
        {
            if (compAuras is null)
            {
                compAuras = new List<CompAura>();
            }
            if (compAurasToTick is null)
            {
                compAurasToTick = new HashSet<CompAura>();
            }
            if (temperatureSources is null)
            {
                temperatureSources = new Dictionary<IntVec3, List<CompTemperatureSource>>();
            }
            if (dirtyComps is null)
            {
                dirtyComps = new List<CompAura>();
            }
        }
        public void MarkDirty(CompAura comp)
        {
            dirtyComps.Add(comp);
            dirty = true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (this.dirty)
            {
                foreach (var comp in dirtyComps)
                {
                    if (comp != null)
                    {
                        if (comp.parent.MapHeld != null)
                        {
                            comp.RecalculateAffectedCells();
                            if (!comp.AffectedCells.Any())
                            {
                                comp.UnConnectFromManager();
                            }
                        }
                        else
                        {
                            comp.UnConnectFromManager();
                        }
                    }
                }
                dirtyComps.Clear();
                this.dirty = false;
            }

            foreach (var comp in compAurasToTick)
            {
                comp.Tick();
            }
        }

        public float GetTemperatureOutcomeFor(IntVec3 cell, float result)
        {
            if (temperatureSources.TryGetValue(cell, out List<CompTemperatureSource> tempSources))
            {
                var tempResult = result;
                foreach (var tempSourceCandidate in tempSources)
                {
                    var props = tempSourceCandidate.Props;
                    var tempOutcome = tempSourceCandidate.TemperatureOutcome;
                    if (tempOutcome != 0)
                    {
                        if (props.maxTemperature.HasValue && result >= props.maxTemperature.Value && tempOutcome > 0)
                        {
                            continue;
                        }
                        else if (props.minTemperature.HasValue && props.minTemperature.Value >= result && tempOutcome < 0)
                        {
                            continue;
                        }
                        tempResult += tempOutcome;
                        if (props.maxTemperature.HasValue && result < props.maxTemperature.Value && tempResult > props.maxTemperature.Value && tempResult > result)
                        {
                            tempResult = props.maxTemperature.Value;
                        }
                        else if (props.minTemperature.HasValue && props.minTemperature.Value > tempResult && result > tempResult)
                        {
                            tempResult = props.minTemperature.Value;
                        }
                    }
                }
                result = tempResult;
            }
            return result;
        }
    }
}
