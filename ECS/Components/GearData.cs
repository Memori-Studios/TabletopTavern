using System.Collections.Generic;
using System;

namespace TJ
{
    public enum GearRarity { Common, Uncommon, Rare }
    public enum GearID { None,
        ArmingSwords, Longbows, Glaives, ConscriptionOrders, TexanBBQ, BallisticCharts, DiamondTippedArrows, WellHonedAxes, RingoftheElvenKing, RiverTrout, RavensEye, Turkey, HeavyWeapons,
        BucklerShields, IronBank, OmenofFamine, QuantitativeEasingPolicy, Mitre, MichaelsSecretStuff, ChugJug, TowerShields, OrnateRing, JailersKey, BraceletoftheSunGoddess, ThePotato, Cauldron, Shungite, CommonBuilder, UncommonBuilder, RareBuilder, JoustingLances, BearSpray, CookieAndFowlCard, GnomishArmorers, PrivateeringPapers, NorthernLooters, PumpkinPie, AuraFarming, DwarvenTaxCollectors, LuckyHorseshoe,}

    /// <summary>
    /// How Gear is named in the localization table:
    /// {GearName}Name, {GearName}Desc, {GearName}Flavor
    /// e.g. Arming Swords would be ArmingSwordsName, ArmingSwordsDesc, ArmingSwordsFlavor
    /// </summary>
    [Serializable] public struct Gear
    {
        public string GearName;
        public GearRarity GearRarity;
        public int GearModifierValue;
        public bool BanFromShop;
    }
    //add gear for only positive bonuses
    public static class GearData
    {
        // Mod-supplied GearModifierValue overrides, keyed by GearID - applied on top of the
        // hardcoded defaults below by GetGear(). Lives here (not a separate evaluator class like
        // HeroBonusRuleData) because GearData already sits in the Components assembly, which
        // UnitSetUpSystem (Systems assembly) already references directly.
        private static readonly Dictionary<GearID, int> ModifierOverrides = new();

        public static void ClearModifierOverrides() => ModifierOverrides.Clear();
        public static void SetModifierOverride(GearID gearID, int value) => ModifierOverrides[gearID] = value;

        // Separate store from ModifierOverrides above - different value (gold cost vs gameplay
        // modifier) keyed by GearRarity (3 values) instead of GearID (one per item).
        private static readonly Dictionary<GearRarity, int> CostOverrides = new();

        public static void ClearCostOverrides() => CostOverrides.Clear();
        public static void SetCostOverride(GearRarity rarity, int cost) => CostOverrides[rarity] = cost;

        public const int GEAR_ARMINGSWORDS_MODIFIER         = 6;
        public const int GEAR_BUCKLERSHIELDS_MODIFIER       = 5;
        public const int GEAR_TOWERSHIELDS_MODIFIER        = 100;
        public const int GEAR_GLAIVES_MODIFIER              = 10;
        public const int GEAR_CONSCRIPTIONORDERS_MODIFIER   = 6;
        public const int GEAR_TEXANBBQ_MODIFIER             = 6;
        public const int GEAR_JOUSTINGLANCES_MODIFIER       = 8;
        public const int GEAR_GNOMISHARMORERS_MODIFIER      = 8;
        public const int GEAR_LONGBOWS_MODIFIER             = 25;
        public const int GEAR_WELLHONEDAXES_MODIFIER        = 10;
        public const int GEAR_RINGOFTHEELVENKING_MODIFIER   = 4;
        public const int GEAR_RAVENSEY_MODIFIER             = 20;
        public const int GEAR_BALLISTICCHARTS_MODIFIER      = 10;
        public const int GEAR_OMENOFFAMINE_MODIFIER         = 2;
        public const int GEAR_QUANTITATIVEEASINGPOLICY_MODIFIER = 10;
        public const int GEAR_DWARVANTAXCOLLECTORS_MODIFIER = 10;
        public const int GEAR_COMMONBUILDER_MODIFIER        = 3;
        public const int GEAR_UNCOMMONBUILDER_MODIFIER      = 10;
        public const int GEAR_RAREBUILDER_MODIFIER          = 30;
        public const int GEAR_SHUNGITE_MODIFIER             = 8;
        public const int GEAR_JAILERSKEY_MODIFIER           = 2;

        public static GearID[] GetGearIDs()
        {
            //return all GearID enums except the first one
            List<GearID> gearIDList = new List<GearID>();
            foreach (GearID gearID in Enum.GetValues(typeof(GearID))) {
                if (gearID != GearID.None) {
                    gearIDList.Add(gearID);
                }
            }
            return gearIDList.ToArray();
        }
        public static Gear GetGear(GearID _gear)
        {
            Gear gear = GetBaseGear(_gear);
            if (ModifierOverrides.TryGetValue(_gear, out int overrideValue))
                gear.GearModifierValue = overrideValue;
            return gear;
        }

        private static Gear GetBaseGear(GearID _gear)
        {
            return _gear switch
            {
                //unit stats
                GearID.ArmingSwords => ArmingSwords,//melee attack melee
                GearID.BucklerShields => BucklerShields,//melee defense melee
                GearID.Longbows => Longbows,//ranged range
                GearID.Glaives => Glaives,//anti large weapon strength
                GearID.ConscriptionOrders => ConscriptionOrders, //melee attack and defense common
                GearID.TexanBBQ => TexanBBQ,//weapon strength melee
                GearID.JoustingLances => JoustingLances, //weapon strength cavalry
                GearID.GnomishArmorers => GnomishArmorers, //armor uncommon
                GearID.DiamondTippedArrows => DiamondTippedArrows, // armor piercing ranged
                GearID.WellHonedAxes => WellHonedAxes, // armor piercing melee
                GearID.BallisticCharts => BallisticCharts, //accuracy ranged
                GearID.RingoftheElvenKing => RingoftheElvenKing, //missile strength ranged
                GearID.RavensEye => RavensEye, //accuracy ranged uncommon rare
                GearID.Turkey => Turkey,//anti large ranged
                GearID.HeavyWeapons => HeavyWeapons, //armor piercing rare
                GearID.Shungite => Shungite, //melee attack uncommon
                GearID.QuantitativeEasingPolicy => QuantitativeEasingPolicy, //leadership all units

                //gold
                GearID.IronBank => IronBank,
                GearID.OmenofFamine => OmenofFamine,
                GearID.OrnateRing => OrnateRing,
                GearID.ThePotato => ThePotato,
                GearID.Cauldron => Cauldron,
                GearID.DwarvenTaxCollectors => DwarvenTaxCollectors,

                //shops
                GearID.CookieAndFowlCard => CookieAndFowlCard,
                GearID.CommonBuilder => CommonBuilder,
                GearID.UncommonBuilder => UncommonBuilder,
                GearID.RareBuilder => RareBuilder,
                GearID.PrivateeringPapers => PrivateeringPapers,

                //battle
                GearID.BraceletoftheSunGoddess => BraceletoftheSunGoddess,
                GearID.TowerShields => TowerShields,
                GearID.BearSpray => BearSpray,
                GearID.LuckyHorseshoe => LuckyHorseshoe,
                GearID.RiverTrout => RiverTrout,

                //event
                GearID.MichaelsSecretStuff => MichaelsSecretStuff,
                // GearID.EnronAccounting => EnronAccounting,

                //unit prestige and health
                GearID.Mitre => Mitre,
                GearID.ChugJug => ChugJug,

                //towns
                GearID.PumpkinPie => PumpkinPie,
                GearID.AuraFarming => AuraFarming,
                GearID.NorthernLooters => NorthernLooters,
                GearID.JailersKey => JailersKey,

                _ => None,
            };
        }

        #region UnitStats

        //Desc: "No gear equipped"
        //Flavor: "You have no gear equipped"
        public static Gear None = new ()
        {
            GearName = "None",
            GearRarity = GearRarity.Common,
        };

        //Desc: "[Melee] units gain +{0} [Melee Attack]"
        //Flavor: "Swords that are perfect for stabbing"
        public static Gear ArmingSwords = new ()
        {
            GearName = "Arming Swords",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_ARMINGSWORDS_MODIFIER,
        };

        //Desc: "[Shielded] units are now Invulnerable to ranged attacks"
        //Flavor: "The best defense is a good defense"
        public static Gear BucklerShields = new ()
        {
            GearName = "Buckler Shields",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_BUCKLERSHIELDS_MODIFIER,
        };

        //Desc: "[AntiLarge] units gain +{0} [Weapon Strength]"
        //Flavor: "A weapon that is perfect for poking at range"
        public static Gear Glaives = new ()
        {
            GearName = "Glaives",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_GLAIVES_MODIFIER,
        };

        //Desc: "[Common] units gain +{0} [Melee Attack] & [Melee Defense]"
        //Flavor: "Orders from the emperor to conscript more troops"
        public static Gear ConscriptionOrders = new ()
        {
            GearName = "Conscription Orders",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_CONSCRIPTIONORDERS_MODIFIER,
        };

        //Desc: "[Melee] units gain +{0} [Weapon Strength]"
        //Flavor: "A BBQ recipe from the land of Tex-ass"
        public static Gear TexanBBQ = new ()
        {
            GearName = "Texan BBQ",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_TEXANBBQ_MODIFIER,
        };

        //Desc: "[Cavalry] Units gain +{0} [Weapon Strength]"
        //Flavor: "Cheers love! The cavalry's here!"
        public static Gear JoustingLances = new ()
        {
            GearName = "Jousting Lances",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_JOUSTINGLANCES_MODIFIER,
        };

        //Desc: "[Uncommon] Units gain +{0} [Armor]"
        //Flavor: "Better chainmail, better plating, Gnomish Armorers"
        public static Gear GnomishArmorers = new ()
        {
            GearName = "Gnomish Armorers",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_GNOMISHARMORERS_MODIFIER,
        };

        //Desc: "[Ranged] units gain [Armor Piercing]"
        //Flavor: "Cut armor into pieces, this is my last resort"
        public static Gear DiamondTippedArrows = new ()
        {
            GearName = "Diamond Tipped Arrows",
            GearRarity = GearRarity.Common,
        };

        //Desc: "[Ranged] units gain +{0} Bonus [Range]"
        //Flavor: "A bow that is perfect for shooting arrows"
        public static Gear Longbows = new ()
        {
            GearName = "Longbows",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_LONGBOWS_MODIFIER,
        };

        //Desc: "[Armor Piercing] units gain +{0} [Melee Attack]"
        //Flavor: "Like a hot knife through butter"
        public static Gear WellHonedAxes = new ()
        {
            GearName = "Well Honed Axes",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_WELLHONEDAXES_MODIFIER,
        };

        //Desc: "[Ranged] units gain +{0} [Missile Strength]"
        //Flavor: "Why make toys when you can make war"
        public static Gear RingoftheElvenKing = new ()
        {
            GearName = "Ring of the Elven King",
            GearRarity = GearRarity.Rare,
            GearModifierValue = GEAR_RINGOFTHEELVENKING_MODIFIER,
        };

        //Desc: "[Uncommon] & [Rare] [Ranged] units gain +{0} [Accuracy]"
        //Flavor: "Oh my god it's an actual eye"
        public static Gear RavensEye = new ()
        {
            GearName = "Ravens Eye",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_RAVENSEY_MODIFIER,
        };

        //Desc: "[Ranged] units gain +{0} [Accuracy]"
        //Flavor: "It's not rocket science you heretic"
        public static Gear BallisticCharts = new ()
        {
            GearName = "Ballistic Charts",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_BALLISTICCHARTS_MODIFIER,
        };

        //Desc: "[Ranged] units gain [Anti Large]"
        //Flavor: "It's hard to miss a target that big"
        public static Gear Turkey = new ()
        {
            GearName = "Turkey",
            GearRarity = GearRarity.Rare,
        };

        //Desc: "[Rare] units gain [Armor Piercing]"
        //Flavor: "Bring out the big guns, well, swords"
        public static Gear HeavyWeapons = new ()
        {
            GearName = "Heavy Weapons",
            GearRarity = GearRarity.Common,
        };

        #endregion

        #region Gold

        //Desc: "Generate double gold from interest"
        //Flavor: "The iron bank will have its due"
        public static Gear IronBank = new ()
        {
            GearName = "Iron Bank",
            GearRarity = GearRarity.Rare,
        };

        //Desc: "Earn 1 <sprite name=GoldSprite> bonus interest for every empty gear slot"
        //Flavor: "Less is more"
        public static Gear OmenofFamine = new ()
        {
            GearName = "Omen of Famine",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_OMENOFFAMINE_MODIFIER,
        };

        //Desc: "All units gain +{0} [Leadership]"
        //Flavor: "I ran the numbers and it checks out"
        public static Gear QuantitativeEasingPolicy = new ()
        {
            GearName = "Quantitative Easing Policy",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_QUANTITATIVEEASINGPOLICY_MODIFIER,
        };

        //Desc: "Doubles gold when sold (Max 20)"
        //Flavor: "Whoever said money can't buy happiness was never broke"
        public static Gear OrnateRing = new ()
        {
            GearName = "Ornate Ring",
            GearRarity = GearRarity.Common,
            BanFromShop = true
        };

        //Desc: "Increases in sell value each chapter"
        //Flavor: "It's a potato"
        public static Gear ThePotato = new ()
        {
            GearName = "The Potato",
            GearRarity = GearRarity.Common,
        };

        //Desc: "Sells for the combined value of other gear"
        //Flavor: "Bubble bubble toil and trouble"
        public static Gear Cauldron = new ()
        {
            GearName = "Cauldron",
            GearRarity = GearRarity.Common,
            BanFromShop = true
        };

        //Desc: "First consumable of the shop is free"
        //Flavor: "Buy in bulk and save"
        public static Gear CookieAndFowlCard = new ()
        {
            GearName = "Cookie and Fowl Card",
            GearRarity = GearRarity.Common,
        };

        //Desc: "Collect up to +{0} interest per turn"
        //Flavor: "They always get their due"
        public static Gear DwarvenTaxCollectors = new ()
        {
            GearName = "Dwarven Tax Collectors",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_DWARVANTAXCOLLECTORS_MODIFIER,
        };

        #endregion

        #region Recruitment

        //Desc: "Reduces cost of [Common] Unit Packs by {0}"
        //Flavor: "It's like they're giving them away"
        public static Gear CommonBuilder = new ()
        {
            GearName = "Common Builder",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_COMMONBUILDER_MODIFIER,
        };

        //Desc: "Reduces cost of [Uncommon] Unit Packs by {0}"
        //Flavor: "Better than the common builder"
        public static Gear UncommonBuilder = new ()
        {
            GearName = "Uncommon Builder",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_UNCOMMONBUILDER_MODIFIER,
        };

        //Desc: "Reduces cost of [Rare] Unit Packs by {0}"
        //Flavor: "The best money can buy for less"
        public static Gear RareBuilder = new ()
        {
            GearName = "Rare Builder",
            GearRarity = GearRarity.Uncommon,
            GearModifierValue = GEAR_RAREBUILDER_MODIFIER,
        };

        #endregion

        #region Event

        //Desc: "No longer lose health from events"
        //Flavor: "It's just water"
        public static Gear MichaelsSecretStuff = new ()
        {
            GearName = "Michaels Secret Stuff",
            GearRarity = GearRarity.Common,
        };

        //Desc: "[Uncommon] units gain +{0} [Melee Attack]"
        //Flavor: "Little pyramids, put them around mi casa"
        public static Gear Shungite = new ()
        {
            GearName = "Shungite",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_SHUNGITE_MODIFIER,
        };

        #endregion

        #region Reputation

        //Desc: "Each gold spent on modifying rolls give +{0} to the dice value"
        //Flavor: "It's only fraud if you get caught"
        //Old Enron Accounting: 


        //Desc: "[Shielded] units are now Invulnerable to ranged attacks"
        //Flavor: "The best defense is a good defense"
        public static Gear TowerShields = new ()
        {
            GearName = "Tower Shields",
            GearRarity = GearRarity.Rare,
            GearModifierValue = GEAR_TOWERSHIELDS_MODIFIER,
        };

        //Desc: "Reduces cost of recruiting from towns by {0} gold"
        //Flavor: "No one wants them anyway"
        public static Gear JailersKey = new ()
        {
            GearName = "Jailers Key",
            GearRarity = GearRarity.Common,
            GearModifierValue = GEAR_JAILERSKEY_MODIFIER,
        };

        #endregion

        #region Battle

        //Desc: "Prevents rain from falling on the battlefield"
        //Flavor: "The sun always shines on the righteous"
        public static Gear BraceletoftheSunGoddess = new ()
        {
            GearName = "Bracelet of the Sun Goddess",
            GearRarity = GearRarity.Common,
        };

        //Desc: "No longer encounter [Monstrous] or [SingleUnit] enemies"
        //Flavor: "It's like pepper spray but for bears"
        public static Gear BearSpray = new ()
        {
            GearName = "Bear Spray",
            GearRarity = GearRarity.Rare,
        };

        //Desc: "Guarantees a consumable drop from battles"
        //Flavor: "It's like a rabbit's foot but for horses"
        public static Gear LuckyHorseshoe = new ()
        {
            GearName = "Lucky Horseshoe",
            GearRarity = GearRarity.Common,
        };

        //Desc: "Conscripted survivors post battle are granted full health"
        //Flavor: "Omega 3 fatty acids are good for your health"
        public static Gear RiverTrout = new ()
        {
            GearName = "River Trout",
            GearRarity = GearRarity.Common,
        };

        #endregion

        #region Prestige and Health

        //Desc: "Doubles troop health recovery"
        //Flavor: "I really want to, chug jug with you"
        public static Gear ChugJug = new ()
        {
            GearName = "Chug Jug",
            GearRarity = GearRarity.Common,
        };

        //Desc: "Prestige a random unit when sold"
        //Flavor: "Hats off to you"
        public static Gear Mitre = new ()
        {
            GearName = "Mitre",
            GearRarity = GearRarity.Uncommon,
        };

        #endregion

        #region Towns

        //Desc: "First Gear Pack in the shop is free"
        //Flavor: "It's not piracy if you have a permit"
        public static Gear PrivateeringPapers = new ()
        {
            GearName = "Privateering Papers",
            GearRarity = GearRarity.Common,
        };

        //Desc: "Doubles loot from sacking towns"
        //Flavor: "Professionals know where to find the good stuff"
        public static Gear NorthernLooters = new ()
        {
            GearName = "Northern Looters",
            GearRarity = GearRarity.Uncommon,
        };

        //Desc: "Units heal to full health when entering towns (peacefully)"
        //Flavor: "It's like a warm hug for your stomach"
        public static Gear PumpkinPie = new ()
        {
            GearName = "Pumpkin Pie",
            GearRarity = GearRarity.Uncommon,
        };

        //Desc: "Towns have smaller garrisons for protection"
        //Flavor: "Effortless emanation of good vibes"
        public static Gear AuraFarming = new ()
        {
            GearName = "Aura Farming",
            GearRarity = GearRarity.Rare,
        };

        #endregion

        public static Gear[] GetAllGear()
        {
            return new [] {
                //unit stats
                ArmingSwords,
                BucklerShields,
                Longbows,
                Glaives,
                ConscriptionOrders,
                TexanBBQ,
                BallisticCharts,
                JoustingLances,
                GnomishArmorers,
                DiamondTippedArrows,
                WellHonedAxes,
                RingoftheElvenKing,
                RavensEye,
                Turkey,
                HeavyWeapons,
                Shungite,
                QuantitativeEasingPolicy,
                TowerShields,

                //gold
                IronBank,
                OmenofFamine,
                OrnateRing,
                ThePotato,
                Cauldron,
                DwarvenTaxCollectors,

                //shops
                CommonBuilder,
                UncommonBuilder,
                RareBuilder,
                PrivateeringPapers,
                CookieAndFowlCard,

                //battle
                BraceletoftheSunGoddess,
                BearSpray,
                LuckyHorseshoe,
                RiverTrout,

                //events
                MichaelsSecretStuff,
                // EnronAccounting,

                //prestige and health
                Mitre,
                ChugJug,

                //towns
                PumpkinPie,
                AuraFarming,
                NorthernLooters,
                JailersKey,
            };
        }
        public static List<GearID> GetRandomGear(int _amount, List<GearID> _aquiredGearList, int _seed, int _bookNumver, bool _isShop = false)
        {
            List<GearID> newGearList = new();
            //get random piece of gear without repeating
            GearID[] allGear = Enum.GetValues(typeof(GearID)) as GearID[];

            // One Random for the whole draw, and retries pull the NEXT sample from it. Re-seeding
            // per retry (_seed + fails) used to walk the retries along the same seed line the
            // caller's reroll counter advances by 1, so a draw that skipped k>=1 excluded
            // candidates landed on exactly the seed the next draw would start walking from. The
            // shop then handed out the same item twice in a row whenever the player didn't take it
            // (the only thing that shifts the walk is the drawn item joining the exclusion list) -
            // guaranteed with full gear slots, since claiming is impossible there.
            Random random = new(Seed: _seed + (_bookNumver * 13));

            for (int i = 0; i < _amount; i++) {
                GearID gearID = allGear[random.Next(1, allGear.Length)];
                Gear gear = GetGear(gearID);

                if (newGearList.Contains(gearID) || _aquiredGearList.Contains(gearID)) {
                    i--;
                    continue;
                } else if(_isShop && gear.BanFromShop){
                    i--;
                    continue;
                } else {
                    newGearList.Add(gearID);
                }
            }

            for(int i = 0; i < newGearList.Count; i++) {
                GearID gearID = newGearList[i];
                newGearList[i] = gearID;
            }
            return newGearList;
        }
        // Mirrors GetRandomGear's exclusion rules to predict whether it can satisfy _amount
        // without looping forever - used to decide when a sold-gear exclusion list must reset.
        public static bool HasEnoughEligibleGear(List<GearID> _excludedGear, int _amount, bool _isShop = false)
        {
            GearID[] allGear = Enum.GetValues(typeof(GearID)) as GearID[];
            int eligibleCount = 0;
            for (int i = 1; i < allGear.Length; i++) { // index 0 is GearID.None, never drawn
                GearID gearID = allGear[i];
                if (_excludedGear.Contains(gearID)) continue;
                if (_isShop && GetGear(gearID).BanFromShop) continue;
                eligibleCount++;
                if (eligibleCount >= _amount) return true;
            }
            return false;
        }
        public static float SkipChance(GearRarity cardRarity) => cardRarity switch
        {
            GearRarity.Common => 0.1f,
            GearRarity.Uncommon => 0.5f,
            GearRarity.Rare => 0.75f,
            _ => 0,
        };
        public static int GearCost(GearRarity _gearRarity)
        {
            if (CostOverrides.TryGetValue(_gearRarity, out int overrideCost)) return overrideCost;
            return _gearRarity switch
            {
                GearRarity.Common => 2,
                GearRarity.Uncommon => 4,
                GearRarity.Rare => 6,
                _ => 0,
            };
        }
        public static int GetSellValue(GearRarity _gearRarity)
        {
            return GearCost(_gearRarity);
        }
    }
}
