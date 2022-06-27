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

namespace RimFantasy
{
	public static class Utils
	{
        private static Dictionary<Type, Dictionary<ThingWithComps, ThingComp>> cachedComps = new Dictionary<Type, Dictionary<ThingWithComps, ThingComp>>();
        public static bool TryGetCachedComp<T>(this ThingWithComps thing, out T comp) where T : ThingComp
        {
            comp = null;
            if (thing != null)
            {
                if (!cachedComps.TryGetValue(typeof(T), out var comps))
                {
                    comps = cachedComps[typeof(T)] = new Dictionary<ThingWithComps, ThingComp>();
                }
                if (!comps.TryGetValue(thing, out var comp2))
                {
                    comp2 = thing.TryGetComp<T>();
                }
                comp = comp2 as T;
                return comp != null;
            }
            return false;
        }
        public static bool IsCarryingWeaponOpenly(this Pawn pawn)
        {
            if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
            {
                return false;
            }
            if (pawn.Drafted)
            {
                return true;
            }
            if (pawn.CurJob?.def?.alwaysShowWeapon ?? false)
            {
                return true;
            }
            if (pawn.mindState?.duty?.def?.alwaysShowWeapon ?? false)
            {
                return true;
            }
            Lord lord = pawn.GetLord();
            if (lord != null && lord.LordJob != null && lord.LordJob.AlwaysShowWeapon)
            {
                return true;
            }
            return false;
        }

        public static bool PsychologicallyOutdoors(this IntVec3 cell, Map map)
        {
            return cell.GetRoom(map)?.PsychologicallyOutdoors ?? false;
        }
    }
}
