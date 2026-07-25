using System.Collections.Generic;
using UnityEngine;
using Memori.Audio;
using Memori.Localization;

namespace TJ.Spells
{
    // Append only - ordinals are serialized into SpellData .asset files.
    public enum Spell { None, LesserMoraleSpell, LesserDamageSpell, LesserWindSpell, LesserWeaponStrengthSpell, LightningStrike, NaturesWrath, Heal, Fireball, SkeletalSummon, HuntersMark, Sunder,
        IaijutsuFlash, ArtilleryBombardment, Cyclone, Dread, Smokescreen, Rampage, Shieldwall, Starstep, HealingGrove, SnareTrap, Taunt, VenomousBite, SnareWeb }
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
        public SFXReference warmupSound;
        public SFXReference hitSound;
        public Team TargetTeam;
        public ActiveSpell SpellPrefab;

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
        // itself. Keep SpellDuration < SpellCooldown so a re-cast never double-marks the same squad.
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

        [Header("Summon")]
        public bool SummonsSquad;
        // Spawns a friendly squad at the cast point that lasts until killed and is never written
        // back to the campaign save. Author these as SpellTargetingType.World.
        public UnitName SummonedUnitName;

        public string GetLocalizedSpellDescription()
        {
            string localizedSpellDescription = LocalizationManager.Instance.GetText(Spell.ToString() + "_Desc");
            if(string.IsNullOrEmpty(localizedSpellDescription)) return Spell.ToString();
            localizedSpellDescription = string.Format(localizedSpellDescription, SpellType, SpellModifierValue, SpellDuration);
            ColorData.XMLTagColorApplicator(ref localizedSpellDescription);
            return localizedSpellDescription;
        }
    }
}