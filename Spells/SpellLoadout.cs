using System.Collections.Generic;
using Memori.SaveData;

namespace TJ.Spells
{
    /// <summary>
    /// Which spells a player may take into a run, and what they start with.
    ///
    /// The unlocked pool is <b>derived, never stored</b>: a hero's signature spell joins the
    /// shared pool once that hero has completed a run, and
    /// PlayerSaveData.HeroDifficultiesCompleted already records exactly that. Same reasoning as
    /// CampaignSaveManager.TryGetNextPendingPrestigeTraitChoice - a derived list survives
    /// save/quit for free and cannot drift out of sync with the thing it is derived from.
    ///
    /// Slot 0 is always the active hero's own signature spell and cannot be swapped out.
    /// </summary>
    public static class SpellLoadout
    {
        /// <summary>
        /// The size of a loadout ARRAY, and of the battle hotbar. Always 4, regardless of how many
        /// slots the player has actually unlocked - locked slots are Spell.None entries rather than a
        /// shorter array. Keeping this fixed is what lets CampaignSaveData.selectedSpells stay
        /// save-compatible, and lets the hotbar keep four buttons and four hotkeys.
        /// </summary>
        public const int SlotCount = 4;
        /// <summary>Slots available before any Renown upgrade: the signature plus one free pick.</summary>
        public const int BaseSlotCount = 2;
        public const int SignatureSlotIndex = 0;

        /// <summary>
        /// Available from a fresh save, so slots 1-3 are never dead on a first run. No hero's signature
        /// is currently in this list, but GetSelectableSpells still skips the active signature by value
        /// rather than assuming no overlap - LesserMoraleSpell was Edric's signature until Rally the
        /// Banners was authored, and nothing stops a future hero from claiming one of these again.
        /// </summary>
        public static readonly Spell[] AlwaysAvailableSpells =
        {
            Spell.LesserMoraleSpell,
            Spell.LesserDamageSpell,
            Spell.LesserWindSpell,
            Spell.LesserWeaponStrengthSpell,
        };

        #region Renown upgrades
        public const string ProgressionResourcePath = "SpellData/SpellProgression";

        private static SpellProgressionSO _progression;
        private static bool _progressionLoaded;

        /// <summary>
        /// Missing asset is not an error: it means no spell upgrades have been authored yet, which is
        /// a valid shipping state (base slots, base mana). Cached so a null result is not retried on
        /// every UI repaint.
        /// </summary>
        private static SpellProgressionSO Progression
        {
            get
            {
                if (_progressionLoaded) return _progression;

                _progressionLoaded = true;
                _progression = UnityEngine.Resources.Load<SpellProgressionSO>(ProgressionResourcePath);
                return _progression;
            }
        }

        private static int CountUnlockedNodes(Memori.Metaprogression.MetaprogressionModel[] nodes)
        {
            if (nodes == null) return 0;

            int unlocked = 0;
            foreach (Memori.Metaprogression.MetaprogressionModel node in nodes)
            {
                if (node == null) continue;
                if (SaveDataHandler.IsMetaprogressionNodeUnlocked(node)) unlocked++;
            }
            return unlocked;
        }

        /// <summary>
        /// How many of the four slots the player may actually fill: <see cref="BaseSlotCount"/> plus
        /// one per unlocked slot node, capped at <see cref="SlotCount"/>. Everything above this is
        /// rendered locked and held at Spell.None.
        /// </summary>
        public static int GetUnlockedSlotCount()
        {
            SpellProgressionSO progression = Progression;
            if (progression == null) return BaseSlotCount;

            int unlocked = BaseSlotCount + CountUnlockedNodes(progression.SlotUnlockNodes);
            return System.Math.Min(unlocked, SlotCount);
        }

        /// <summary>
        /// Extra mana per battle from Renown, on top of the base pool. See SaveDataHandler.GetSpellManaPool.
        ///
        /// Each node contributes its own NodeValue, matching how the interest node is read and how the
        /// two Starting Gold nodes share one localized string and differ only by value - UpgradesPanel
        /// appends NodeValue to the node's tooltip whenever it is non-zero. A node left at 0 would
        /// therefore both read wrong and do nothing, so it falls back to the default and says so.
        /// </summary>
        public static int GetManaBonus()
        {
            SpellProgressionSO progression = Progression;
            if (progression == null || progression.ManaUpgradeNodes == null) return 0;

            int bonus = 0;
            foreach (Memori.Metaprogression.MetaprogressionModel node in progression.ManaUpgradeNodes)
            {
                if (node == null) continue;
                if (!SaveDataHandler.IsMetaprogressionNodeUnlocked(node)) continue;

                if (node.NodeValue > 0)
                {
                    bonus += node.NodeValue;
                    continue;
                }

                UnityEngine.Debug.LogWarning($"[SpellLoadout] Mana node '{node.name}' has NodeValue 0, so its " +
                    $"tooltip will omit the amount. Falling back to {TabletopTavernConstants.SPELL_MANA_POOL_PER_UPGRADE}.", node);
                bonus += TabletopTavernConstants.SPELL_MANA_POOL_PER_UPGRADE;
            }
            return bonus;
        }

        /// <summary>True if this slot index is beyond what the player has unlocked.</summary>
        public static bool IsSlotLocked(int slotIndex) => slotIndex >= GetUnlockedSlotCount();
        #endregion

        public static Spell GetSignatureSpell(int heroID) => HeroData.GetHeroByID(heroID).SignatureSpell;

        public static bool IsAlwaysAvailable(Spell spell) => System.Array.IndexOf(AlwaysAvailableSpells, spell) >= 0;

        /// <summary>
        /// The hero whose signature spell this is. False for the always-available Lesser spells and
        /// for any registered asset no hero claims.
        /// </summary>
        public static bool TryGetSignatureHero(Spell spell, out Hero owner)
        {
            if (spell != Spell.None)
            {
                foreach (Hero hero in HeroData.Heroes)
                {
                    if (hero.SignatureSpell != spell) continue;

                    owner = hero;
                    return true;
                }
            }

            owner = default;
            return false;
        }

        /// <summary>
        /// A spell is unlocked once its owning hero has completed a run on ANY difficulty. The Lesser
        /// spells are always available so a fresh save is never stuck with dead slots.
        ///
        /// This used to require Godking specifically. That was 16 heroes beaten on the hardest
        /// difficulty before the pool opened at all, so in practice most players would only ever see
        /// the four Lesser spells. RecordGameOver populates HeroDifficultiesCompleted for every
        /// difficulty, not just Godking, so a non-empty list is exactly "this hero has finished a run".
        /// </summary>
        public static bool IsUnlocked(Spell spell)
        {
            if (IsAlwaysAvailable(spell)) return true;
            if (!TryGetSignatureHero(spell, out Hero owner)) return false;

            return SaveDataHandler.GetHeroDifficultiesCompleted(owner.HeroID).Count > 0;
        }

        /// <summary>
        /// Every spell the grimoire lists, locked or not, in registry order.
        ///
        /// This deliberately INCLUDES the active hero's own signature. The grimoire is a complete map
        /// of the spells that exist, grouped by faction, and the signature reads there as equipped in
        /// slot 1 - leaving it out punched a hole in its faction band, and for Edric (whose signature
        /// is the Lesser Morale Spell) shrank the Common band to three tiles. Excluding it belongs in
        /// <see cref="GetSelectableSpells"/>, which is the list of things that may be PICKED.
        ///
        /// Filtered to spells that are actually obtainable: a registered asset that is neither
        /// always-available nor any hero's signature would otherwise render as a permanently locked
        /// row no player could ever unlock. Every registered asset is claimed today, but the guard
        /// stays: a stub added to the registry ahead of the hero that will claim it is the normal
        /// authoring order, and that is exactly how Rally the Banners sat for a while.
        /// </summary>
        public static List<Spell> GetGrimoireSpells()
        {
            List<Spell> spells = new();
            foreach (SpellData spellData in SpellRegistry.All)
            {
                Spell spell = spellData.Spell;
                if (!IsAlwaysAvailable(spell) && !TryGetSignatureHero(spell, out _)) continue;

                spells.Add(spell);
            }
            return spells;
        }

        /// <summary>
        /// Every spell the player may actually place in slots 1-3, in registry order. The active
        /// hero's own signature is excluded here because it already occupies slot 0 and may not be
        /// duplicated - skipped by value rather than merely left out of a hero loop, so that a
        /// signature which is also an always-available spell cannot slip back into slots 1-3.
        /// </summary>
        public static List<Spell> GetSelectableSpells(int activeHeroID)
        {
            Spell activeSignature = GetSignatureSpell(activeHeroID);

            List<Spell> selectable = new();
            foreach (Spell spell in GetGrimoireSpells())
            {
                if (spell == activeSignature) continue;
                if (IsUnlocked(spell)) selectable.Add(spell);
            }
            return selectable;
        }

        /// <summary>
        /// Signature in slot 0, then the first always-available spells that are registered and not
        /// already taken. Used for a hero with no saved loadout.
        /// </summary>
        public static Spell[] GetDefaultLoadout(int heroID)
        {
            Spell[] loadout = new Spell[SlotCount];
            loadout[SignatureSlotIndex] = GetSignatureSpell(heroID);

            int unlockedSlots = GetUnlockedSlotCount();
            int slot = SignatureSlotIndex + 1;
            foreach (Spell spell in AlwaysAvailableSpells)
            {
                // Stops at the unlocked count, not the array length, so locked slots stay Spell.None.
                if (slot >= unlockedSlots) break;
                if (spell == loadout[SignatureSlotIndex]) continue;
                if (!SpellRegistry.Exists(spell)) continue;

                loadout[slot] = spell;
                slot++;
            }
            return loadout;
        }

        /// <summary>
        /// Forces a loadout to be legal: slot 0 is the hero's signature, no duplicates, nothing
        /// unregistered or not yet unlocked, and every empty slot back-filled from the defaults.
        /// Run this on anything read from a save, and before writing one - a save can predate a
        /// hero's spell changing, and a mod can remove a spell entirely.
        ///
        /// <b>Every UNLOCKED slot always holds a spell.</b> Slots can be replaced but never emptied,
        /// so back-filling here is what guarantees an unlocked slot is never dead however the loadout
        /// was produced - an old save, a mod, or a hero whose signature changed. Locked slots are the
        /// opposite: they are held at Spell.None and never filled, and the array stays four long
        /// either way so existing saves keep loading.
        /// </summary>
        public static Spell[] Sanitize(Spell[] chosen, int heroID)
        {
            Spell[] sanitized = new Spell[SlotCount];
            sanitized[SignatureSlotIndex] = GetSignatureSpell(heroID);

            List<Spell> selectable = GetSelectableSpells(heroID);
            int unlockedSlots = GetUnlockedSlotCount();

            for (int i = 0; i < SlotCount; i++)
            {
                if (i == SignatureSlotIndex) continue;
                // A save written while more slots were unlocked keeps its extra spells on disk, but
                // they are dropped here rather than carried into a run the player cannot cast from.
                // Nothing re-locks slots today, so this only matters for a reset metaprogression.
                if (i >= unlockedSlots) continue;
                if (chosen == null || i >= chosen.Length) continue;

                Spell candidate = chosen[i];
                if (candidate == Spell.None) continue;
                if (!selectable.Contains(candidate)) continue;
                if (System.Array.IndexOf(sanitized, candidate) >= 0) continue;

                sanitized[i] = candidate;
            }

            BackFillEmptySlots(sanitized, heroID, unlockedSlots);
            return sanitized;
        }

        private static void BackFillEmptySlots(Spell[] loadout, int heroID, int unlockedSlots)
        {
            Spell[] defaults = GetDefaultLoadout(heroID);

            for (int i = 0; i < loadout.Length; i++)
            {
                // Locked slots are meant to be empty, so they are never back-filled.
                if (i >= unlockedSlots) continue;
                if (loadout[i] != Spell.None) continue;

                foreach (Spell fallback in defaults)
                {
                    if (fallback == Spell.None) continue;
                    if (System.Array.IndexOf(loadout, fallback) >= 0) continue;

                    loadout[i] = fallback;
                    break;
                }
            }
        }
    }
}
