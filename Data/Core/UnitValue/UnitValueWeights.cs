using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    /// <summary>A hand correction on one unit's value, multiplied in after the model.</summary>
    [Serializable] public struct UnitValueNudge
    {
        public UnitName unit;
        public float multiplier;
    }

    /// <summary>A value given outright to a unit the model cannot price.</summary>
    [Serializable] public struct UnitFixedValue
    {
        public UnitName unit;
        public float value;
    }

    /// <summary>Every tunable input of the unit value model. The asset lives at Resources/UnitValueWeights; these defaults apply when it is missing.</summary>
    [CreateAssetMenu(fileName = "UnitValueWeights", menuName = "GameData/UnitValueWeights", order = 2)]
    public class UnitValueWeights : ScriptableObject
    {
        public const string ResourcePath = "UnitValueWeights";

        [Header("Opponent mix")]
        [Tooltip("How much a Common opponent counts when a unit's meetings are averaged. Raise it and traits that beat Commons are worth more.")]
        public float commonOpponents = 1f;
        [Tooltip("How much an Uncommon opponent counts.")]
        public float uncommonOpponents = 1f;
        [Tooltip("How much a Rare opponent counts.")]
        public float rareOpponents = 1f;
        [Tooltip("How much a Legendary opponent counts.")]
        public float legendaryOpponents = 1f;

        [Header("Shape of a fight")]
        [Tooltip("Seconds of free fire a shooter gets behind a friendly line, on top of the time the enemy needs to reach it.")]
        public float shieldedSeconds = 15f;
        [Tooltip("Part of a melee a foot or monstrous unit spends with its charge bonus.")]
        [Range(0f, 1f)] public float chargeShareFoot = 0.08f;
        [Tooltip("Part of a melee a cavalry unit spends with its charge bonus.")]
        [Range(0f, 1f)] public float chargeShareMounted = 0.1f;
        [Tooltip("How often a melee is a flank. Prices Back Stabbers.")]
        [Range(0f, 1f)] public float flankShare = 0.1f;
        [Tooltip("How often the enemy fields a monstrous unit. Prices Monster Slayer against everything that is not a monster.")]
        [Range(0f, 1f)] public float monsterShare = 0.35f;
        [Tooltip("How much of a battle is fought in trees. Prices Forest Dweller.")]
        [Range(0f, 1f)] public float forestShare = 0.15f;
        [Tooltip("Part of a fight a Rage unit spends under half health.")]
        [Range(0f, 1f)] public float rageTime = 0.35f;
        [Tooltip("Part of a fight a Blood Frenzy unit spends over half health.")]
        [Range(0f, 1f)] public float frenzyTime = 0.6f;

        [Header("What the combat rules cannot see")]
        [Tooltip("Bonus for the fastest units, as a share of their value. Speed 30 earns none, speed 80 earns all of it.")]
        public float speedBonus = 0f;
        [Tooltip("Bonus for deploying outside the deployment zone.")]
        public float outriderBonus = 0.03f;
        [Tooltip("Bonus for frightening more than the one squad a meeting counts.")]
        public float terrorCrowdBonus = 0f;

        [Header("Scale")]
        [Tooltip("How far weak units close the gap by ganging up. 1 is the raw exchange; lower squeezes weak and strong together.")]
        [Range(0.3f, 1f)] public float crowding = 0.6f;
        [Tooltip("The power that reads 100. Frozen so a change to one unit moves only that unit; 0 pins 100 to the strongest unit instead. The default is the Oni's power before its 2026-10-06 nerf.")]
        public float referencePower = 8.3266f;

        [Header("Hand corrections")]
        public List<UnitValueNudge> nudges = new();
        [Tooltip("Values for units the model cannot price yet. The mages are here, fitted from simulated armies, until the model can price a spell.")]
        public List<UnitFixedValue> fixedValues = new()
        {
            new() { unit = UnitName.HexenjagerMage, value = 21f },
            new() { unit = UnitName.OnmyojiDiviner, value = 12f },
            new() { unit = UnitName.SableConsort, value = 12f },
            new() { unit = UnitName.NytherialSeer, value = 12f },
            new() { unit = UnitName.Glyphwright, value = 9f },
            new() { unit = UnitName.QuakescaleElder, value = 9f },
            new() { unit = UnitName.GutrotShaman, value = 3f },
            new() { unit = UnitName.Cairnwitch, value = 3f },
        };

        /// <summary>The asset if there is one, otherwise the code defaults.</summary>
        public static UnitValueWeights LoadOrDefault()
        {
            UnitValueWeights asset = Resources.Load<UnitValueWeights>(ResourcePath);
            return asset != null ? asset : CreateInstance<UnitValueWeights>();
        }

        public float OpponentWeight(UnitRarity rarity) => rarity switch
        {
            UnitRarity.Common => commonOpponents,
            UnitRarity.Uncommon => uncommonOpponents,
            UnitRarity.Rare => rareOpponents,
            _ => legendaryOpponents,
        };

        public float Nudge(UnitName unit)
        {
            foreach (UnitValueNudge nudge in nudges)
                if (nudge.unit == unit) return nudge.multiplier;
            return 1f;
        }

        public bool TryGetFixedValue(UnitName unit, out float value)
        {
            foreach (UnitFixedValue entry in fixedValues)
            {
                if (entry.unit != unit) continue;
                value = entry.value;
                return true;
            }
            value = 0f;
            return false;
        }
    }
}
