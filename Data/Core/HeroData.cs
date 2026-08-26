using System.Collections.Generic;
using TJ.Spells;
using UnityEngine;

namespace TJ
{
    [System.Serializable]
    public struct Hero
    {
        public string HeroName;
        public string HeroDescription;
        public string[] HeroBonusDescription;
        public string HeroPrefabName;
        public UnlockCondition UnlockCondition;
        public UnlockCondition DemoUnlockCondition;
        public int StartingGold;
        public int HeroID;
        public Race Race;
        public UnitName SignatureUnit;
        // Pinned to spell slot 0 for this hero, and added to the shared cross-hero pool once the
        // hero has completed a run. See SpellLoadout.
        public Spell SignatureSpell;
        public UnitName[] StartingArmyUnits;
    }

    // Loads from HeroDataSO assets (Assets/Resources/HeroData/*.asset, one per hero, same
    // Resources.LoadAll pattern as SquadData/RaceData) instead of hardcoded instances, with mod
    // overrides layered on top (see HeroOverrideLoader). HeroID and Race are not overridable:
    // HeroID is the lookup key, and campaign generation assumes a stable hero-to-race pairing.
    public static class HeroData
    {
        public const int DefaultHeroID = 1;

        // Preserves the original individually-named static fields for existing callers
        // (SaveDataHandler.cs, CampaignSaveManager.cs, PlayPanel.cs, CollectionPanel.cs) - each was
        // a hardcoded Hero instance before, now a live lookup by the same HeroID.
        public static Hero EdricValeward => GetHeroByID(1);
        public static Hero RhydanGreythorne => GetHeroByID(2);
        public static Hero BoblinTheGoblinKing => GetHeroByID(3);
        public static Hero KragmukGorethirster => GetHeroByID(4);
        public static Hero BjornIronskull => GetHeroByID(5);
        public static Hero FreyjaStormweaver => GetHeroByID(6);
        public static Hero IltharionStarpire => GetHeroByID(7);
        public static Hero SerendaelOfNytherial => GetHeroByID(8);
        public static Hero SisterMorvayne => GetHeroByID(9);
        public static Hero LordDravenBloodreaver => GetHeroByID(10);
        public static Hero OdaNobukage => GetHeroByID(11);
        public static Hero TokugawaHarunobu => GetHeroByID(12);
        public static Hero HrothgarGoblinslayer => GetHeroByID(13);
        public static Hero BerthaBarrelstorm => GetHeroByID(14);
        public static Hero SkrixTheSwarmcaller => GetHeroByID(15);
        public static Hero ValthrexPrimeclaw => GetHeroByID(16);

        private static readonly Dictionary<int, Hero> _heroesByID = new();
        private static readonly List<Hero> _heroesSorted = new();

        private static void EnsureLoaded()
        {
            if (_heroesByID.Count > 0) return;
            LoadFromResourcesAndOverrides(ModLoadOrder.GetEnabledModFolderPathsInOrder());
        }

        public static void LoadFromResourcesAndOverrides(List<string> modFolders)
        {
            _heroesByID.Clear();
            _heroesSorted.Clear();

            HeroDataSO[] allSOs = Resources.LoadAll<HeroDataSO>("HeroData");
            foreach (var so in allSOs)
            {
                _heroesByID[so.heroData.HeroID] = so.heroData;
            }

            foreach (string modFolder in modFolders)
            {
                HeroOverrideLoader.ApplyOverridesFromModFolder(modFolder, _heroesByID);
            }

            // Sorted by HeroID so GetRandomHero's index-based selection below stays stable
            // regardless of Resources.LoadAll's (unspecified) enumeration order.
            _heroesSorted.AddRange(_heroesByID.Values);
            _heroesSorted.Sort((a, b) => a.HeroID.CompareTo(b.HeroID));
        }

        // Preserves the original public API shape (was a hardcoded array declared in HeroID
        // order) for existing callers (PlayPanel.cs indexes into it for the hero-select UI;
        // PlayerSaveDataEditor.cs/HeroCompletionEditor.cs iterate it).
        public static Hero[] Heroes
        {
            get
            {
                EnsureLoaded();
                return _heroesSorted.ToArray();
            }
        }

        public static Hero GetHeroByID(int id)
        {
            EnsureLoaded();
            if (_heroesByID.TryGetValue(id, out Hero hero)) return hero;
            return _heroesByID.TryGetValue(DefaultHeroID, out Hero fallback) ? fallback : default;
        }

        public static Race GetRaceFromHero(int _heroID) => GetHeroByID(_heroID).Race;

        public static Hero GetRandomHero()
        {
            EnsureLoaded();
            // Preserves a pre-existing quirk: starting the range at 1 means index 0 (Edric,
            // HeroID 1, first in ID order) can never be returned.
            return _heroesSorted[UnityEngine.Random.Range(1, _heroesSorted.Count)];
        }

        public static List<Hero> GetHeroesByRace(Race race)
        {
            EnsureLoaded();
            List<Hero> heroesOfRace = new List<Hero>();
            foreach (Hero hero in _heroesSorted)
            {
                if (hero.Race == race)
                {
                    heroesOfRace.Add(hero);
                }
            }
            return heroesOfRace;
        }
        public static List<UnitName> GetSignatureUnitsByRace(Race race)
        {
            List<UnitName> signatureUnits = new List<UnitName>();
            foreach (Hero hero in GetHeroesByRace(race))
            {
                signatureUnits.Add(hero.SignatureUnit);
            }
            return signatureUnits;
        }
    }
}
// Edric, the Fallen Knight: Once a celebrated knight serving a proud noble house, Edric’s world shattered when war and betrayal brought his liege low and his homeland to ruin. Now a wanderer in a fractured realm, he clings to a fading code of honor amidst a sea of mercenaries and brigands. Driven by a vision of a kingdom reborn, he fights not for gold but for justice, wielding a worn blade and sharp mind to rally the disheartened. Clad in battered regal armor, Edric gathers loyal souls willing to defy the chaos, carving a fragile path toward stability in a land teetering on the edge of oblivion.

// Rhydan, the Exiled Noble: Born to a minor noble house, Rhydan’s privileged life ended when his family was branded traitors and hunted by the crown. Forced into the wild, he honed his survival skills, becoming a spectral figure among outlaws and exiles. Known as a master of hidden trails and forgotten ruins, he aids the forsaken while biding his time for vengeance. As war engulfs the land, Rhydan steps from the shadows—not as a broken lord, but as a cunning hunter of orcs and leader of the displaced. His blade and knowledge of the wilderness guide his growing band toward a reckoning with those who wronged him.

// Boblin, the Goblin King: From the squalid depths of Rotspike Hollow, Boblin clawed his way to power, a rotund goblin with a flair for chaos and cunning. Through rigged duels—laced with traps and poison—he crushed rival leaders, uniting the fractious clans under his crude crown. Now, with a horde at his back, he leads gleeful raids on dwarf mines and elf woods, dreaming of a goblin realm carved from enemy lands. His wild charisma holds his followers, but his reckless greed stirs dissent among the ranks. Boblin’s rule is as brutal as it is unstable, a bloody gamble in a world of warring giants.

// Kragmuk Gorethirst: A hulking orc warlord whose name strikes dread into the hearts of men and beasts alike, Kragmuk Gorethirst is a whirlwind of carnage. His insatiable lust for blood empowers his ravagers, turning them into frenzied terrors on the battlefield with sharpened blades and relentless fury. Known for leaving nothing but smoldering ruins in his wake, he doubles the spoils from sacked cities, his greed matched only by his brutality.

// Bjorn Ironskull: Bjorn Ironskull, a giant among Vikings, earned his title when a foe’s mace shattered against his unyielding head. A master of siege and slaughter, he leads his armored warriors with a stoic ferocity, their defenses bolstered by his unbreakable presence. Every battle’s end sees him piling skulls as tribute, earning extra gold from the chaos—a grim reward for his relentless campaign of destruction.

// Freyja Stormweaver: A warrior-priestess touched by divine winds, commands the battlefield with the grace of a tempest and the strength of stone. Her voice weaves fate itself, inspiring her shieldmaidens to stand tall with unwavering courage and reinforced defenses. Hailed as the Rock of Trondheim, she shields her forces from fear, ensuring no terror can break their spirit as they fight under her storm-wrought banner.

//Iltharion Starpire: From the Astralwind Plains, Iltharion Starpire, a rebellious noble, leads Iltharion Forest’s Starcharge cavalry, his starlit lance shattering foes. Dubbed the Supernova of the West, his radiant presence fuels his warriors’ charges with celestial might. Clad in gleaming armor, he rallies riders to carve through chaos, protecting the fading groves. His dream is a unified Elven realm under starlight, his gallops a beacon of hope.

//Serendael of Nytherial: Serendael, a seeress from Nytherial Glade’s lunar shrines, wields moonlight to heal and summon treants for Iltharion Forest. Her visions of darkness drove her to lead, her crescent staff mending allies and awakening trees. Known as the Light of Nytherial, she doubles healing, bolstering forest spirits. Her grace fuels the fight to preserve the twilight realm from defilers.

//Sister Morvayne, once a devout priestess in a forgotten cloister, fell to the Sanguine Court’s dark whispers, her prayers twisted into necromantic hymns. From the desecrated Bleakspire Crypt, she weaves chants that summon undead hordes and curse the living with despair. Clad in tattered vestments stained with blood, her rosary of skulls channels profane power, binding souls to her will. Morvayne’s cold piety fuels the Sanguine Court’s endless march, her voice a dirge that chills the hearts of foes and inspires her skeletal legions to rise anew.

// Lord Draven Bloodreave, master of the Crimson Hollow, is a vampire lord whose thirst for blood is matched only by his ruthless ambition. Once a mortal prince, he embraced undeath to rule eternally, his blade dripping with the essence of fallen foes. His presence on the battlefield sows terror, his crimson eyes compelling enemies to flee or kneel. Leading the Sanguine Court’s elite, Draven carves through armies, his aristocratic disdain fueling the BloodKnights’ ferocity. He seeks to drown the world in darkness, claiming every soul for his eternal court.

//Oda Nobukage, the ruthless daimyo of a fractured clan, rose through cunning and blade, unifying warring provinces under his iron will. From the shadowed halls of Nagoya Fortress, he leads with unyielding ambition, his katana forged in dragonfire, seeking to conquer all under the Shogun Dynasty's banner. His presence ignites his warriors' fervor, turning ashigaru into unstoppable forces.

//Tokugawa Harunobu, born to a minor noble house near the imperial palace, rose through intellect and cunning, mastering economics, diplomacy, and strategy. During a famine threatening the emperor's rule, his reforms—tax incentives and grain reserves—averted crisis, earning the emperor's trust. As chief advisor, he built the dynasty's golden age with spy networks and trade mastery, amassing wealth amid whispers of manipulation. In war, his genius funded fusiliers and samurai. Loyal yet ambitious, Harunobu marches with the army, fueling conquests for eternal prosperity.

//Bushido Discipline: If army contains only Sakura Dynasty units, all units gain +20 [Leadership] and +4 [Melee Attack]

// Hrothgar Goblinslayer was once thane of a prosperous minor hold until a massive goblin night raid slaughtered his entire clan in a single bloody evening. Emerging as the sole survivor from beneath a pile of corpses, he swore a lifelong oath of vengeance upon all gruntkin. For decades he has stalked tunnels and mountain passes, his massive rune-axe dripping with goblin ichor, teaching every warrior under his command the brutal art of swarm-breaking. His mere presence fills dwarves with grim resolve and strikes terror into goblin hearts—he is the nightmare that goblins whisper about around their campfires.

// Bertha Barrelstorm is the master engineer of the Deepstone artillery guilds, renowned for designing the most devastating black-powder weapons the hold has ever forged. Born in the great gun-forges, she lost an eye to a misfiring prototype but turned that failure into relentless innovation, creating multi-barrel cannons and rune-guided mortars that can level entire enemy formations from miles away. With a perpetual cloud of pipe smoke and the smell of sulfur about him, Bertha leads her gun crews with booming precision, turning battles into thunderous symphonies of destruction and ensuring no foe ever reaches the dwarven lines intact.

// Skrix the Swarmcaller, a cunning kobold warlord who commands vast chittering hordes through fear and clever manipulation. Skrix turns weak and scattered tribes into an unstoppable tide, drowning his enemies beneath sheer numbers and ruthless coordination.

// Valthrex Primeclaw, a towering drakosaur champion forged in ritual combat and sacred duty. Valthrex leads disciplined war-beasts and elite guardians with brutal precision, crushing foes beneath claw, scale, and unyielding authority.
