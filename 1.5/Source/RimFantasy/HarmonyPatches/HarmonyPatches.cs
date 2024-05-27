using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (HarmonyPatches.areaTemperatureManagers.TryGetValue(__instance.Map, out AuraManager auraManager))
            {
                foreach (var comp in auraManager.compAuras)
                {
                    if (comp.InRangeAndActive(__instance.Position))
                    {
                        auraManager.MarkDirty(comp);
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
            if (HarmonyPatches.areaTemperatureManagers.TryGetValue(__instance.Map, out AuraManager auraManager))
            {
                foreach (var comp in auraManager.compAuras)
                {
                    if (comp.InRangeAndActive(__instance.Position))
                    {
                        auraManager.MarkDirty(comp);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
    public static class GlobalControls_TemperatureString_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var codes = codeInstructions.ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (code.opcode == OpCodes.Stloc_S && code.operand is LocalBuilder lb && lb.LocalIndex == 4)
                {
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 4);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap)));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GlobalControls_TemperatureString_Patch), nameof(ModifyTemperatureIfNeeded)));
                }
            }
        }

        public static void ModifyTemperatureIfNeeded(ref float result, IntVec3 cell, Map map)
        {
            if (HarmonyPatches.areaTemperatureManagers.TryGetValue(map, out AuraManager auraManager))
            {
                result = auraManager.GetTemperatureOutcomeFor(cell, result);
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.AmbientTemperature), MethodType.Getter)]
    public static class Patch_AmbientTemperature
    {
        private static void Postfix(Thing __instance, ref float __result)
        {
            var map = __instance.Map;
            if (map != null && HarmonyPatches.areaTemperatureManagers.TryGetValue(map, out AuraManager auraManager))
            {
                __result = auraManager.GetTemperatureOutcomeFor(__instance.Position, __result);
            }
        }
    }

    [HarmonyPatch(typeof(PlantUtility), nameof(PlantUtility.GrowthSeasonNow))]
    public static class Patch_GrowthSeasonNow
    {
        private static bool Prefix(ref bool __result, IntVec3 c, Map map, bool forSowing = false)
        {
            if (HarmonyPatches.areaTemperatureManagers.TryGetValue(map, out AuraManager auraManager))
            {
                var tempResult = auraManager.GetTemperatureOutcomeFor(c, 0f);
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
                if (HarmonyPatches.areaTemperatureManagers.TryGetValue(map, out AuraManager auraManager))
                {
                    tempResult = auraManager.GetTemperatureOutcomeFor(c, tempResult);
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
            var rotatedBy = AccessTools.Method(typeof(Vector3Utility), "RotatedBy", new Type[] { typeof(Vector3), typeof(float) });
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

    [HarmonyPatch(typeof(Projectile_Explosive), "Impact")]
    public static class Patch_Projectile_Explosive_Impact
    {
        public static void Prefix(Projectile_Explosive __instance)
        {
            var comp = CompArcaneWeapon.GetLinkedCompFor(__instance);
            if (comp != null)
            {
                foreach (var trait in comp.TraitsListForReading)
                {
                    if (trait is ArcaneWeaponTraitDef arcaneTraitDef)
                    {
                        foreach (var hitThing in GenRadial.RadialDistinctThingsAround(__instance.Position, __instance.Map, __instance.def.projectile.explosionRadius, true))
                        {
                            arcaneTraitDef.Worker.OnDamageDealt(comp.parent, new DamageInfo(__instance.def.projectile.damageDef,
                                __instance.def.projectile.GetDamageAmount(comp.parent)), comp, comp.Wearer, hitThing);
                        }
                    }
                }
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
            var applyEffects = AccessTools.Method(typeof(Patch_Bullet_Impact), nameof(ApplyEffects));
            var codes = instructions.ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                yield return codes[i];
                if (!found && codes[i].Calls(associateWithLog))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
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
                            arcaneTraitDef.Worker.OnDamageDealt(comp.parent, damageInfo, comp, comp.Wearer, hitThing);
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

    [HarmonyPatch(typeof(CompAttachBase), "HasAttachment")]
    public static class CompAttachBase_HasAttachment_Patch
    {
        public static void Postfix(ref bool __result, CompAttachBase __instance, ThingDef def)
        {
            if (!__result && def == ThingDefOf.Fire)
            {
                foreach (var customFire in Utils.customFires)
                {
                    if (__instance.GetAttachment(customFire) != null)
                    {
                        __result = true;
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill
    {
        public static Pawn isBeingKilled;

        private static void Prefix(Pawn __instance, out CustomFire __state)
        {
            __state = __instance.GetAttachment(ThingDefOf.Fire) as CustomFire;
            isBeingKilled = __instance;
        }
        private static void Postfix(Pawn __instance, CustomFire __state)
        {
            isBeingKilled = null;
            if (__instance.Dead && __instance.Corpse != null && __state != null)
            {
                var num = __state.CurrentSize();
                if (num > 0f)
                {
                    CustomFire.TryStartFireIn(__state.def, __instance.Corpse.Position, __instance.Corpse.Map, num);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompAttachBase), "GetAttachment")]
    public static class CompAttachBase_GetAttachment_Patch
    {
        public static void Postfix(ref Thing __result, CompAttachBase __instance, ThingDef def)
        {
            if (__result is null && def == ThingDefOf.Fire && Patch_Pawn_Kill.isBeingKilled != __instance.parent)
            {
                foreach (var customFire in Utils.customFires)
                {
                    __result = __instance.GetAttachment(customFire);
                    if (__result != null)
                    {
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Verb_BeatFire), "TryCastShot")]
    public static class Verb_BeatFire_TryCastShot_Patch
    {
        public static void Prefix(Verb_BeatFire __instance, bool __result)
        {
            Fire fire = (Fire)__instance.currentTarget.Thing;
            if (fire.ticksSinceSpawn == 0)
            {
                fire.ticksSinceSpawn = 1;
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Scanner), "PotentialWorkThingsGlobal")]
    public static class WorkGiver_Scanner_PotentialWorkThingsGlobal_Patch
    {
        public static void Postfix(ref IEnumerable<Thing> __result, WorkGiver_Scanner __instance, Pawn pawn)
        {
            if (__instance is WorkGiver_FightFires)
            {
                foreach (var fire in Utils.customFires)
                {
                    if (pawn.Map.listerThings.listsByDef.TryGetValue(fire, out var value) && value.Any())
                    {
                        var list = __result is null ? new List<Thing>() : __result.ToList();
                        foreach (var t in value)
                        {
                            list.Add(t);
                        }
                        __result = list;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(FireWatcher), "UpdateObservations")]
    public static class FireWatcher_UpdateObservations_Patch
    {
        public static void Postfix(FireWatcher __instance)
        {
            foreach (var fireDef in Utils.customFires)
            {
                List<Thing> list = __instance.map.listerThings.ThingsOfDef(fireDef);
                for (int i = 0; i < list.Count; i++)
                {
                    Fire fire = list[i] as Fire;
                    __instance.fireDanger += 0.5f + fire.fireSize;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "DropAndForbidEverything")]
	public static class Pawn_DropAndForbidEverything_Patch
    {
		public static bool shouldCheck;
		public static void Prefix(Pawn __instance)
        {
			shouldCheck = true;
        }

		public static void Postfix()
        {
			shouldCheck = false;
		}
    }

	public class WeaponDropExtension : DefModExtension
    {
		public bool preventDroppingWhenDowned;
		public bool preventDroppingWhenDead;
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), "TryDropEquipment")]
	public static class Pawn_EquipmentTracker_TryDropEquipment_Patch
    {
		public static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
			if (Pawn_DropAndForbidEverything_Patch.shouldCheck)
            {
				var extension = eq.def.GetModExtension<WeaponDropExtension>();
				if (extension != null)
				{
					if (extension.preventDroppingWhenDowned && __instance.pawn.Downed)
					{
						return false;
					}
					else if (extension.preventDroppingWhenDead && __instance.pawn.Dead)
					{
						return false;
					}
				}
			}
			return true;
        }
    }

	[HarmonyPatch(typeof(CompBladelinkWeapon), "CanAddTrait")]
	public static class CompBladelinkWeapon_CanAddTrait_Patch
	{
		public static void Postfix(ref bool __result, WeaponTraitDef trait)
		{
			if (__result && trait is ArcaneWeaponTraitDef) 
			{
				__result = false;
			}
		}
	}
}
