using System.Collections.Generic;
using UnityEngine;
using Memori.Audio;
using Memori.Localization;

namespace TJ.Spells
{
    // Append only - ordinals are serialized into SpellData .asset files.
    // RallyTheBanners is Edric's signature, held by 'Iron Legion Spell 1 - IL.asset'. It briefly
    // declared LesserMoraleSpell, colliding with the real Lesser Morale Spell asset, which then
    // stood in as Edric's signature until this one was authored as a charge-empowerment aura.
    public enum Spell { None, LesserMoraleSpell, LesserDamageSpell, LesserWindSpell, LesserWeaponStrengthSpell, LightningStrike, NaturesWrath, Heal, Fireball, SkeletalSummon, HuntersMark, Sunder,
        IaijutsuFlash, ArtilleryBombardment, Cyclone, Dread, Smokescreen, Rampage, Shieldwall, Starstep, HealingGrove, SnareTrap, Taunt, VenomousBite, SnareWeb, RallyTheBanners,
        // Cast by a mage UNIT rather than the player. Deliberately absent from SpellRegistry: that
        // registry drives the run-setup grimoire, and GetGrimoireSpells only lists spells that are
        // always-available or some hero's signature, so a unit spell would render as a permanently
        // locked row nobody could ever unlock. A mage reaches its spell through SquadAssets.mageSpell.
        Smite,
        // The per-faction mage spells. All follow the same "keep it out of SpellRegistry" rule as
        // Smite above - they are cast by units, not by the player, and reach the unit through
        // SquadAssets.mageSpell.
        Doomspeak, Veilmist, BloodScent, Foxfire, PrimalQuake, BattleBrew, Runeward,
        // Common-pool spells, promoted from mage spells to fill the two holes in the always-available
        // set: it had no heal and no damage-over-time. Separate assets from their mage parents on
        // purpose - a unit spell carries Mana 0 because units do not spend mana, and tuning one would
        // otherwise silently rebalance the other. Unlike the mage spells above, these two DO belong in
        // SpellRegistry and in SpellLoadout.AlwaysAvailableSpells.
        LesserMending, LesserEmbers }
    // World: raycast ground point, stays fixed. Squad: follows the target squad's live
    // position through warmup and damage resolution.
    public enum SpellTargetingType { World, Squad }
    public enum SpellType { AOE, SingleTarget }

    // One stat change carried by a spell. A SpellData can hold a list of these so a single cast can
    // buff/debuff several stats at once - each pair becomes its own BattlefieldBonusEnum.SpellStatBonus
    // applicator in ActiveSpell.CastSpell. Value is float so fractional stats (Armor mitigation, Speed)
    // are expressible, unlike the int SpellModifierValue used by the single-bonus path.
    [System.Serializable]
    public struct SpellBonusStat
    {
        public UnitStat UnitStat;
        public float Value;
    }

    [CreateAssetMenu(fileName = "SpellData", menuName = "GameData/SpellData", order = 1)]
    public class SpellData : ScriptableObject
    {
        public Spell Spell;
        public Race Race;
        public int SpellModifierValue;
        public SpellTargetingType SpellTargetingType;
        public SpellType SpellType;
        public float SpellCooldown;
        // Spent from the per-battle mana budget on cast. Cooldown paces casts within a battle; mana caps how many there are.
        public int SpellManaCost;
        public Sprite SpellSprite;
        public float SpellRadius;
        public float SpellWarmUpDuration;
        public float SpellDuration;
        public float SpellForce;
        public bool IsOneOff;
        // Persistent damage/heal spells (Healing Grove, Venomous Bite): set IsOneOff = false and give a
        // TickInterval so the effect lands once per interval instead of every frame. HealsInsteadOfDamage
        // routes it through DamageType.Healing (heals the TargetTeam) instead of the default Magical damage.
        public bool HealsInsteadOfDamage;
        public float TickInterval;
        // Execute-style strike (Iaijutsu Flash): the hit lands on exactly one unit, the one nearest the
        // strike point, rather than on every unit inside SpellRadius. Author the full per-hit damage in
        // SpellModifierValue; the 0.25 melee modifier still applies like every other spell.
        public bool HitsSingleUnit;
        public SFXCue warmupSound;
        public SFXCue hitSound;
        // The hit cue re-fires this many seconds apart after the first play; 0 plays it once.
        public float HitSoundRepeatInterval;
        // Extra plays after the first; 0 with an interval set means until SpellDuration runs out.
        public int HitSoundRepeatCount;
        public Team TargetTeam;
        // Optional art spawned under the shared AOE Spell instance (SpellManager.aoeSpellPrefab) at cast.
        // A SpellVisualAddon on its root gets its warm-up and cast objects switched on at the right moments.
        public GameObject SpellVisualPrefab;

        [Header("Status Icon")]
        // Shows SpellSprite above the health bar of every squad this spell is in effect on, for as long
        // as it lasts. Only lasting effects have anything to show: tags (Mark, Shieldwall), timed bonuses
        // and zone ticks write the squad's SpellStatusBufferElement; one-off bursts never do.
        public bool ShowsStatusIcon;

        [Header("Battlefield Bonus")]
        public bool GrantsBattlefieldBonus;
        public UnitStat BonusUnitStat;
        public BattlefieldBonusEnum BonusType;
        // Multi-stat buffs/debuffs. When this has any entries it takes precedence: each (stat, value)
        // pair is applied as its own SpellStatBonus applicator and the single BonusUnitStat / BonusType /
        // SpellModifierValue bonus above is skipped. Leave empty to use that single-bonus path, which the
        // morale-rate and other special BonusType spells still need.
        public List<SpellBonusStat> BonusStats;

        [Header("Hunter's Mark")]
        // Marks the targeted enemy squad (author as SpellTargetingType.Squad, TargetTeam.Enemy).
        // HuntersMarkSystem then amplifies all hostile damage to that squad's units for SpellDuration
        // seconds by (1 + SpellModifierValue/100). No SpellEntity is created, so it deals no damage
        // itself. A re-cast on a marked squad refreshes the mark, so SpellDuration may exceed SpellCooldown.
        public bool MarksTarget;

        [Header("Shieldwall")]
        // Braces the targeted friendly squad (author as SpellTargetingType.Squad, TargetTeam.Player).
        // ShieldwallSystem makes its units knockback-immune and halves their speed for SpellDuration.
        // Keep SpellDuration < SpellCooldown so a re-cast never stacks the speed penalty.
        public bool BracesTarget;

        [Header("Snare Trap")]
        // Places a hidden armed trap (author as SpellTargetingType.World). SnareTrapSystem springs a burst
        // of SpellModifierValue damage + SpellForce knockback over SpellRadius when an enemy enters, or the
        // trap quietly expires after SpellDuration seconds if nothing trips it.
        public bool PlacesTrap;

        [Header("Starstep")]
        // Blinks the player's currently-selected squad to the cast point (author as SpellTargetingType.World).
        // Reads the live selection: the player selects a squad, picks the spell, then clicks a destination.
        public bool TeleportsSquad;

        [Header("Race Theming")]
        // Optional. When set, the pre-battle browse menu tints each spell's background with a gradient
        // built from this race's PrimaryColor -> SecondaryColor, to visually group spells by race.
        public RaceData RaceData;

        [Header("Mage Unit Casting")]
        // How a mage UNIT picks a target for this spell. Ignored by hotbar casts, which the player
        // aims. Read by EntityWatcher at squad spawn into MageSquad.TargetPriority, because
        // MageSquadFindTargetSystem cannot read a managed ScriptableObject.
        public MageTargetPriority MageTargetPriority;

        [Header("Summon")]
        public bool SummonsSquad;
        // Spawns a friendly squad at the cast point that lasts until killed and is never written
        // back to the campaign save. Author these as SpellTargetingType.World.
        public UnitName SummonedUnitName;

        /// <summary>The formatted description, keyword tags still raw: draw it through KeywordText.</summary>
        public string GetLocalizedSpellDescription()
        {
            string localizedSpellDescription = LocalizationManager.Instance.GetText(Spell.ToString() + "_Desc");
            if(string.IsNullOrEmpty(localizedSpellDescription)) return Spell.ToString();
            return string.Format(localizedSpellDescription, SpellType, SpellModifierValue, SpellDuration);
        }
    }
}