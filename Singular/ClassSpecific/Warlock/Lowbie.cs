﻿using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.Lowbie)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public Composite CreateLowbieWarlockCombat()
        {
            WantedPet = "Imp";

            return new PrioritySelector(
                CreateEnsureTarget(),
                CreateMoveToAndFace(35f, ret => Me.CurrentTarget),
                CreateAutoAttack(true),
                CreateWaitForCast(true),
                CreateSpellCast("Life Tap", ret => Me.ManaPercent < 50 && Me.HealthPercent > 70),
                CreateSpellCast("Drain Life", ret => Me.HealthPercent < 70),
                CreateSpellBuff("Immolate"),
                CreateSpellBuff("Corruption"),
                CreateSpellCast("Shadow Bolt")
                );
        }
    }
}