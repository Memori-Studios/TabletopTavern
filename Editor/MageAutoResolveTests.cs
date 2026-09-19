#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TJ.Engagement
{
    /// <summary>
    /// Assertions over AutoResolveBattleManager.HandleMageCasts - the one-shot pass that resolves every
    /// mage's charges before normal auto-resolve begins.
    ///
    /// Why this exists: that method used to read SpellModifierValue as damage for every mage, which was
    /// correct while Smite was the only mage spell. It is not - the field means healing on a HoT, a
    /// percentage on a mark, and a negative stat delta on a debuff. Read as damage, a brace dealt 900 a
    /// cast and a morale drain dealt 1. Only live battle was correct; auto-resolved engagements were
    /// badly skewed, and nothing surfaced it because auto-resolve produces a number rather than a
    /// visible battle.
    ///
    /// Three bugs were found by running these that inspection had missed: a mage buffing ITSELF, an
    /// accuracy debuff RAISING a melee squad's accuracy off the floor, and stat debuffs dumping every
    /// charge on one squad. The last is only visible with several candidate targets - so when adding a
    /// spell shape, add a multi-target case as well as a single-target one.
    ///
    /// Run from Tabletop Tavern > Test Mage Auto-Resolve, or the button on the
    /// AutoResolveBattleManager inspector.
    /// </summary>
    public static class MageAutoResolveTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // Stand-ins used as ally / target. LevySwordsmen is melee (zero accuracy), PeasantBowmen has
        // accuracy worth debuffing, GoblinRabble is a generic enemy body.
        private const UnitName Ally = UnitName.LevySwordsmen;
        private const UnitName Foe = UnitName.GoblinRabble;
        private const UnitName Archer = UnitName.PeasantBowmen;

        [MenuItem("Tabletop Tavern/Test Mage Auto-Resolve")]
        public static void RunFromMenu()
        {
            string report = RunAll(out int passed, out int failed);
            if (failed > 0) Debug.LogError(report);
            else Debug.Log(report);
        }

        /// <summary>
        /// Runs every assertion and returns a printable report. Must be called in Play Mode:
        /// TabletopTavernData only fills its dictionaries at Awake, so GetSquadStats throws otherwise.
        /// </summary>
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;

            if (!Application.isPlaying)
                return "Mage auto-resolve tests: ENTER PLAY MODE FIRST.\n" +
                       "TabletopTavernData populates its dictionaries at Awake, so GetSquadStats throws in edit mode.";

            var sb = new StringBuilder();
            var t = typeof(AutoResolveBattleManager);
            FieldInfo fPlayer = t.GetField("playerAutoResolveStats", Flags);
            FieldInfo fEnemy = t.GetField("enemyAutoResolveStats", Flags);
            FieldInfo fApplied = t.GetField("_mageAlphaStrikeApplied", Flags);
            FieldInfo fSlain = t.GetField("unitsSlainData", Flags);
            MethodInfo mCasts = t.GetMethod("HandleMageCasts", Flags);
            MethodInfo mModify = t.GetMethod("ModifyDamageDealt", Flags);

            if (fPlayer == null || fEnemy == null || fApplied == null || fSlain == null || mCasts == null || mModify == null)
            {
                failed = 1;
                return "Mage auto-resolve tests: FAILED to reflect AutoResolveBattleManager internals - " +
                       "a field or method was renamed. Update MageAutoResolveTests to match.";
            }

            var host = new GameObject("~MageAutoResolveTests");
            host.hideFlags = HideFlags.HideAndDontSave;
            var arm = host.AddComponent<AutoResolveBattleManager>();

            int p = 0, f = 0;
            void Check(string name, bool ok, string detail)
            {
                if (ok) { p++; sb.AppendLine("  PASS  " + name.PadRight(46) + detail); }
                else { f++; sb.AppendLine("  FAIL  " + name.PadRight(46) + detail); }
            }

            AutoResolveSquad Make(UnitName unitName, int index, int units)
            {
                SquadStats stats = TabletopTavernData.Instance.GetSquadStats(unitName);
                int hpPer = stats.HitPointsPerUnit > 0 ? stats.HitPointsPerUnit : 100;
                return new AutoResolveSquad
                {
                    squadStats = stats,
                    SquadIndex = index,
                    TargetIndex = -1,
                    UniqueID = "test" + index,
                    healthPerKill = hpPer,
                    maxUnits = units,
                    UnitsAlive = units,
                    finalHealth = units * hpPer,
                    armorMitigation = 0f,
                    shieldBlockChance = 0f,
                    damageTakenMultiplier = 1f,
                };
            }

            // The enemy-side CastFrom reads CampaignManager.Instance.CampaignSaveManager.SaveData for its
            // difficulty bonus, which does not exist outside the Map scene. The PLAYER-side call has
            // already completed by the time it throws, so the effects under test are still valid, and the
            // two paths differ only by that bonus multiplier. Touching the property also fabricates a
            // phantom CampaignManager via the Singleton auto-create - harmless here, but it is where any
            // CampaignManager.Start() null-reference in the console comes from.
            void Run(AutoResolveSquad[] players, AutoResolveSquad[] enemies)
            {
                fPlayer.SetValue(arm, players);
                fEnemy.SetValue(arm, enemies);
                fApplied.SetValue(arm, false);
                // Built reflectively so this Editor assembly does not need a Unity.Mathematics
                // reference just to name List<int3>.
                fSlain.SetValue(arm, Activator.CreateInstance(fSlain.FieldType));
                try { mCasts.Invoke(arm, null); }
                catch (Exception e)
                {
                    Exception inner = e.InnerException ?? e;
                    if (!(inner is NullReferenceException))
                        Check("unexpected exception", false, inner.GetType().Name + ": " + inner.Message);
                }
            }

            AutoResolveSquad[] P() => (AutoResolveSquad[])fPlayer.GetValue(arm);
            AutoResolveSquad[] E() => (AutoResolveSquad[])fEnemy.GetValue(arm);
            // Sums the queued damage. unitsSlainData is a List<int3> of
            // (attackerSquadIndex, targetSquadIndex, damage); z is the damage.
            FieldInfo zField = null;
            int SlainTotal()
            {
                var list = fSlain.GetValue(arm) as System.Collections.IEnumerable;
                if (list == null) return 0;
                int total = 0;
                foreach (object entry in list)
                {
                    if (zField == null) zField = entry.GetType().GetField("z");
                    if (zField != null) total += (int)zField.GetValue(entry);
                }
                return total;
            }

            try
            {
                sb.AppendLine("=== HEAL ===");
                {
                    var caster = Make(UnitName.GutrotShaman, 1, 1);
                    var wounded = Make(Ally, 2, 48);
                    // Hurt but still above its rout threshold: HasRouted keys off Leadership, and a routed
                    // squad is deliberately skipped so a heal cannot resurrect it.
                    wounded.UnitsAlive = wounded.maxUnits - 4;
                    wounded.finalHealth = wounded.UnitsAlive * wounded.healthPerKill;
                    int before = wounded.finalHealth;
                    var foe = Make(Foe, 11, 48);
                    Run(new[] { caster, wounded }, new[] { foe });

                    Check("ally healed", P()[1].finalHealth > before, before + " -> " + P()[1].finalHealth);
                    Check("heal capped at max health",
                        P()[1].finalHealth <= P()[1].maxUnits * P()[1].healthPerKill, "");
                    Check("UnitsAlive recomputed", P()[1].UnitsAlive > wounded.UnitsAlive,
                        wounded.UnitsAlive + " -> " + P()[1].UnitsAlive);
                    Check("caster did not heal itself", P()[0].finalHealth == caster.finalHealth, "");
                    Check("no damage queued by a heal", SlainTotal() == 0, "");
                    Check("enemy untouched", E()[0].finalHealth == foe.finalHealth, "");
                }
                {
                    var caster = Make(UnitName.GutrotShaman, 1, 1);
                    var healthy = Make(Ally, 2, 48);
                    Run(new[] { caster, healthy }, new[] { Make(Foe, 11, 48) });
                    Check("no overheal on a healthy army", P()[1].finalHealth == healthy.finalHealth, "");
                }
                {
                    var caster = Make(UnitName.GutrotShaman, 1, 1);
                    var routed = Make(Ally, 2, 48);
                    routed.UnitsAlive = 2;                       // far below the rout threshold
                    routed.finalHealth = routed.UnitsAlive * routed.healthPerKill;
                    Run(new[] { caster, routed }, new[] { Make(Foe, 11, 48) });
                    Check("routed ally not resurrected", P()[1].finalHealth == routed.finalHealth, "");
                }

                sb.AppendLine("=== BRACE ===");
                {
                    var caster = Make(UnitName.Glyphwright, 1, 1);
                    var ally = Make(Ally, 2, 48);
                    Run(new[] { caster, ally }, new[] { Make(Foe, 11, 48) });
                    Check("ally damage taken reduced", P()[1].damageTakenMultiplier < 1f,
                        "x" + P()[1].damageTakenMultiplier.ToString("F2"));
                    Check("clamped, never below 0.25", P()[1].damageTakenMultiplier >= 0.25f, "");
                    Check("caster did not brace itself", P()[0].damageTakenMultiplier == 1f, "");
                    Check("enemy untouched by a brace", E()[0].damageTakenMultiplier == 1f, "");
                    Check("no damage queued by a brace", SlainTotal() == 0, "");
                }

                sb.AppendLine("=== MARK ===");
                {
                    var caster = Make(UnitName.SableConsort, 1, 1);
                    var ally = Make(Ally, 2, 48);
                    Run(new[] { caster, ally }, new[] { Make(Foe, 11, 48) });
                    Check("enemy damage taken raised", E()[0].damageTakenMultiplier > 1f,
                        "x" + E()[0].damageTakenMultiplier.ToString("F2"));
                    Check("ally untouched by a mark", P()[1].damageTakenMultiplier == 1f, "");
                    Check("no damage queued by a mark", SlainTotal() == 0, "");
                }

                sb.AppendLine("=== STAT AURAS ===");
                {
                    var caster = Make(UnitName.Cairnwitch, 1, 1);
                    var foe = Make(Foe, 11, 48);
                    float before = foe.squadStats.Leadership;
                    Run(new[] { caster, Make(Ally, 2, 48) }, new[] { foe });
                    Check("enemy Leadership drained", E()[0].squadStats.Leadership < before,
                        before + " -> " + E()[0].squadStats.Leadership);
                    Check("Leadership never negative", E()[0].squadStats.Leadership >= 0f, "");
                    Check("no damage queued by a morale drain", SlainTotal() == 0, "");
                }
                {
                    var caster = Make(UnitName.NytherialSeer, 1, 1);
                    var foe = Make(Archer, 11, 48);
                    float before = foe.squadStats.attackAccuracy;
                    Run(new[] { caster, Make(Ally, 2, 48) }, new[] { foe });
                    Check("enemy accuracy reduced", E()[0].squadStats.attackAccuracy < before,
                        before + " -> " + E()[0].squadStats.attackAccuracy);
                    Check("accuracy floored at 0, never raised", E()[0].squadStats.attackAccuracy >= 0f, "");
                }
                {
                    // A melee squad sits at zero accuracy. A floor of 1 would have the debuff RAISE it.
                    var caster = Make(UnitName.NytherialSeer, 1, 1);
                    var melee = Make(Foe, 11, 48);
                    float before = melee.squadStats.attackAccuracy;
                    Run(new[] { caster, Make(Ally, 2, 48) }, new[] { melee });
                    Check("debuff never raises a zero stat", E()[0].squadStats.attackAccuracy <= before,
                        before + " -> " + E()[0].squadStats.attackAccuracy);
                }

                sb.AppendLine("=== DAMAGE ===");
                foreach (UnitName mage in new[] { UnitName.HexenjagerMage, UnitName.OnmyojiDiviner, UnitName.QuakescaleElder })
                {
                    var caster = Make(mage, 1, 1);
                    var ally = Make(Ally, 2, 48);
                    var foe = Make(Foe, 11, 48);
                    caster.TargetIndex = foe.SquadIndex;
                    Run(new[] { caster, ally }, new[] { foe });
                    Check(mage + " dealt damage", SlainTotal() > 0, "queued " + SlainTotal());
                    Check(mage + " healed nobody", P()[1].finalHealth == ally.finalHealth, "");
                }

                sb.AppendLine("=== SPREAD ACROSS TARGETS ===");
                {
                    // A stat debuff does not change UnitsAlive, so picking purely by size re-picked the
                    // same squad every charge and zeroed one archer outright. Only visible with several
                    // candidates - a single-target test cannot catch it.
                    var caster = Make(UnitName.NytherialSeer, 1, 1);
                    var a = Make(Archer, 11, 48);
                    var b = Make(Archer, 12, 48);
                    var c = Make(Archer, 13, 48);
                    float baseAcc = a.squadStats.attackAccuracy;
                    Run(new[] { caster, Make(Ally, 2, 48) }, new[] { a, b, c });
                    var e = E();
                    Check("debuff spread across all three",
                        e[0].squadStats.attackAccuracy < baseAcc && e[1].squadStats.attackAccuracy < baseAcc &&
                        e[2].squadStats.attackAccuracy < baseAcc,
                        baseAcc + " -> " + e[0].squadStats.attackAccuracy + " / " + e[1].squadStats.attackAccuracy +
                        " / " + e[2].squadStats.attackAccuracy);
                    Check("no squad zeroed by stacking",
                        e[0].squadStats.attackAccuracy > 0f && e[1].squadStats.attackAccuracy > 0f &&
                        e[2].squadStats.attackAccuracy > 0f, "");
                }
                {
                    var caster = Make(UnitName.SableConsort, 1, 1);
                    Run(new[] { caster, Make(Ally, 2, 48) },
                        new[] { Make(Foe, 11, 48), Make(Foe, 12, 48), Make(Foe, 13, 48) });
                    var e = E();
                    Check("mark spread across all three",
                        e[0].damageTakenMultiplier > 1f && e[1].damageTakenMultiplier > 1f &&
                        e[2].damageTakenMultiplier > 1f,
                        "x" + e[0].damageTakenMultiplier.ToString("F2") + " / x" +
                        e[1].damageTakenMultiplier.ToString("F2") + " / x" +
                        e[2].damageTakenMultiplier.ToString("F2"));
                }
                {
                    var caster = Make(UnitName.Glyphwright, 1, 1);
                    Run(new[] { caster, Make(Ally, 2, 48), Make(Ally, 3, 48), Make(Ally, 4, 48) },
                        new[] { Make(Foe, 11, 48) });
                    var pp = P();
                    Check("brace spread across all three allies",
                        pp[1].damageTakenMultiplier < 1f && pp[2].damageTakenMultiplier < 1f &&
                        pp[3].damageTakenMultiplier < 1f,
                        "x" + pp[1].damageTakenMultiplier.ToString("F2") + " / x" +
                        pp[2].damageTakenMultiplier.ToString("F2") + " / x" +
                        pp[3].damageTakenMultiplier.ToString("F2"));
                    Check("caster excluded with several allies", pp[0].damageTakenMultiplier == 1f, "");
                }

                sb.AppendLine("=== NEGATIVE CONTROLS ===");
                {
                    var spent = Make(UnitName.SableConsort, 1, 1);
                    spent.squadStats.Ammunition = 0;
                    Run(new[] { spent, Make(Ally, 2, 48) }, new[] { Make(Foe, 11, 48) });
                    Check("spent mage does nothing",
                        E()[0].damageTakenMultiplier == 1f && SlainTotal() == 0, "");
                }
                {
                    Run(new[] { Make(Ally, 1, 48), Make(Ally, 2, 48) }, new[] { Make(Foe, 11, 48) });
                    Check("non-mage squad casts nothing",
                        SlainTotal() == 0 && E()[0].damageTakenMultiplier == 1f, "");
                }
                {
                    var caster = Make(UnitName.SableConsort, 1, 1);
                    Run(new[] { caster, Make(Ally, 2, 48) }, new[] { Make(Foe, 11, 48) });
                    float once = E()[0].damageTakenMultiplier;
                    try { mCasts.Invoke(arm, null); } catch { /* expected enemy-side throw */ }
                    Check("one-shot guard holds", E()[0].damageTakenMultiplier == once,
                        "x" + once.ToString("F2"));
                }

                sb.AppendLine("=== DAMAGE PIPELINE HONOURS THE MULTIPLIER ===");
                {
                    // Setting damageTakenMultiplier is only half the job - ModifyDamageDealt has to read it.
                    var attacker = Make(Ally, 1, 48);
                    int DamageAgainst(float multiplier)
                    {
                        var defender = Make(Foe, 2, 48);
                        defender.damageTakenMultiplier = multiplier;
                        object[] args = { 1000, defender, attacker.squadStats };
                        mModify.Invoke(arm, args);
                        return (int)args[0];
                    }
                    int normal = DamageAgainst(1f);
                    int marked = DamageAgainst(1.45f);
                    int braced = DamageAgainst(0.70f);
                    Check("mark increases damage taken", marked > normal, normal + " -> " + marked);
                    Check("brace reduces damage taken", braced < normal, normal + " -> " + braced);
                    Check("mark scales correctly", marked == (int)(normal * 1.45f), "");
                    Check("brace scales correctly", braced == (int)(normal * 0.70f), "");
                    Check("damage never floored below 1", DamageAgainst(0.0001f) >= 1, "");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            passed = p;
            failed = f;
            sb.Insert(0, "Mage auto-resolve tests: " + p + " passed, " + f + " failed\n");
            return sb.ToString();
        }
    }
}
#endif
