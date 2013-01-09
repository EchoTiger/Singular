﻿//#define USE_ISFLEEING
#define USE_MECHANIC

using System;
using System.Collections.Generic;
using System.Linq;

using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using System.Drawing;

namespace Singular.ClassSpecific.Shaman
{
    internal static class Totems
    {
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        public static Composite CreateTotemsBehavior()
        {
            return CreateTotemsNormalBehavior();
        }

        public static Composite CreateTotemsNormalBehavior()
        {
            // create Fire Totems behavior first, then wrap if needed
            Composite fireTotemBehavior =
                new PrioritySelector(
                    Spell.BuffSelf("Fire Elemental",
                        ret => Common.StressfulSituation && !SpellManager.CanBuff(WoWTotem.EarthElemental.ToSpellId(), false)),

/*  Magma - handle within AoE DPS logic only
                    Spell.BuffSelf("Magma Totem",
                        ret => Unit.NearbyUnitsInCombatWithMe.Count(u => u.Distance <= GetTotemRange(WoWTotem.Magma)) >= StressMobCount
                            && !Exist( WoWTotem.FireElemental)),
*/    
                    Spell.BuffSelf("Searing Totem",
                        ret => Me.GotTarget 
                            && Me.CurrentTarget.Distance < GetTotemRange(WoWTotem.Searing) - 2f 
                            && !Exist( WoWTotemType.Fire))
                    );

            if ( Me.Specialization == WoWSpec.ShamanRestoration )
                fireTotemBehavior = new Decorator(ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid), fireTotemBehavior);

            // now 
            return new PrioritySelector(

                new Throttle( 1,
                    new Action( r => {
                        bool ccMechanic = Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing | WoWSpellMechanic.Polymorphed | WoWSpellMechanic.Asleep);
                        bool ccEffect = Me.HasAuraWithEffect(WoWApplyAuraType.ModFear | WoWApplyAuraType.ModPacify | WoWApplyAuraType.ModPacifySilence);
                        bool ccAttrib = Me.Fleeing;
                        if (ccMechanic || ccEffect || ccAttrib)
                            Logger.WriteDebug(Color.Pink, "... FEAR CHECKED OUT --  Mechanic={0}  Effect={1}  Attrib={2}", ccMechanic, ccEffect, ccAttrib);
                        return RunStatus.Failure;
                        })
                    ),


                Spell.BuffSelf(WoWTotem.Tremor.ToSpellId(),
                    ret => Unit.GroupMembers.Any(f => f.Fleeing && f.Distance < Totems.GetTotemRange(WoWTotem.Tremor))
                        && !Exist(WoWTotem.StoneBulwark, WoWTotem.EarthElemental)),

                new Decorator(
                    ret => !Me.IsMoving || (Me.GotTarget && Me.CurrentTarget.Distance < (Me.MeleeDistance(Me.CurrentTarget) + 3)),
                    new PrioritySelector(

                        // check for stress - enemy player or elite within 8 levels nearby
                        // .. dont use NearbyUnitsInCombatWithMe since it checks .Tagged and we only care if we are being attacked 
                        ctx => Common.StressfulSituation ,

                        // earth totems
                        Spell.BuffSelf(WoWTotem.EarthElemental.ToSpellId(),
                            ret => ((bool) ret || Group.Tanks.Any( t => t.IsDead && t.Distance < 40)) && !Exist( WoWTotem.StoneBulwark)),

                        Spell.BuffSelf(WoWTotem.StoneBulwark.ToSpellId(),
                            ret => Me.HealthPercent < ShamanSettings.StoneBulwarkTotemPercent && !Exist( WoWTotem.EarthElemental)),

                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.Any(u => u.IsTargetingMeOrPet && u.IsPlayer && u.Combat),

                            Spell.BuffSelf(WoWTotem.Earthgrab.ToSpellId(),
                                ret => (bool)ret && !Exist(WoWTotemType.Earth)),

                            Spell.BuffSelf(WoWTotem.Earthbind.ToSpellId(),
                                ret => (bool)ret && !Exist(WoWTotemType.Earth))
                            ),


                        // fire totems
                        fireTotemBehavior,

                        // water totems
                        Spell.Cast("Mana Tide Totem", ret => ((bool)ret) && StyxWoW.Me.ManaPercent < 80
                            && !Exist( WoWTotem.HealingTide )),

        /* Healing...: handle within Helaing logic
                        Spell.Cast("Healing Tide Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 50
                            && !Exist(WoWTotem.ManaTide)),

                        Spell.Cast("Healing Stream Totem", ret => ((bool)ret) && StyxWoW.Me.HealthPercent < 80
                            && !Exist( WoWTotemType.Water)),
        */

                        // air totems
                        Spell.Cast("Grounding Totem",
                            ret => ((bool) ret)
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 40 && u.IsTargetingMeOrPet && u.IsCasting)
                                && !Exist(WoWTotemType.Air)),

                        Spell.Cast("Capacitor Totem",
                            ret => ((bool) ret)
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < GetTotemRange(WoWTotem.Capacitor))
                                && !Exist(WoWTotemType.Air)),

                        Spell.Cast("Stormlash Totem",
                            ret => ((bool)ret)
                                && PartyBuff.WeHaveBloodlust
                                && !Exist(WoWTotemType.Air)),

                        Spell.BuffSelf("Windwalk Totem", 
                            ret => !Exist(WoWTotemType.Air) 
                                && Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared))
                        )
                    )
                );

        }

        public static Composite CreateTotemsPvPBehavior()
        {
            return CreateTotemsNormalBehavior();
        }

        public static Composite CreateTotemsInstanceBehavior()
        {
            return CreateTotemsNormalBehavior();
        }

        /// <summary>
        ///   Recalls any currently 'out' totems. This will use Totemic Recall if its known, otherwise it will destroy each totem one by one.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        public static Composite CreateRecallTotems()
        {
            return new Decorator(
                ret => Totems.NeedToRecallTotems,
                new Throttle(
                    new Styx.TreeSharp.Action( delegate
                    {
                        Logger.Write("Recalling totems!");
                        if (SpellManager.HasSpell("Totemic Recall"))
                        {
                            SpellManager.Cast("Totemic Recall");
                            return;
                        }

                        List<WoWTotemInfo> totems = StyxWoW.Me.Totems;
                        foreach (WoWTotemInfo t in totems)
                        {
                            if (t != null && t.Unit != null)
                            {
                                DestroyTotem(t.Type);
                            }
                        }
                    })
                    )
                );
        }

        /// <summary>
        ///   Destroys the totem described by type.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "type">The type.</param>
        public static void DestroyTotem(WoWTotemType type)
        {
            if (type == WoWTotemType.None)
            {
                return;
            }

            Lua.DoString("DestroyTotem({0})", (int)type);
        }


        private static readonly Dictionary<WoWTotemType, WoWTotem> LastSetTotems = new Dictionary<WoWTotemType, WoWTotem>();


        #region Helper shit

        public static bool NeedToRecallTotems 
        { 
            get 
            { 
                return TotemsInRange == 0 
                    && StyxWoW.Me.Totems.Count(t => t.Unit != null) != 0
                    && !Unit.NearbyFriendlyPlayers.Any( f => f.Combat )
                    && !StyxWoW.Me.Totems.Any(t => totemsWeDontRecall.Any( twl => twl == t.WoWTotem )); 
            } 
        }

        public static bool TotemIsKnown(WoWTotem totem)
        {
            return SpellManager.HasSpell(totem.ToSpellId());
        }


        #region Totem Existance

        public static bool Exist(WoWTotem ti)
        {
            return ti != WoWTotem.None
                && ti != WoWTotem.DummyAir
                && ti != WoWTotem.DummyEarth
                && ti != WoWTotem.DummyFire
                && ti != WoWTotem.DummyWater;
        }

        public static bool Exist(WoWTotemInfo ti)
        {
            return Exist(ti.WoWTotem);
        }

        public static bool Exist(WoWTotemType type)
        {
            WoWTotem wt = GetTotem(type).WoWTotem;
            return Exist(wt);
        }

        public static bool Exist(params WoWTotem[] wt)
        {
            return wt.Any(t => Exist(t));
        }

        public static bool ExistInRange(WoWPoint pt, WoWTotem tt)
        {
            if ( !Exist(tt))
                return false;

            WoWTotemInfo ti = GetTotem(tt);
            return ti.Unit != null && ti.Unit.Location.Distance(pt) < GetTotemRange(tt);
        }

        public static bool ExistInRange(WoWPoint pt, params WoWTotem[] awt)
        {
            return awt.Any(t => ExistInRange(pt, t));
        }

        public static bool ExistInRange(WoWPoint pt, WoWTotemType type)
        {
            WoWTotemInfo ti = GetTotem(type);
            return Exist(ti) && ti.Unit != null && ti.Unit.Location.Distance(pt) < GetTotemRange(ti.WoWTotem);
        }

        #endregion

        /// <summary>
        /// gets reference to array element in Me.Totems[] corresponding to WoWTotemType of wt.  Return is always non-null and does not indicate totem existance
        /// </summary>
        /// <param name="wt">WoWTotem of slot to reference</param>
        /// <returns>WoWTotemInfo reference</returns>
        public static WoWTotemInfo GetTotem(WoWTotem wt)
        {
            return GetTotem(wt.ToType());
        }

        /// <summary>
        /// gets reference to array element in Me.Totems[] corresponding to type.  Return is always non-null and does not indicate totem existance
        /// </summary>
        /// <param name="type">WoWTotemType of slot to reference</param>
        /// <returns>WoWTotemInfo reference</returns>
        public static WoWTotemInfo GetTotem(WoWTotemType type)
        {
            return Me.Totems[(int)type - 1];
        }

        public static int TotemsInRange 
        { 
            get 
            {
                return TotemsInRangeOf(StyxWoW.Me);
            }
        }

        public static int TotemsInRangeOf(WoWUnit unit)
        {
            return StyxWoW.Me.Totems.Where(t => t.Unit != null).Count(t => unit.Location.Distance(t.Unit.Location) < GetTotemRange(t.WoWTotem));
        }

        /// <summary>
        ///   Finds the max range of a specific totem, where you'll still receive the buff.
        /// </summary>
        /// <remarks>
        ///   Created 3/26/2011.
        /// </remarks>
        /// <param name = "totem">The totem.</param>
        /// <returns>The calculated totem range.</returns>
        public static float GetTotemRange(WoWTotem totem)
        {
            switch (totem)
            {
                case WoWTotem.HealingStream:
                case WoWTotem.Tremor:
                    return 30f;

                case WoWTotem.Searing:
                    if (SpellManager.HasSpell(29000))
                        return 35f;
                    return 20f;

                case WoWTotem.Earthbind:
                    return 10f;

                case WoWTotem.Grounding:
                case WoWTotem.Magma:
                    return 8f;

                case WoWTotem.EarthElemental:
                case WoWTotem.FireElemental:
                    // Not really sure about these 3.
                    return 20f;

                case WoWTotem.ManaTide:
                    // Again... not sure :S
                    return 40f;


                case WoWTotem.Earthgrab:
                    return 10f;

                case WoWTotem.StoneBulwark:
                    // No idea, unlike former glyphed stoneclaw it has a 5 sec pluse shield component so range is more important
                    return 40f;

                case WoWTotem.HealingTide:
                    return 40f;

                case WoWTotem.Capacitor:
                    return 8f;

                case WoWTotem.Stormlash:
                    return 30f;

                case WoWTotem.Windwalk:
                    return 40f;

                case WoWTotem.SpiritLink:
                    return 10f;
            }

            return 0f;
        }


        public static int ToSpellId(this WoWTotem totem)
        {
            return (int)(((long)totem) & ((1 << 32) - 1));
        }

        public static WoWTotemType ToType(this WoWTotem totem)
        {
            return (WoWTotemType)((long)totem >> 32);
        }


        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        static WoWTotem[] totemsWeDontRecall = new WoWTotem[] 
        {
            WoWTotem.FireElemental , 
            WoWTotem.EarthElemental  , 
            WoWTotem.HealingTide , 
            WoWTotem.ManaTide 
        };

        #endregion

    }

}