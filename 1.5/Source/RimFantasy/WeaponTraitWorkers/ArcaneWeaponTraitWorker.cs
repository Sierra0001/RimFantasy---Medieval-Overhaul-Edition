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
    public class ArcaneWeaponTraitDef : WeaponTraitDef
    {
        public List<WeaponEffect> weaponEffects;

        public bool isShield;
        public float shieldEnergyMax;
        public float shieldRechargeRate;
        public string shieldTexPath;
        public bool shieldTexStuffPostfix;
        public bool shieldCombatRecovery;
        public bool ignoreRechargeDelay;
        public float? deflectMeleeChance;
        public float? deflectRangeChance;
        public FleckDef fleckDefOnDeflect;
        public float fleckDefOnDeflectScale = 1f;
        public new ArcaneWeaponTraitWorker Worker => base.Worker as ArcaneWeaponTraitWorker;
    }
    public class ArcaneWeaponTraitWorker : WeaponTraitWorker
    {
        public new ArcaneWeaponTraitDef def => base.def as ArcaneWeaponTraitDef;
        public virtual void OnDamageDealt(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (def.weaponEffects != null)
            {
                foreach (var weaponEffect in def.weaponEffects)
                {
                    if (Rand.Chance(weaponEffect.effectChance))
                    {
                        weaponEffect.DoEffect(attackSource, damageInfo, comp, attacker, target);
                    }
                }
            }
        }
    }
}