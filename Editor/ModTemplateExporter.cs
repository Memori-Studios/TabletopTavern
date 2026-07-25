using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TJ
{
    // One-click "known-good starting point" for mod authors, covering every override file
    // type. TabletopTavernData.ExportUnitOverridesTemplate (its own [ContextMenu]) still exists for
    // exporting just unit_overrides.json on its own - this is the unified version.
    public static class ModTemplateExporter
    {
        [MenuItem("Tools/Modding/Export Mod Template")]
        private static void ExportTemplate()
        {
            string templateFolder = Path.Combine(ModLoadOrder.ModsRootPath, "_Template");
            Directory.CreateDirectory(templateFolder);

            File.WriteAllText(Path.Combine(templateFolder, SquadStatsOverrideLoader.FileName),
                SquadStatsOverrideLoader.ExportTemplate(TabletopTavernData.Instance.SquadStatsDictionary, TabletopTavernData.Instance.SquadAssetsDictionary));

            File.WriteAllText(Path.Combine(templateFolder, RaceDataOverrideLoader.FileName),
                RaceDataOverrideLoader.ExportTemplate(TabletopTavernData.Instance.RaceDataDictionary));

            File.WriteAllText(Path.Combine(templateFolder, HeroOverrideLoader.FileName),
                HeroOverrideLoader.ExportTemplate(HeroData.Heroes.ToDictionary(h => h.HeroID, h => h)));

            File.WriteAllText(Path.Combine(templateFolder, HeroBonusRuleOverrideLoader.FileName),
                HeroBonusRuleOverrideLoader.ExportTemplate());

            File.WriteAllText(Path.Combine(templateFolder, GearOverrideLoader.FileName),
                GearOverrideLoader.ExportTemplate());

            File.WriteAllText(Path.Combine(templateFolder, ArmyGenerationRuleOverrideLoader.FileName),
                ArmyGenerationRuleOverrideLoader.ExportTemplate());

            File.WriteAllText(Path.Combine(templateFolder, EconomyOverrideLoader.FileName),
                EconomyOverrideLoader.ExportTemplate());

            File.WriteAllText(Path.Combine(templateFolder, RaceBonusOverrideLoader.FileName),
                RaceBonusOverrideLoader.ExportTemplate());

            File.WriteAllText(Path.Combine(templateFolder, LocalizationOverrideLoader.FileName),
                LocalizationOverrideLoader.ExportTemplate());

            string modManifestPath = Path.Combine(templateFolder, ModLoadOrder.ModManifestFileName);
            if (!File.Exists(modManifestPath))
            {
                var manifest = new ModManifest { description = "Generated from current game data - edit the values you want to change, delete files you don't need." };
                File.WriteAllText(modManifestPath, JsonUtility.ToJson(manifest, true));
            }

            Debug.Log($"[ModTemplateExporter] Exported mod template to {templateFolder}");
            EditorUtility.RevealInFinder(templateFolder);
        }
    }
}
