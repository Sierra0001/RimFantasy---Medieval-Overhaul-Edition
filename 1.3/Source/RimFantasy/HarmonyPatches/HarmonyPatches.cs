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
using Verse.AI.Group;
using static Verse.DamageWorker;

namespace RimFantasy
{
	[StaticConstructorOnStartup]
	internal static class HarmonyPatches
	{
		public static Dictionary<Map, AuraManager> areaTemperatureManagers = new Dictionary<Map, AuraManager>();
		static HarmonyPatches()
		{
			Harmony harmony = new Harmony("Sierra.RimFantasy");
			CompTemperatureSource.gasCompType = AccessTools.TypeByName("GasNetwork.CompGasTrader");
			if (CompTemperatureSource.gasCompType != null)
			{
				CompTemperatureSource.methodInfoGasOn = AccessTools.PropertyGetter(CompTemperatureSource.gasCompType, "GasOn");
			}
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(Need_Food), "NeedInterval")]
		internal static class Patch_FoodNeedInterval
		{
			private static void Postfix(Need_Food __instance, Pawn ___pawn)
			{
				if (___pawn.Map != null && CompAuraFood.cachedComps.TryGetValue(___pawn.Map, out var comps))
                {
					foreach (var comp in comps)
                    {
						if (comp.CanApplyOn(___pawn))
                        {
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastNonStarvingTick").SetValue(Find.TickManager.TicksGame);
						}
					}
                }
			}
		}

		[HarmonyPatch(typeof(Need_Rest), "NeedInterval")]
		internal static class Patch_RestNeedInterval
		{
			private static void Postfix(Need_Rest __instance, Pawn ___pawn)
			{
				if (___pawn.Map != null && CompAuraRest.cachedComps.TryGetValue(___pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(___pawn))
						{
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastRestTick").SetValue(Find.TickManager.TicksGame);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Need_Joy), "NeedInterval")]
		public static class FallNeedInterval_Patch
		{
			private static void Postfix(Pawn ___pawn, Need_Joy __instance)
			{
				if (___pawn.Map != null && CompAuraJoy.cachedComps.TryGetValue(___pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(___pawn))
						{
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastGainTick").SetValue(Find.TickManager.TicksGame);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(ThoughtHandler), "TotalMoodOffset")]
		public static class TotalMoodOffset_Patch
		{
			private static void Postfix(ThoughtHandler __instance, ref float __result)
			{
				if (__instance.pawn.Map != null && CompAuraMood.cachedComps.TryGetValue(__instance.pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(__instance.pawn))
						{
							__result += comp.Props.auraStrength;
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
		public static class Patch_Pawn_SpawnSetup
		{
			private static void Postfix(Pawn __instance)
			{
				foreach (var comp in __instance.AllComps)
                {
					if (comp is CompAura aura)
                    {
						aura.SpawnSetup();
                    }
                }
				if (__instance.apparel?.WornApparel != null)
                {
					foreach (var thing in __instance.apparel.WornApparel)
					{
						foreach (var comp in thing.AllComps)
                        {
							if (comp is CompAura aura)
							{
								aura.SpawnSetup();
							}
						}
					}
				}
				if (__instance.equipment?.AllEquipmentListForReading != null)
                {
					foreach (var thing in __instance.equipment.AllEquipmentListForReading)
					{
						foreach (var comp in thing.AllComps)
						{
							if (comp is CompAura aura)
							{
								aura.SpawnSetup();
							}
						}
					}
				}
				if (__instance.inventory?.innerContainer != null)
				{
					foreach (var thing in __instance.inventory.innerContainer)
					{
						if (thing is ThingWithComps withComps)
                        {
							foreach (var comp in withComps.AllComps)
							{
								if (comp is CompAura aura)
								{
									aura.SpawnSetup();
								}
							}
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
		public static class Patch_SpawnSetup
		{
			private static void Postfix(Building __instance)
			{
				if (areaTemperatureManagers.TryGetValue(__instance.Map, out AuraManager proxyHeatManager))
				{
					foreach (var comp in proxyHeatManager.compAuras)
					{
						if (comp.InRangeAndActive(__instance.Position))
						{
							proxyHeatManager.MarkDirty(comp);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
		public static class Patch_DeSpawn
		{
			private static void Prefix(Building __instance)
			{
				if (areaTemperatureManagers.TryGetValue(__instance.Map, out AuraManager proxyHeatManager))
				{
					foreach (var comp in proxyHeatManager.compAuras)
					{
						if (comp.InRangeAndActive(__instance.Position))
						{
							proxyHeatManager.MarkDirty(comp);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
		public static class Patch_TemperatureString
		{
			private static string indoorsUnroofedStringCached;

			private static int indoorsUnroofedStringCachedRoofCount = -1;

			private static bool Prefix(ref string __result)
			{
				IntVec3 intVec = UI.MouseCell();
				IntVec3 c = intVec;
				Room room = intVec.GetRoom(Find.CurrentMap);
				if (room == null)
				{
					for (int i = 0; i < 9; i++)
					{
						IntVec3 intVec2 = intVec + GenAdj.AdjacentCellsAndInside[i];
						if (intVec2.InBounds(Find.CurrentMap))
						{
							Room room2 = intVec2.GetRoom(Find.CurrentMap);
							if (room2 != null && ((!room2.PsychologicallyOutdoors && !room2.UsesOutdoorTemperature) || (!room2.PsychologicallyOutdoors && (room == null || room.PsychologicallyOutdoors)) || (room2.PsychologicallyOutdoors && room == null)))
							{
								c = intVec2;
								room = room2;
							}
						}
					}
				}
				if (room == null && intVec.InBounds(Find.CurrentMap))
				{
					Building edifice = intVec.GetEdifice(Find.CurrentMap);
					if (edifice != null)
					{
						foreach (IntVec3 item in edifice.OccupiedRect().ExpandedBy(1).ClipInsideMap(Find.CurrentMap))
						{
							room = item.GetRoom(Find.CurrentMap);
							if (room != null && !room.PsychologicallyOutdoors)
							{
								c = item;
								break;
							}
						}
					}
				}
				string text;
				if (c.InBounds(Find.CurrentMap) && !c.Fogged(Find.CurrentMap) && room != null && !room.PsychologicallyOutdoors)
				{
					if (room.OpenRoofCount == 0)
					{
						text = "Indoors".Translate();
					}
					else
					{
						if (indoorsUnroofedStringCachedRoofCount != room.OpenRoofCount)
						{
							indoorsUnroofedStringCached = "IndoorsUnroofed".Translate() + " (" + room.OpenRoofCount.ToStringCached() + ")";
							indoorsUnroofedStringCachedRoofCount = room.OpenRoofCount;
						}
						text = indoorsUnroofedStringCached;
					}
				}
				else
				{
					text = "Outdoors".Translate();
				}
				var map = Find.CurrentMap;
				float num = 0f;
				if (room == null || c.Fogged(map))
				{
					num = GetOutDoorTemperature(Find.CurrentMap.mapTemperature.OutdoorTemp, map, c);
				}
				else
				{
					num = GetOutDoorTemperature(room.Temperature, map, c);
				}
				__result = text + " " + num.ToStringTemperature("F0");
				return false;
			}

			private static float GetOutDoorTemperature(float result, Map map, IntVec3 cell)
			{
				if (areaTemperatureManagers.TryGetValue(map, out AuraManager proxyHeatManager))
				{
					return proxyHeatManager.GetTemperatureOutcomeFor(cell, result);
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(Thing), nameof(Thing.AmbientTemperature), MethodType.Getter)]
		public static class Patch_AmbientTemperature
		{
			private static void Postfix(Thing __instance, ref float __result)
			{
				var map = __instance.Map;
				if (map != null && areaTemperatureManagers.TryGetValue(map, out AuraManager proxyHeatManager))
				{
					__result = proxyHeatManager.GetTemperatureOutcomeFor(__instance.Position, __result);
				}
			}
		}

		[HarmonyPatch(typeof(PlantUtility), nameof(PlantUtility.GrowthSeasonNow))]
		public static class Patch_GrowthSeasonNow
		{
			private static bool Prefix(ref bool __result, IntVec3 c, Map map, bool forSowing = false)
			{
				if (areaTemperatureManagers.TryGetValue(map, out AuraManager proxyHeatManager))
				{
					var tempResult = proxyHeatManager.GetTemperatureOutcomeFor(c, 0f);
					if (tempResult != 0)
					{
						float temperature = c.GetTemperature(map) + tempResult;
						if (temperature > 0f)
						{
							__result = temperature < 58f;
						}
						else
						{
							__result = false;
						}
						return false;
					}
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(GenTemperature), "TryGetTemperatureForCell")]
		public static class Patch_TryGetTemperatureForCell
		{
			private static void Postfix(bool __result, IntVec3 c, Map map, ref float tempResult)
			{
				if (__result)
				{
					if (areaTemperatureManagers.TryGetValue(map, out AuraManager proxyHeatManager))
					{
						tempResult = proxyHeatManager.GetTemperatureOutcomeFor(c, tempResult);
					}
				}
			}
		}

		[HarmonyPatch(typeof(PawnRenderer))]
		[HarmonyPatch("DrawEquipment")]
		public static class DrawEquipment_Patch
		{
			public const float drawSYSYPosition = 0.03904f;
			public static void Postfix(Pawn ___pawn, Vector3 rootLoc, Rot4 pawnRotation, PawnRenderFlags flags)
			{
				Pawn pawn = ___pawn;
				if (pawn.Dead || !pawn.Spawned || pawn.equipment == null || pawn.equipment.Primary == null || (pawn.CurJob != null && pawn.CurJob.def.neverShowWeapon))
				{
					return;
				}
				if (pawn.equipment.Primary.TryGetCachedComp<CompWornWeapon>(out var comp) && comp != null && comp.ShouldShowWeapon(___pawn, flags))
				{
					DrawWornWeapon(comp, ___pawn, rootLoc, comp.FullGraphic);
				}
				if (pawn.equipment.Primary.TryGetCachedComp<CompArcaneWeapon>(out var compArcaneWeapon))
                {
					compArcaneWeapon.DrawWornExtras();
				}
			}
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
			{
				var pawnInfo = AccessTools.Field(typeof(PawnRenderer), "pawn");
				var method = AccessTools.Method(typeof(DrawEquipment_Patch), "DrawSheathOnlyGraphic");
				var carryWeaponOpenly = AccessTools.Method(typeof(PawnRenderer), "CarryWeaponOpenly");
				var rotatedBy = AccessTools.Method(typeof(Vector3Utility), "RotatedBy", new Type[] {typeof(Vector3), typeof(float) });
				var codes = instructions.ToList();
				for (var i = 0; i < codes.Count; i++)
                {
					if (i > 5 && (codes[i - 4].opcode == OpCodes.Ldloc_3 && codes[i - 3].Calls(rotatedBy) || codes[i - 2].Calls(carryWeaponOpenly)))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0, null);
						yield return new CodeInstruction(OpCodes.Ldfld, pawnInfo);
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						yield return new CodeInstruction(OpCodes.Call, method);
                    }
					yield return codes[i];
                }
			}

			public static void DrawSheathOnlyGraphic(Pawn pawn, Vector3 rootLoc)
            {
				var eq = pawn.equipment.Primary;
				if (eq.TryGetCachedComp<CompWornWeapon>(out var comp))
				{
					DrawWornWeapon(comp, pawn, rootLoc, comp.SheathOnlyGraphic);
				}
			}

			public static void DrawWornWeapon(CompWornWeapon CompWornWeapon, Pawn pawn, Vector3 drawLoc, Graphic graphic)
			{
				switch (CompWornWeapon.Props.drawPosition)
				{
					case DrawPosition.Side:
						if (pawn.Rotation == Rot4.South)
						{
							drawLoc += CompWornWeapon.Props.northOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.northOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.North)
						{
							drawLoc += CompWornWeapon.Props.southOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.southOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.East)
						{
							drawLoc += CompWornWeapon.Props.eastOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.eastOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.West)
						{
							drawLoc += CompWornWeapon.Props.westOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.westOffset.angle, graphic);
							return;
						}
						break;
					case DrawPosition.Back:
						if (pawn.Rotation == Rot4.South)
						{
							drawLoc += CompWornWeapon.Props.southOffset.position;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.southOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.North)
						{
							drawLoc += CompWornWeapon.Props.northOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.northOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.East)
						{
							drawLoc += CompWornWeapon.Props.eastOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.eastOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.West)
						{
							drawLoc += CompWornWeapon.Props.westOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawWornWeapon(pawn, CompWornWeapon, drawLoc, CompWornWeapon.Props.westOffset.angle, graphic);
							return;
						}
						break;
					default:
						return;
				}
			}
			public static void DrawWornWeapon(Pawn pawn, CompWornWeapon comp, Vector3 drawLoc, float aimAngle, Graphic graphic)
			{
				float num = aimAngle;
				num %= 360f;
				if (comp != null)
				{
					Graphics.DrawMesh(graphic.MeshAt(pawn.Rotation), drawLoc, Quaternion.AngleAxis(num, Vector3.up), graphic.MatAt(pawn.Rotation), 0);
				}
			}
		}

		[HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
		public static class Patch_DamageInfosToApply
		{
			private static void Postfix(ref IEnumerable<DamageInfo> __result, Verb __instance, LocalTargetInfo target)
			{
				if (__instance.EquipmentSource.TryGetCachedComp<CompArcaneWeapon>(out var comp))
				{
					foreach (var trait in comp.TraitsListForReading)
					{
						if (trait is ArcaneWeaponTraitDef arcaneDef)
						{
							foreach (var damageInfo in __result)
                            {
								arcaneDef.Worker.OnDamageDealt(__instance.EquipmentSource, damageInfo, comp, __instance.Caster, target);
                            }
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
		public static class Patch_TryCastShot
		{
			public static Verb verbSource;
			private static void Prefix(Verb __instance)
			{
				verbSource = __instance;
			}
			private static void Postfix(Verb __instance)
			{
				verbSource = null;
			}
		}

		[HarmonyPatch(typeof(Projectile), "Launch", new Type[]
		{
			typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
		})]
		public static class Patch_Projectile_Launch
		{
			public static void Postfix(Projectile __instance, Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, Thing equipment = null, ThingDef targetCoverDef = null)
			{
				if (Patch_TryCastShot.verbSource != null && Patch_TryCastShot.verbSource.EquipmentSource.TryGetCachedComp<CompArcaneWeapon>(out var comp))
                {
					comp.releasedProjectiles.Add(__instance);
				}
			}
		}

		[HarmonyPatch(typeof(Bullet), "Impact")]
		public static class Patch_Bullet_Impact
		{
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
			{
				bool found = false;
				var associateWithLog = AccessTools.Method(typeof(DamageResult), "AssociateWithLog");
				var applyEffects = AccessTools.Method(typeof(Patch_Bullet_Impact), "ApplyEffects");
				var codes = instructions.ToList();
				for (var i = 0; i < codes.Count; i++)
				{
					yield return codes[i];
					if (!found && codes[i].Calls(associateWithLog))
                    {
						found = true;
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						yield return new CodeInstruction(OpCodes.Ldloc_3);
						yield return new CodeInstruction(OpCodes.Call, applyEffects);
                    }
				}
			}

			public static void ApplyEffects(Projectile projectile, Thing hitThing, DamageInfo damageInfo)
            {
				if (hitThing != null)
                {
					var comp = CompArcaneWeapon.GetLinkedCompFor(projectile);
					if (comp != null)
                    {
						foreach (var trait in comp.TraitsListForReading)
                        {
							if (trait is ArcaneWeaponTraitDef arcaneTraitDef)
                            {
								arcaneTraitDef.Worker.OnDamageDealt(projectile, damageInfo, comp, comp.Wearer, hitThing);
							}
                        }
                    }
                }
            }
		}

		[HarmonyPatch(typeof(VerbProperties), "AdjustedCooldown", new Type[]
		{
			typeof(Verb), typeof(Pawn)
		})]
		public static class Patch_AdjustedCooldown
		{
			public static void Postfix(ref float __result, Verb ownerVerb, Pawn attacker)
			{
				var pawn = ownerVerb.CasterPawn;
				if (pawn != null && pawn.health?.hediffSet?.hediffs != null)
                {
					foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
						var hediffExtension = hediff.def.GetModExtension<HediffExtension>();
						if (hediffExtension != null && hediffExtension.cooldownMultiplier.HasValue)
                        {
							__result *= hediffExtension.cooldownMultiplier.Value;
						}
                    }
                }
			}
		}

		[HarmonyPatch(typeof(Pawn), "GetGizmos")]
		public class Pawn_GetGizmos_Patch
		{
			public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
			{
				foreach (var g in __result)
				{
					yield return g;
				}
				if (__instance.Faction == Faction.OfPlayer && __instance.equipment?.Primary != null 
					&& __instance.equipment.Primary.TryGetCachedComp<CompArcaneWeapon>(out var comp) && comp.shieldTraitDef != null)
				{
					if (Find.Selector.SingleSelectedThing == comp.Wearer)
					{
						Gizmo_EnergyShieldStatus gizmo_EnergyShieldStatus = new Gizmo_EnergyShieldStatus();
						gizmo_EnergyShieldStatus.shield = comp;
						yield return gizmo_EnergyShieldStatus;
					}
				}
			}
		}

		[HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
		public class Pawn_PreApplyDamage_Patch
		{
			[HarmonyPriority(Priority.Last)]
			public static bool Prefix(Pawn ___pawn, DamageInfo dinfo, out bool absorbed)
			{
				absorbed = false;
				if (___pawn.equipment?.Primary != null && ___pawn.equipment.Primary.TryGetCachedComp<CompArcaneWeapon>(out var comp))
                {
					if (comp.CheckPreAbsorbDamage(dinfo))
                    {
						Faction homeFaction = ___pawn.HomeFaction;
						if (dinfo.Instigator != null && homeFaction != null && homeFaction.IsPlayer && !___pawn.InAggroMentalState)
						{
							Pawn pawn = dinfo.Instigator as Pawn;
							if (dinfo.InstigatorGuilty && pawn != null && pawn.guilt != null && pawn.mindState != null)
							{
								pawn.guilt.Notify_Guilty();
							}
						}
						if (___pawn.Spawned)
						{
							if (!___pawn.Position.Fogged(___pawn.Map))
							{
								___pawn.mindState.Active = true;
							}
							___pawn.GetLord()?.Notify_PawnDamaged(___pawn, dinfo);
							if (dinfo.Def.ExternalViolenceFor(___pawn))
							{
								GenClamor.DoClamor(___pawn, 18f, ClamorDefOf.Harm);
							}
							___pawn.jobs.Notify_DamageTaken(dinfo);
						}
						if (homeFaction != null)
						{
							homeFaction.Notify_MemberTookDamage(___pawn, dinfo);
							if (Current.ProgramState == ProgramState.Playing && homeFaction == Faction.OfPlayer && dinfo.Def.ExternalViolenceFor(___pawn) && ___pawn.SpawnedOrAnyParentSpawned)
							{
								___pawn.MapHeld.dangerWatcher.Notify_ColonistHarmedExternally();
							}
						}
						absorbed = true;
						return false;
					}
                }
				return true;
			}
		}

		[HarmonyPatch(typeof(CompExplosive), "StartWick")]
		public static class Patch_StartWick
		{
			private static bool Prefix(CompExplosive __instance, Thing instigator = null)
			{
				if (__instance is CompExplosiveStuffed compExplosiveStuffed)
                {
					compExplosiveStuffed.StartWick(instigator);
					return false;
                }
				return true;
			}
		}
	}
}
