﻿using System.Collections.Generic;
using System.Linq;

using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.Helpers
{
    public static class Unit
    {
        /// <summary>
        ///   Gets the nearby friendly players within 40 yards.
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static List<WoWPlayer> NearbyFriendlyPlayers
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Where(p => p.DistanceSqr <= 40 * 40 && p.IsFriendly).ToList(); 
            }
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within 40 yards.
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static List<WoWUnit> NearbyUnfriendlyUnits
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(p => p.IsHostile && !p.Dead && !p.IsPet && p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        public static bool HasAura(WoWUnit unit, string aura)
        {
            return HasAura(unit, aura, 0);
        }

        public static bool HasAura(WoWUnit unit, string aura, int stacks)
        {
            Logger.WriteDebug("Looking for aura: " + aura);
            var auras = unit.GetAllAuras();
            foreach(var a in auras)
            {
                Logger.WriteDebug("Aura name: " + a.Name + " - " + a.StackCount);
                if (a.Name == aura)
                    return a.StackCount >= stacks;
            }
            return false;
        }

        public static bool HasAnyAura(WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => hashes.Contains(a.Name));
        }

        public static bool HasAuraWithMechanic(WoWUnit unit, params WoWSpellMechanic[] mechanics)
        {
            var auras = unit.GetAllAuras();
            return auras.Any(a => mechanics.Contains(a.Spell.Mechanic));
        }
    }
}
