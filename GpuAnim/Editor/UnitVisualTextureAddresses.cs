using MemoriStudios.TJBake;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TabletopTavern.GpuAnim.Editor
{
    /// <summary>
    /// The game's own unit bakes name their textures as Addressables instead of copying PNGs, so the build keeps one
    /// compressed copy of each and battle load shares it.
    /// </summary>
    public static class UnitVisualTextureAddresses
    {
        public const string GroupName = "UnitVisualTextures";
        public const string AddressPrefix = "UnitVisualTex/";
        // Copied so the new group builds and loads like the other local groups.
        private const string TemplateGroupName = "Recruitment";

        /// <summary>The manifest name for this texture, making it addressable if it is not; null means copy it out as a PNG.</summary>
        public static string Reference(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || path.Contains("/Resources/")) return null;
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;
            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                AddressableAssetGroup group = settings.FindGroup(GroupName) ?? CreateGroup(settings);
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = AddressPrefix + guid;
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            }
            return TJBakeValidation.HostTexturePrefix + entry.address;
        }

        private static AddressableAssetGroup CreateGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup template = settings.FindGroup(TemplateGroupName);
            AddressableAssetGroup group = template != null
                ? settings.CreateGroup(GroupName, false, false, true, template.Schemas)
                : settings.CreateGroup(GroupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            // One bundle per texture, so a battle opens only the textures of the units it fields.
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null) bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            return group;
        }
    }
}
