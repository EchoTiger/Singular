﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonBehaviors.Actions;

using Singular.Settings;

using Styx;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    partial class SingularRoutine
    {

        public Composite CreateUseTrinketsBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.UseFirstTrinket,
                    new Decorator(
                        ret => Miscellaneous.UseTrinket(true),
                        new ActionAlwaysSucceed())),

                new Decorator(
                    ret => SingularSettings.Instance.UseSecondTrinket,
                    new Decorator(
                        ret => Miscellaneous.UseTrinket(false),
                        new ActionAlwaysSucceed()))
                );
        }

        public Composite CreateUseEquippedItem(uint slotId)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Miscellaneous.UseEquippedItem(slotId),
                    new ActionAlwaysSucceed()));
        }

        /// <summary>
        /// Creates a composite to use potions and healthstone.
        /// </summary>
        /// <param name="healthPercent">Healthpercent to use health potions and healthstone</param>
        /// <param name="manaPercent">Manapercent to use mana potions</param>
        /// <returns></returns>
        public Composite CreateUsePotionAndHealthstone(double healthPercent, double manaPercent)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.HealthPercent < healthPercent,
                    new PrioritySelector(
                        ctx => Miscellaneous.FindFirstUsableItemBySpell("Healthstone", "Healing Potion"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))
                        )),
                new Decorator(
                    ret => Me.ManaPercent < manaPercent,
                    new PrioritySelector(
                        ctx => Miscellaneous.FindFirstUsableItemBySpell("Restore Mana"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))))
                );
        }
    }
}
