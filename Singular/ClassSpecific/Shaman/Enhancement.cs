﻿using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Singular.Lists;
using Styx.WoWInternals.WoWObjects;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.ClassSpecific.Shaman
{
    public class Enhancement
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        #region Common

        [Behavior(BehaviorType.PreCombatBuffs|BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances | WoWContext.Normal)]
        public static Composite CreateShamanEnhancementPreCombatBuffs()
        {
            return new PrioritySelector(

                // cancel form if we get a shapeshift error 
                new Throttle(5,
                    new Decorator(
                        ret => SingularRoutine.IsQuestBotActive && !Me.IsMoving && Me.Shapeshift == ShapeshiftForm.GhostWolf && Common.RecentShapeshiftErrorOccurred,
                        new Action(ret =>
                        {
                            string formName = "Ghost Wolf";
                            Logger.Write("/cancel [{0}] due to shapeshift error in Questing; disabling form for {1:F0} secs while not in combat", formName, (Common.SuppressShapeshiftUntil - DateTime.Now).TotalSeconds);
                            Me.CancelAura(formName);
                        })
                        )
                    ),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                Common.CreateShamanImbueOffHandBehavior( Imbue.Flametongue ),

                Common.CreateShamanDpsShieldBehavior(),

                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs|BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds)]
        public static Composite CreateShamanEnhancementPvpPreCombatBuffs()
        {
            return new PrioritySelector(

                Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                Common.CreateShamanImbueOffHandBehavior(Imbue.Frostbrand, Imbue.Flametongue),

                Common.CreateShamanDpsShieldBehavior(),

                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite CreateShamanEnhancementRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),

                        Common.CreateShamanMovementBuff()
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementHeal()
        {
            return new PrioritySelector(
                Spell.Cast("Healing Surge", on => Me, 
                    ret => Me.GetPredictedHealthPercent(true) < ShamanSettings.MaelHealingSurge && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),

                Common.CreateShamanDpsHealBehavior()
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances)]
        public static Composite CreateShamanEnhancementHealInstances()
        {
            return Common.CreateShamanDpsHealBehavior();
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds )]
        public static Composite CreateShamanEnhancementHealPvp()
        {
            return new PrioritySelector(
                new Decorator(ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5),
                    new PrioritySelector(
                        Spell.Cast("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.GetPredictedHealthPercent() < ShamanSettings.MaelHealingSurge),
                        Spell.Cast("Healing Surge", ret => (WoWPlayer)Unit.GroupMembers.Where(p => p.IsAlive && p.GetPredictedHealthPercent() < ShamanSettings.MaelPvpOffHeal && p.Distance < 40).FirstOrDefault())
                        )
                    ),

                new Decorator(
                    ret => !StyxWoW.Me.Combat || (!Me.IsMoving && !Unit.NearbyUnfriendlyUnits.Any()),
                    Common.CreateShamanDpsHealBehavior( )
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementNormalPull()
        {
            return new PrioritySelector(
                new Decorator(req => Me.Level < 20, Helpers.Common.EnsureReadyToAttackFromLongRange()),
                new Decorator(req => Me.Level >= 20, Helpers.Common.EnsureReadyToAttackFromMelee()),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                        Common.CreateShamanImbueOffHandBehavior(Imbue.Flametongue),

                        Common.CreateShamanDpsShieldBehavior(),

                        new Decorator(
                            ret => StyxWoW.Me.Level < 20,
                            Spell.Cast("Lightning Bolt")
                            ),

                        Helpers.Common.CreateAutoAttack(true),
                        Totems.CreateTotemsBehavior(),
                        Spell.Cast("Lightning Bolt", ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                        Spell.Cast("Unleash Weapon", 
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null 
                                && StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == 5),
                        Spell.Cast("Earth Shock"),

                        Spell.Cast("Lightning Bolt", ret => Me.CurrentTarget.IsFlying || !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, Me.CurrentTarget.Location))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                        Common.CreateShamanImbueOffHandBehavior(Imbue.Flametongue),

                        Common.CreateShamanDpsShieldBehavior(),
                        // Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                        Spell.BuffSelf("Feral Spirit", ret => 
                            ShamanSettings.FeralSpiritCastOn == CastOn.All 
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Bosses && StyxWoW.Me.CurrentTarget.Elite)
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Players && Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.Combat && u.IsTargetingMeOrPet))),

                        Spell.Cast("Elemental Blast"),
                        Spell.Cast("Unleash Elements", ret => Common.HasTalent(ShamanTalents.UnleashedFury)),

                        Spell.Cast("Stormstrike"),
                        Spell.Buff("Flame Shock", true,
                            ret => (StyxWoW.Me.HasAura("Unleash Flame") || !SpellManager.HasSpell("Unleash Elements")) &&
                                   (StyxWoW.Me.CurrentTarget.Elite || (SpellManager.HasSpell("Fire Nova") && Unit.UnfriendlyUnitsNearTarget(10).Count(u => u.IsTargetingMeOrPet) >= 3))),
                        Spell.Cast("Earth Shock",
                            ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3 
                                || !StyxWoW.Me.CurrentTarget.Elite
                                || !SpellManager.HasSpell("Flame Shock")),
                        Spell.Cast("Lava Lash",
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null &&
                                   StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                        Spell.Buff("Fire Nova",
                            on => StyxWoW.Me.CurrentTarget,
                            ret => StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock") &&
                                   Unit.NearbyUnfriendlyUnits.Count(u => 
                                       u.IsTargetingMeOrPet &&
                                       u.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) < 10 * 10) >= 3),
                        Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                        Spell.Cast("Unleash Elements"),

                        new Decorator(ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5) && (StyxWoW.Me.GetAuraTimeLeft("Maelstom Weapon", true).TotalSeconds < 3000 || StyxWoW.Me.GetPredictedHealthPercent(true) > 90),
                            new PrioritySelector(
                                Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                                Spell.Cast("Lightning Bolt")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds)]
        public static Composite CreateShamanEnhancementPvPPullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                        Common.CreateShamanImbueOffHandBehavior(Imbue.Frostbrand, Imbue.Flametongue),

                        Common.CreateShamanDpsShieldBehavior(),

                        // Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                        Spell.BuffSelf("Feral Spirit", ret =>
                            ShamanSettings.FeralSpiritCastOn == CastOn.All
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Bosses && StyxWoW.Me.CurrentTarget.Elite)
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Players && Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.Combat && u.IsTargetingMeOrPet))),

                        Spell.Cast("Elemental Blast"),
                        Spell.Cast("Unleash Elements", ret => Common.HasTalent(ShamanTalents.UnleashedFury)),

                        Spell.Cast("Stormstrike"),
                        Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                        Spell.Cast("Lava Lash", 
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                                   StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),

                        new Decorator(ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5) && (StyxWoW.Me.GetAuraTimeLeft("Maelstom Weapon", true).TotalSeconds < 3000 || StyxWoW.Me.GetPredictedHealthPercent() > 90),
                            new PrioritySelector(
                                Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                                Spell.Cast("Lightning Bolt")
                                )
                            ),

                        Spell.Cast("Unleash Elements"),
                        Spell.Buff("Flame Shock", true, ret => StyxWoW.Me.HasAura("Unleash Wind") || !SpellManager.HasSpell("Unleash Elements")),
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 6)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances)]
        public static Composite CreateShamanEnhancementInstancePullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateEnhanceDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CreateShamanDpsShieldBehavior(),

                        // Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                        Spell.BuffSelf("Feral Spirit", ret =>
                            ShamanSettings.FeralSpiritCastOn == CastOn.All
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Bosses && StyxWoW.Me.CurrentTarget.IsBoss() )
                            || (ShamanSettings.FeralSpiritCastOn == CastOn.Players && Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.Combat && u.IsTargetingMeOrPet))),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled()),
                            new PrioritySelector(
                                Spell.Cast("Unleash Elements"),
                                Spell.Buff("Flame Shock", true),
                                Spell.Cast("Lava Lash", 
                                    ret => StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock") &&
                                           StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                                           StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                                Spell.Cast("Fire Nova"),
                                Spell.Cast("Chain Lightning", ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                                Spell.Cast("Stormstrike"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )),

                        Spell.Cast("Elemental Blast"),
                        Spell.Cast("Unleash Elements", ret => Common.HasTalent(ShamanTalents.UnleashedFury)),

                        Spell.Cast("Stormstrike"),
                        Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                        Spell.Cast("Lava Lash",
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                                   StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                        Spell.Cast("Lightning Bolt", ret => !ShamanSettings.AvoidMaelstromDamage && StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                        Spell.Cast("Unleash Elements"),
                        Spell.Buff("Flame Shock", true, ret => StyxWoW.Me.HasAura("Unleash Wind") || !SpellManager.HasSpell("Unleash Elements")),
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 6)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateEnhanceDiagnosticOutputBehavior()
        {
            return new ThrottlePasses(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        uint maelStacks = 0;
                        WoWAura aura = Me.ActiveAuras.Where( a => a.Key == "Maelstrom Weapon").Select( d => d.Value ).FirstOrDefault();
                        if (aura != null)
                        {
                            maelStacks = aura.StackCount;
                            if (maelStacks == 0)
                                Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  Maelstrom Weapon buff exists with 0 stacks !!!!");
                            else if ( !Me.HasAura("Maelstrom Weapon", (int)maelStacks))
                                Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  Me.HasAura('Maelstrom Weapon', {0}) was False!!!!", maelStacks );
                        }

                        string line = string.Format(".... h={0:F1}%/m={1:F1}%, maelstrom={2}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            maelStacks
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, tmelee={3}, tloss={4}", 
                                target.SafeName(), 
                                target.Distance, 
                                target.HealthPercent,
                                target.IsWithinMeleeRange, 
                                target.InLineOfSpellSight );

                        Logger.WriteDebug(line);
                        return RunStatus.Failure;
                    }))
                );
        }

        #endregion
    }
}
