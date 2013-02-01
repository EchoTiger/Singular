﻿using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using System.Drawing;


namespace Singular.ClassSpecific.Warlock
{
    public class Affliction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                // hawker added a mount check here 23 Jan 2013
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior("Pull"),
                        CreateApplyDotsBehavior(onUnit => Me.CurrentTarget, ret => true)
                        )
                    ),

                Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => 35f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),

                // cancel an early drain soul if done to proc 1 soulshard
                new Decorator(
                    ret => Me.GotTarget && Me.ChanneledSpell != null,
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Drain Soul"
                                && Me.CurrentSoulShards > 0
                                && Me.CurrentTarget.HealthPercent > 20 && SpellManager.HasSpell("Malefic Grasp"),
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Drain Soul on {0} now we have {1} shard", Me.CurrentTarget.SafeName(), Me.CurrentSoulShards)),
                                new Action(ret => SpellManager.StopCasting()),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed())
                                )
                            ),

                            

                        // cancel malefic grasp if target health < 20% and cast drain soul (revisit and add check for minimum # of dots)
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Malefic Grasp"
                                && Me.CurrentSoulShards < Me.MaxSoulShards
                                && Me.CurrentTarget.HealthPercent <= 20,
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Malefic Grasp on {0} @ {1:F1}%", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HealthPercent)),
                                new Action(ret => SpellManager.StopCasting()),
                // Helpers.Common.CreateWaitForLagDuration( ret => Me.ChanneledSpell == null ),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed()),
                                Spell.Cast("Drain Soul", ret => Me.CurrentTarget.HasAnyAura("Agony", "Corruption", "Haunt", "Unstable Affliction"))
                                )
                            )
                        )
                    ),

                Spell.WaitForCastOrChannel(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior("Combat"),

                        CreateAoeBehavior(),

                        // following Drain Soul only while Solo combat to maximize Soul Shard generation
                        Spell.Cast("Drain Soul",
                            ret => !Me.IsInGroup()
                                && Me.CurrentTarget.HealthPercent < 5
                                && !Me.CurrentTarget.IsPlayer
                                && !Me.CurrentTarget.Elite
                                && Me.CurrentSoulShards < 1),

                        CreateApplyDotsBehavior(
                            ret => Me.CurrentTarget,
                            ret => Me.CurrentTarget.HealthPercent < 20 || Me.CurrentTarget.HasAnyAura("Agony", "Corruption", "Unstable Affliction")),

                        Common.CreateCastSoulburn(ctx => Me.HealthPercent < 75),

                            // HAwker added Soulburn Jan 22 2013
                        Spell.Cast("Drain Life", ret => (Me.HealthPercent < 95 && Me.HasAura("Soulburn")) || Me.HealthPercent < 40 && !Group.AnyHealerNearby),
                        Spell.Cast("Malefic Grasp", ret => Me.CurrentTarget.HealthPercent >= 20),
                        Spell.Cast("Shadow Bolt", ret => !SpellManager.HasSpell("Malefic Grasp")),
                        Spell.Cast("Drain Soul"),

                        Spell.Cast("Fel Flame", ret => Me.IsMoving),

                        // only a lowbie should hit this
                        Spell.Cast("Drain Life", ret => !SpellManager.HasSpell("Malefic Grasp"))
                        )
                    ),

                Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => 35f)
                );

        }

        public static Composite CreateAoeBehavior()
        {
            return new Decorator(
                ret => Spell.UseAOE,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount >= 4 && SpellManager.HasSpell("Seed of Corruption"),
                        new PrioritySelector(
                            ctx => Common.TargetsInCombat.FirstOrDefault(m => !m.HasAura("Seed of Corruption")),
                            Spell.BuffSelf("Soulburn", ret => ret != null),
                            Spell.Cast("Seed of Corruption", ret => (WoWUnit)ret)
                            )
                        ),
                    new Decorator(
                        ret => _mobCount >= 2,
                        new PrioritySelector(
                            CreateApplyDotsBehavior(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Agony")), soulBurn => true)
                // , CreateApplyDotsBehavior( ctx => TargetsInCombat.FirstOrDefault(m => Common.AuraMissing(m,"Corruption")), soulBurn => true)
                            , CreateApplyDotsBehavior(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Unstable Affliction")), soulBurn => true)
                            )
                        )
                    )
                );
        }

        public static Composite CreateApplyDotsBehavior(UnitSelectionDelegate onUnit, SimpleBooleanDelegate soulBurn)
        {
            return new PrioritySelector(

                   Spell.BuffSelf("Pandemic",
                        ret => PartyBuff.WeHaveBloodlust
                            && onUnit(ret).InLineOfSpellSight
                            && Me.CurrentSoulShards > 0),

                   Common.CreateCastSoulburn(
                        ret => soulBurn(ret)
                            && onUnit != null && onUnit(ret) != null
                            && onUnit(ret).CurrentHealth > 1
                            && SpellManager.HasSpell("Soul Swap")
                            && (Me.HasAura("Pandemic") || onUnit(ret).HasAuraExpired("Agony") || onUnit(ret).HasAuraExpired("Corruption") || onUnit(ret).HasAuraExpired("Unstable Affliction"))
                            && onUnit(ret).InLineOfSpellSight
                            && Me.CurrentSoulShards > 0),

                    CreateCastSoulSwap(onUnit),

                    Spell.Cast("Agony", ctx => onUnit(ctx), ret => onUnit(ret).HasAuraExpired("Agony")),
                    Spell.Cast("Corruption", ctx => onUnit(ctx), ret => onUnit(ret).HasAuraExpired("Corruption")),
                    Common.BuffWithCastTime("Unstable Affliction", ctx => onUnit(ctx), req => onUnit(req).HasAuraExpired("Unstable Affliction")),
                    Common.BuffWithCastTime("Haunt", ctx => onUnit(ctx), req => onUnit(req).HasAuraExpired("Haunt") || Me.CurrentSoulShards == Me.MaxSoulShards)
                    );
        }

        #endregion

        public static Composite CreateCastSoulSwap(UnitSelectionDelegate onUnit)
        {
            return new Throttle(
                new Decorator(
                    ret => Me.HasAura("Soulburn")
                        && onUnit != null && onUnit(ret) != null
                        && onUnit(ret).IsAlive
                        && (onUnit(ret).HasAuraExpired("Agony") || onUnit(ret).HasAuraExpired("Corruption") || onUnit(ret).HasAuraExpired("Unstable Affliction"))
                        && SpellManager.HasSpell("Soul Swap")
                        && onUnit(ret).Distance <= 40
                        && onUnit(ret).InLineOfSpellSight,
                    new Action(ret =>
                    {
                        Logger.Write(string.Format("Casting Soul Swap on {0}", onUnit(ret).SafeName()));
                        SpellManager.Cast("Soul Swap", onUnit(ret));
                    })
                    )
                );
        }

        private WoWUnit GetBestAoeTarget()
        {
            WoWUnit unit = null;

            if (SpellManager.HasSpell("Seed of Corruption"))
                unit = Clusters.GetBestUnitForCluster(Common.TargetsInCombat.Where(m => !m.HasAura("Seed of Corruption")), ClusterType.Radius, 15f);

            if (SpellManager.HasSpell("Agony"))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasMyAura("Agony"));

            return unit;
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string sState = null)
        {
            if (!SingularSettings.Instance.EnableDebugLogging)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, m={2:F1}%, moving={3}, pet={4:F0}% @ {5:F0} yds, soulburn={6}",
                        sState,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.GotAlivePet ? Me.Pet.HealthPercent : 0,
                        Me.GotAlivePet ? Me.Pet.Distance : 0,
                        (long)Me.GetAuraTimeLeft("Soulburn", true).TotalMilliseconds

                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, dies {2}, {3:F1} yds, loss={4}, face={5}, agony={6}, corr={7}, ua={8}, haunt={9}, seed={10}, aoe={11}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.InLineOfSpellSight,
                            Me.IsSafelyFacing(target),
                            (long)target.GetAuraTimeLeft("Agony", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Unstable Affliction", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Haunt", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Seed of Corruption", true).TotalMilliseconds,
                            _mobCount
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}