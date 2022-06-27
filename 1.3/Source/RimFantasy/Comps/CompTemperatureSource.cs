using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimFantasy
{
	public class CompProperties_Aura_Temperature : CompProperties_Aura
	{
		public float? minTemperature;
		public float? maxTemperature;

		public float smeltSnowRadius;
		public float smeltSnowAtTemperature;
		public float smeltSnowPower;
		public CompProperties_Aura_Temperature()
		{
			compClass = typeof(CompTemperatureSource);
		}
	}
	public class CompTemperatureSource : CompAura
	{
		public new CompProperties_Aura_Temperature Props => (CompProperties_Aura_Temperature)props;

		public float TemperatureOutcome
        {
			get
            {
				return this.Props.auraStrength;
            }
        }

        public override void RecalculateAffectedCells()
        {
            base.RecalculateAffectedCells();
			foreach (var cell in affectedCells)
			{
				if (Manager.temperatureSources.ContainsKey(cell))
				{
					Manager.temperatureSources[cell].Add(this);
				}
				else
				{
					Manager.temperatureSources[cell] = new List<CompTemperatureSource> { this };
				}
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			base.PostSpawnSetup(respawningAfterLoad);
			SpawnSetup();
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
			SpawnSetup();
		}
		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			base.PostDestroy(mode, previousMap);
			if (Manager.compAurasToTick.Contains(this))
			{
				Manager.compAurasToTick.Remove(this);
			}
		}

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
			if (this.TemperatureOutcome >= 0)
            {
				GenDraw.DrawFieldEdges(affectedCellsList, GenTemperature.ColorRoomHot);
            }
			else
            {
				GenDraw.DrawFieldEdges(affectedCellsList, GenTemperature.ColorRoomCold);
			}
		}
		
		public override void Tick()
        {
			base.Tick();
			if (active)
            {
				if (MapHeld != null)
				{
					var cellToSmeltSnow = new HashSet<IntVec3>();
					if (Props.smeltSnowRadius > 0)
                    {
						foreach (var cell in ParentHeld.OccupiedRect())
						{
							foreach (var cell2 in GenRadial.RadialCellsAround(cell, Props.smeltSnowRadius, true))
							{
								if (cell2.GetSnowDepth(MapHeld) > 0 && HarmonyPatches.areaTemperatureManagers.TryGetValue(MapHeld, out AuraManager proxyHeatManager))
								{
									var finalTemperature = proxyHeatManager.GetTemperatureOutcomeFor(cell2, cell2.GetTemperature(MapHeld));
									if (finalTemperature >= Props.smeltSnowAtTemperature)
									{
										cellToSmeltSnow.Add(cell2);
									}
								}
							}
						}
					}


					foreach (var cell in cellToSmeltSnow)
					{
						MapHeld.snowGrid.AddDepth(cell, -Props.smeltSnowPower);
					}
				}
			}
		}

        public override void UnConnectFromManager()
        {
			base.UnConnectFromManager();
			foreach (var data in Manager.temperatureSources.Values)
			{
				if (data.Contains(this))
				{
					data.Remove(this);
				}
			}
		}
    }
}
