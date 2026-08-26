using UnityEngine;
using System.Collections.Generic;

namespace TJ
{
    public enum ConsumableEnum { MinorHealth, MajorHealth, Prestige, Duplicate, NewUnit, Alchemist, Rewind, TrialofGrasses, FateshineElixir, RunewellNectar, LambSauce }
    public enum ConsumableRarity { Common, Uncommon, Rare, Legendary }
    [System.Serializable] public struct Consumable
    {
        public ConsumableEnum ConsumableEnum;
        public ConsumableRarity ConsumableRarity;
    }
    public static class ConsumableData
    {
        private static readonly Dictionary<ConsumableRarity, int> CostOverrides = new();
        private static readonly Dictionary<(int ActNumber, ConsumableEnum Consumable), float> DropChanceOverrides = new();

        public static void ClearEconomyOverrides()
        {
            CostOverrides.Clear();
            DropChanceOverrides.Clear();
        }
        public static void SetCostOverride(ConsumableRarity rarity, int cost) => CostOverrides[rarity] = cost;
        public static void SetDropChanceOverride(int actNumber, ConsumableEnum consumable, float weight) => DropChanceOverrides[(actNumber, consumable)] = weight;

        public static Consumable GetConsumable(ConsumableEnum _consumableType)
        {
            return _consumableType switch
            {
                ConsumableEnum.MinorHealth => MinorHealthPotion,
                ConsumableEnum.MajorHealth => MajorHealthPotion,
                ConsumableEnum.Prestige => PrestigePotion,
                ConsumableEnum.Duplicate => DuplicateUnitPotion,
                ConsumableEnum.NewUnit => UnitInABottle,
                ConsumableEnum.Alchemist => AlchemistPotion,
                ConsumableEnum.Rewind => RewindPotion,
                ConsumableEnum.TrialofGrasses => TrialofGrasses,
                ConsumableEnum.FateshineElixir => FateshineElixir,
                ConsumableEnum.RunewellNectar => RunewellNectar,
                ConsumableEnum.LambSauce => LambSauce,

                _ => new Consumable(),
            };
        }
        public static Consumable MinorHealthPotion = new ()
        {
            ConsumableEnum = ConsumableEnum.MinorHealth,
            // ConsumableDescription = "Heals a unit for 50% of its max health",
            ConsumableRarity = ConsumableRarity.Common,
        };
        public static Consumable AlchemistPotion = new () // cannot appear in shop
        {
            ConsumableEnum = ConsumableEnum.Alchemist,
            // ConsumableDescription = "Instantly gain 5 gold",
            ConsumableRarity = ConsumableRarity.Common,
        };
        public static Consumable RewindPotion = new()
        {
            ConsumableEnum = ConsumableEnum.Rewind,
            // ConsumableDescription = "Rerolls the last dice roll",
            ConsumableRarity = ConsumableRarity.Common,
        };
        public static Consumable MajorHealthPotion = new ()
        {
            ConsumableEnum = ConsumableEnum.MajorHealth,
            // ConsumableDescription = "Heals a unit for 100% of its max health",
            ConsumableRarity = ConsumableRarity.Uncommon,
        };
        public static Consumable FateshineElixir = new ()
        {
            ConsumableEnum = ConsumableEnum.FateshineElixir,
            // ConsumableDescription = "Guarantees the next dice roll to be a 20"
            ConsumableRarity = ConsumableRarity.Uncommon,
        };
        public static Consumable PrestigePotion = new ()
        {
            ConsumableEnum = ConsumableEnum.Prestige,
            // ConsumableDescription = "Increases prestige of a unit by 1",
            ConsumableRarity = ConsumableRarity.Rare,
        };
        public static Consumable UnitInABottle = new ()
        {
            ConsumableEnum = ConsumableEnum.NewUnit,
            // ConsumableDescription = "Summons a random unit",
            ConsumableRarity = ConsumableRarity.Uncommon,
        };
        public static Consumable RunewellNectar = new()
        {
            ConsumableEnum = ConsumableEnum.RunewellNectar,
            // ConsumableDescription = "Fills all your empty potion slots with random potions",
            ConsumableRarity = ConsumableRarity.Rare
        };
        public static Consumable TrialofGrasses = new ()
        {
            ConsumableEnum = ConsumableEnum.TrialofGrasses,
            // ConsumableDescription = "Presitges a max health unit to level 3 with 10% of its max health",
            ConsumableRarity = ConsumableRarity.Rare,
        };
        public static Consumable DuplicateUnitPotion = new ()
        {
            ConsumableEnum = ConsumableEnum.Duplicate,
            // ConsumableDescription = "Creates a prestige 1 copy of a unit",
            ConsumableRarity = ConsumableRarity.Rare,
        };
        public static Consumable LambSauce = new ()
        {
            ConsumableEnum = ConsumableEnum.LambSauce,
            // ConsumableDescription = "Creates a signature unit for your hero",
            ConsumableRarity = ConsumableRarity.Legendary
        };

        public static ConsumableEnum[] GetAllConsumableEnums()
        {
            return new[] {
                ConsumableEnum.MinorHealth,//Common
                ConsumableEnum.Rewind,//Common
                ConsumableEnum.Alchemist,//Common
                ConsumableEnum.MajorHealth,//Uncommon
                ConsumableEnum.FateshineElixir,//Uncommon
                ConsumableEnum.NewUnit,//Uncommon
                ConsumableEnum.RunewellNectar,//Rare
                ConsumableEnum.Prestige,//Rare
                ConsumableEnum.Duplicate,//Rare
                ConsumableEnum.TrialofGrasses,//Rare
                ConsumableEnum.LambSauce //Legendary
            };
        }
        // Consumable draws are seeded on purpose - there is deliberately no UnityEngine.Random variant.
        // Campaign consumable offers use this with a campaign-derived stream
        // (CampaignSaveManager.GetCampaignRandom/GetSeededRandom) so leaving and re-entering a node -
        // exiting to the main menu, for example - regenerates the same offer instead of rerolling it.
        // Same protection gear already gets through CampaignSaveManager.DrawRandomGear.
        public static ConsumableEnum GetRandomConsumable(System.Random random)
        {
            ConsumableEnum[] allConsumables = GetAllConsumableEnums();
            return allConsumables[random.Next(0, allConsumables.Length)];
        }
        // Takes a raw campaign seed (CampaignSaveManager.GetSeededRandom, usually plus a per-item offset)
        // and mixes it, without which adjacent seeds hand back the same consumable.
        public static ConsumableEnum GetWeightedConsumable(int bookNumber, int seed)
        {
            return GetWeightedConsumable(bookNumber, new System.Random(MathUtilities.MixSeed(seed)));
        }
        // Rarity-weighted draw. Seeded for the same reason as GetRandomConsumable(System.Random) above.
        public static ConsumableEnum GetWeightedConsumable(int bookNumber, System.Random random, bool hasLuckyHorseshoe = false)
        {
            ConsumableEnum GetRandomWeightedItem(ConsumableEnum[] items, float[] weights)
            {
                float totalWeight = 0f;
                foreach (float weight in weights)
                    totalWeight += weight;

                float randomValue = (float)(random.NextDouble() * totalWeight);
                float cumulativeWeight = 0f;

                for (int i = 0; i < items.Length; i++)
                {
                    cumulativeWeight += weights[i];
                    if (randomValue <= cumulativeWeight)
                        return items[i];
                }

                return items[items.Length - 1];
            }

            ConsumableEnum[] allConsumables = GetAllConsumableEnums();
            if (hasLuckyHorseshoe)
                allConsumables = System.Array.FindAll(allConsumables, c => GetConsumable(c).ConsumableRarity != ConsumableRarity.Common);
            float[] weights = new float[allConsumables.Length];
            for (int i = 0; i < allConsumables.Length; i++)
                weights[i] = ConsumableDropChance(allConsumables[i], bookNumber);

            return GetRandomWeightedItem(allConsumables, weights);
        }
        public static float ConsumableDropChance(ConsumableEnum _consumable, int actNumber)
        {
            if (DropChanceOverrides.TryGetValue((actNumber, _consumable), out float overrideWeight)) return overrideWeight;
            switch (actNumber)
            {
                case 1: return _consumable switch
                {
                    ConsumableEnum.MinorHealth      => 35f, // Common
                    ConsumableEnum.Rewind           => 20f, // Common
                    ConsumableEnum.Alchemist        => 20f, // Common
                    ConsumableEnum.MajorHealth      => 15f, // Uncommon
                    ConsumableEnum.FateshineElixir  => 12f, // Uncommon
                    ConsumableEnum.NewUnit          => 12f, // Uncommon
                    ConsumableEnum.Prestige         => 8f,  // Rare
                    ConsumableEnum.RunewellNectar   => 6f,  // Rare
                    ConsumableEnum.Duplicate        => 6f,  // Rare
                    ConsumableEnum.TrialofGrasses   => 6f,  // Rare
                    ConsumableEnum.LambSauce        => 1f,  // Legendary
                    _ => 5f,
                };
                case 2: return _consumable switch
                {
                    ConsumableEnum.MinorHealth      => 25f, // Common
                    ConsumableEnum.Rewind           => 13f, // Common
                    ConsumableEnum.Alchemist        => 15f, // Common
                    ConsumableEnum.MajorHealth      => 18f, // Uncommon
                    ConsumableEnum.FateshineElixir  => 15f, // Uncommon
                    ConsumableEnum.NewUnit          => 15f, // Uncommon
                    ConsumableEnum.Prestige         => 12f, // Rare
                    ConsumableEnum.RunewellNectar   => 10f, // Rare
                    ConsumableEnum.Duplicate        => 10f, // Rare
                    ConsumableEnum.TrialofGrasses   => 10f, // Rare
                    ConsumableEnum.LambSauce        => 5f,  // Legendary
                    _ => 5f,
                };
                case 3: return _consumable switch
                {
                    ConsumableEnum.MinorHealth      => 15f, // Common
                    ConsumableEnum.Rewind           => 5f,  // Common
                    ConsumableEnum.Alchemist        => 8f,  // Common
                    ConsumableEnum.MajorHealth      => 18f, // Uncommon
                    ConsumableEnum.FateshineElixir  => 15f, // Uncommon
                    ConsumableEnum.NewUnit          => 15f, // Uncommon
                    ConsumableEnum.Prestige         => 16f, // Rare
                    ConsumableEnum.RunewellNectar   => 14f, // Rare
                    ConsumableEnum.Duplicate        => 14f, // Rare
                    ConsumableEnum.TrialofGrasses   => 14f, // Rare
                    ConsumableEnum.LambSauce        => 10f, // Legendary
                    _ => 5f,
                };
                default: 
                {
                    Debug.LogError($"Invalid act number {actNumber} for consumable drop chance calculation. Defaulting to act 3 weights.");
                    return _consumable switch
                    {
                        ConsumableEnum.MinorHealth      => 15f, // Common
                        ConsumableEnum.Rewind           => 5f,  // Common
                        ConsumableEnum.Alchemist        => 8f,  // Common
                        ConsumableEnum.MajorHealth      => 18f, // Uncommon
                        ConsumableEnum.FateshineElixir  => 15f, // Uncommon
                        ConsumableEnum.NewUnit          => 15f, // Uncommon
                        ConsumableEnum.Prestige         => 16f, // Rare
                        ConsumableEnum.RunewellNectar   => 14f, // Rare
                        ConsumableEnum.Duplicate        => 14f, // Rare
                        ConsumableEnum.TrialofGrasses   => 14f, // Rare
                        ConsumableEnum.LambSauce        => 10f, // Legendary
                        _ => 5f,
                    };
                };
            }
        }
        public static int ConsumableCost(ConsumableRarity _consumableRarity)
        {
            if (CostOverrides.TryGetValue(_consumableRarity, out int overrideCost)) return overrideCost;
            return _consumableRarity switch
            {
                ConsumableRarity.Common => 4,
                ConsumableRarity.Uncommon => 8,
                ConsumableRarity.Rare => 25,
                ConsumableRarity.Legendary => 60,
                _ => 69,
            };
        }
        public static int SellValue(ConsumableRarity _consumableRarity)
        {
            return ConsumableCost(_consumableRarity) / 2;
        }
        public static bool ConsumableRequiresTarget(ConsumableEnum _consumableEnum)
        {
            return _consumableEnum switch
            {
                ConsumableEnum.MinorHealth => true,
                ConsumableEnum.MajorHealth => true,
                ConsumableEnum.Prestige => true,
                ConsumableEnum.Duplicate => true,
                ConsumableEnum.NewUnit => false,
                ConsumableEnum.Alchemist => false,
                ConsumableEnum.Rewind => false,
                ConsumableEnum.TrialofGrasses => true,
                ConsumableEnum.FateshineElixir => false,
                ConsumableEnum.RunewellNectar => false,
                ConsumableEnum.LambSauce => false,
                _ => false,
            };
        }
    }
}
