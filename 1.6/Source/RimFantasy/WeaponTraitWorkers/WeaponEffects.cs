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
    public class WeaponEffect
    {
        public float effectChance;
        public DamageDef effectDamage;
        public int? baseDamageValue;
        public FleckDef fleckDefOnTarget;
        public FleckDef fleckDefOnSelf;
        public float fleckDefOnSelfScale = 1;
        public float fleckScale = 1;
        public virtual void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && !target.ThingDestroyed)
            {
                if (fleckDefOnTarget != null)
                {
                    FleckMaker.Static(target.Cell, target.Thing.Map, fleckDefOnTarget, fleckScale);
                }
                if (fleckDefOnSelf != null)
                {
                    FleckMaker.Static(attacker.Position, attacker.Map, fleckDefOnSelf, fleckDefOnSelfScale);
                }
                if (effectDamage != null)
                {
                    var damageValue = baseDamageValue.HasValue ? baseDamageValue.Value : effectDamage.defaultDamage;
                    target.Thing.TakeDamage(new DamageInfo(effectDamage, damageValue, instigator: attacker, weapon: comp.parent.def));
                }
            }
        }
    }

    public class WeaponEffect_SetOnFire : WeaponEffect
    {
        public bool setsTargetOnFire;
        public FloatRange fireSize;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (setsTargetOnFire && target.HasThing && !target.ThingDestroyed)
            {
                var extension = attackSource.def.GetModExtension<RimFantasyExtension>();
                var fireDefName = extension != null ? "RF_" + extension.stuffKey + "Fire" : "Fire";
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(fireDefName) ?? ThingDefOf.Fire;
                CustomFire.TryAttachFire(target.Thing, def, fireSize.RandomInRange);
            }
            base.DoEffect(attackSource, damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_ApplyHediff : WeaponEffect
    {
        public HediffDef hediffDef;
        public BodyPartDef partToApply;
        public float severityOffset = 1f;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {
                if (victim.health.hediffSet.GetFirstHediffOfDef(hediffDef) is null)
                {
                    var part = partToApply != null ? victim.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == partToApply) : null;
                    if (typeof(Hediff_Injury).IsAssignableFrom(hediffDef.hediffClass))
                    {
                        IEnumerable<BodyPartRecord> source = from x in victim.health.hediffSet.GetNotMissingParts() where x.depth == BodyPartDepth.Outside
                                                             && !victim.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(x) select x;
                        part = source.RandomElementByWeight((BodyPartRecord x) => x.coverageAbs);

                        if (part is null)
                        {
                            part = victim.health.hediffSet.GetNotMissingParts().RandomElement();
                        }
                    }
                    var hediff = HediffMaker.MakeHediff(hediffDef, victim, part);
                    victim.health.AddHediff(hediff);
                }

                if (severityOffset != 0f)
                {
                    Hediff firstHediffOfDef = victim.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                    if (firstHediffOfDef != null)
                    {
                        firstHediffOfDef.Severity += severityOffset;
                    }
                }

            }
            base.DoEffect(attackSource, damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Drain : WeaponEffect
    {
        public FloatRange hpToDrain;
        public DamageDef drainDamage;
        public IntRange numBodyPartsToDrain = new IntRange(999, 999);
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {
                var num = hpToDrain.RandomInRange;
                var partsToDrain = victim.health.hediffSet.GetNotMissingParts().Where(x => x.depth == BodyPartDepth.Outside && x.coverageAbs > 0).InRandomOrder();
                var count = 0;
                var maxToDamage = numBodyPartsToDrain.RandomInRange;
                foreach (var part in partsToDrain)
                {
                    if (num <= 0)
                    {
                        break;
                    }
                    var partHealth = victim.health.hediffSet.GetPartHealth(part);
                    var toDrain = partHealth > num ? Mathf.Min(partHealth - num, num) : partHealth;
                    num -= toDrain;
                    victim.TakeDamage(new DamageInfo(drainDamage, toDrain, hitPart: part, instigator: attacker, weapon: comp.parent.def));
                    count++;
                    if (count > maxToDamage)
                    {
                        break;
                    }
                }
            }
            base.DoEffect(attackSource, damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Heal : WeaponEffect
    {
        public FloatRange hpToHeal;

        public static void Heal(FloatRange hpToHeal, Pawn pawn)
        {
            var num = hpToHeal.RandomInRange;
            var hediffSet = pawn.health.hediffSet;
            var hediffs = hediffSet.hediffs.OfType<Hediff_Injury>().Where(x => x.CanHealNaturally() || x.CanHealFromTending()).InRandomOrder();
            foreach (var hediff in hediffs)
            {
                if (num <= 0)
                {
                    break;
                }

                var toHeal = hediff.Severity > num ? Mathf.Min(hediff.Severity - num, num) : hediff.Severity;
                num -= toHeal;
                hediff.Heal(toHeal);
            }
        }
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn && attacker is Pawn pawn && pawn.health?.hediffSet != null)
            {
                Heal(hpToHeal, pawn);
                base.DoEffect(attackSource, damageInfo, comp, attacker, target);
            }
        }
    }

    public class WeaponEffect_HealTarget : WeaponEffect
    {
        public FloatRange hpToHeal;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn pawn && pawn.health?.hediffSet != null)
            {
                WeaponEffect_Heal.Heal(hpToHeal, pawn);
                base.DoEffect(attackSource, damageInfo, comp, attacker, target);
            }
        }
    }

    public class WeaponEffect_Stun : WeaponEffect
    {
        public IntRange stunDuration;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim && victim.stances?.stunner != null)
            {
                victim.stances.stunner.StunFor(stunDuration.RandomInRange, attacker);
                base.DoEffect(attackSource, damageInfo, comp, attacker, target);
            }
        }
    }
    public class WeaponEffect_Slash : WeaponEffect
    {
        public DamageDef damageDef;
        public IntRange amountOfEnemies;
        public float maxDistance;
        public float baseDamageFactor = 1f;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.Thing != null)
            {
                var num = amountOfEnemies.RandomInRange;
                var damAmount = baseDamageValue.HasValue ? baseDamageValue.Value : damageInfo.Amount;
                var damDef = damageDef != null ? damageDef : damageInfo.Def;
                target.Thing.TakeDamage(new DamageInfo(damDef, damAmount, instigator: attacker, weapon: comp.parent.def));
                foreach (var thing in GenRadial.RadialDistinctThingsAround(attackSource.PositionHeld, comp.parent.MapHeld, maxDistance, true)
                    .OfType<Pawn>().Where(x => x.Faction == target.Thing.Faction && x != target.Thing).Take(num))
                {
                    thing.TakeDamage(new DamageInfo(damDef, damAmount * baseDamageFactor, instigator: attacker, weapon: comp.parent.def));
                }
            }
            base.DoEffect(attackSource, damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Slam : WeaponEffect
    {
        public DamageDef damageDef;
        public IntRange amountOfEnemies;
        public float maxDistance;
        public float knockbackDistance;
        public float knockbackDistanceSecondaryTargets;
        public float baseDamageFactor = 1f;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.Thing != null && attacker != null)
            {
                var num = amountOfEnemies.RandomInRange;
                var damDef = damageDef != null ? damageDef : damageInfo.Def;
                var damAmount = baseDamageValue.HasValue ? baseDamageValue.Value : damageInfo.Amount;
                if (target.Thing is Pawn victim)
                {
                    if (victim.Corpse != null)
                    {
                        TryToKnockBack(attacker, victim.Corpse, knockbackDistance);
                    }
                    else
                    {
                        TryToKnockBack(attacker, victim, knockbackDistance);
                    }
                }
                else
                {
                    target.Thing.TakeDamage(new DamageInfo(damDef, damAmount, instigator: attacker, weapon: comp.parent.def));
                }

                foreach (var thing in GenRadial.RadialDistinctThingsAround(attackSource.PositionHeld, comp.parent.MapHeld, maxDistance, true)
                    .OfType<Pawn>().Where(x => x.Faction == target.Thing.Faction && x != target.Thing).Take(num))
                {
                    TryToKnockBack(attacker, thing, knockbackDistanceSecondaryTargets);
                    thing.TakeDamage(new DamageInfo(damDef, damAmount * baseDamageFactor, instigator: attacker, weapon: comp.parent.def));
                }
            }
            base.DoEffect(attackSource, damageInfo, comp, attacker, target);
        }

        private void TryToKnockBack(Thing attacker, Thing thing, float knockBackDistance)
        {
            float distanceDiff = attacker.Position.DistanceTo(thing.Position) < knockBackDistance ? attacker.Position.DistanceTo(thing.Position) : knockBackDistance;
            Predicate <IntVec3> validator = delegate (IntVec3 x)
            {
                if (x.DistanceTo(thing.Position) < knockBackDistance)
                {
                    return false;
                }
                if (!x.Walkable(thing.Map) || !GenSight.LineOfSight(thing.Position, x, thing.Map))
                {
                    return false;
                }
                var attackerToVictimDistance = attacker.Position.DistanceTo(thing.Position);
                var attackerToCellDistance = attacker.Position.DistanceTo(x);
                var victimToCellDistance = thing.Position.DistanceTo(x);

                if (attackerToVictimDistance > attackerToCellDistance)
                {
                    return false;
                }
                if (attackerToCellDistance > victimToCellDistance + (distanceDiff - 1))
                {
                    return true;
                }
                else if (attacker.Position == thing.Position)
                {
                    return true;
                }
                return false;
            };
            var cells = GenRadial.RadialCellsAround(thing.Position, knockBackDistance + 1, true).Where(x => validator(x));
            if (cells.Any())
            {
                var cell = cells.RandomElement();
                thing.Position = cell;
                if (thing is Pawn victim)
                {
                    victim.pather.StopDead();
                    victim.jobs.StopAll();
                }
            }
        }
    }
    public class WeaponEffect_Multiple : WeaponEffect
    {
        public List<WeaponEffect> weaponEffects;
        public override void DoEffect(Thing attackSource, DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (weaponEffects != null && Rand.Chance(effectChance))
            {
                foreach (var weaponEffect in weaponEffects)
                {
                    weaponEffect.DoEffect(attackSource, damageInfo, comp, attacker, target);
                }
            }
        }
    }
}