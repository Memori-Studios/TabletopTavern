using System.Collections.Generic;
using NUnit.Framework.Internal;
using UnityEngine;

namespace TJ
{
//DifficultyMod use this to find them all in the project
public enum TT_Difficulty {
    // The old ten-tier ladder. Saves still hold these; DifficultyRules maps them onto the four levels.
    Peasant = 1,
    Squire = 2,
    Knight = 3,
    Baron = 4,
    Duke = 5,
    King = 6,
    Emperor = 7,
    Imperator = 8,
    Overlord = 9,
    Godking = 10,
    // The four levels are Easy, Medium, Hard, then Godking above them.
    Easy = 11,
    Medium = 12,
    Hard = 13,
}
public struct DifficultyLevel
{
    public TT_Difficulty difficulty;
    public string difficultyName;
    // DifficultyMod ids this level adds. DifficultyRules gates each modifier on the level that lists it.
    public int[] modifiers;
    // Index into the difficultyCrests arrays on PlayPanel and GameOverPanel.
    public int crestIndex;
}
public static class DifficultyData
{
    public static string ModifierKey(int modifier) => "difficultyModifier" + modifier;

    // Saves can hold a ten-tier value, so the lookup goes through the level it stands for today.
    public static DifficultyLevel GetDifficultyLevelData(TT_Difficulty difficulty)
    {
        switch (DifficultyRules.Normalize(difficulty))
        {
            case TT_Difficulty.Easy:
                return Easy;
            case TT_Difficulty.Medium:
                return Medium;
            case TT_Difficulty.Hard:
                return Hard;
            case TT_Difficulty.Godking:
                return Godking;
            default:
                Debug.LogError($"Difficulty level {difficulty} not found. Defaulting to Easy.");
                return Easy;
        }
    }
    // Text keys of every modifier added by the levels below this one.
    public static List<string> GetAllDifficultyModifiersBeforeLevel(TT_Difficulty difficulty)
    {
        return GetModifierKeysBelowRank(DifficultyRules.Rank(difficulty));
    }
    // Text keys of every modifier active at this level, its own included.
    public static List<string> GetAllDifficultyModifiersUpToLevel(TT_Difficulty difficulty)
    {
        return GetModifierKeysBelowRank(DifficultyRules.Rank(difficulty) + 1);
    }
    private static List<string> GetModifierKeysBelowRank(int rank)
    {
        List<string> modifiers = new List<string>();
        for (int i = 0; i < rank && i < DifficultyRules.Ladder.Length; i++)
        {
            foreach (int modifier in GetDifficultyLevelData(DifficultyRules.Ladder[i]).modifiers)
                modifiers.Add(ModifierKey(modifier));
        }
        return modifiers;
    }

    //3 "Auto-resolve health lost preview disabled"
    //6 "The Final Battle of each Act is more difficult."
    //7 "Stronger enemy armies"
    //8 "Removes the ability to modify rolls in events."
    //9 "Increases the cost to recruit from towns"
    //10 "Enemy armies may contain prestiged units."
    //11 "Reduced heal on entering cities"
    //14 "Increases the chance and severity of enemy unit prestige."
    //16 "Cities have stronger garrisons."
    //18 "Start each run with a weakened army."
    //19 "Enemy armies scale in strength faster."
    //20 "Auto-resolve disabled"
    // Removed with the ten-tier ladder: 4 shop prices, 5 gear chest price, 12 gold per turn, 13 sell gold,
    // 15 ransom, 17 reserve healing.

    public static DifficultyLevel Easy = new ()
    {
        difficulty = TT_Difficulty.Easy,
        difficultyName = "difficultyName11",
        modifiers = new int[0],
        crestIndex = 10,
    };
    public static DifficultyLevel Medium = new ()
    {
        difficulty = TT_Difficulty.Medium,
        difficultyName = "difficultyName12",
        modifiers = new int[] { 3, 6, 9, 10 },
        crestIndex = 11,
    };
    public static DifficultyLevel Hard = new ()
    {
        difficulty = TT_Difficulty.Hard,
        difficultyName = "difficultyName13",
        modifiers = new int[] { 7, 8, 11, 16 },
        crestIndex = 12,
    };
    public static DifficultyLevel Godking = new ()
    {
        difficulty = TT_Difficulty.Godking,
        difficultyName = "difficultyName10",
        modifiers = new int[] { 14, 18, 19, 20 },
        crestIndex = 9,
    };
}
}
