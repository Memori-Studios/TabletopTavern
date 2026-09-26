using UnityEngine;
using Memori.Utilities;
using Memori.SaveData;

namespace TJ.MainMenu
{
    public class TavernThemeManager : Singleton<TavernThemeManager>
    {
        [SerializeField] private TavernThemeData[] _allThemes;
        [SerializeField] private Transform _spawnParent;
        [SerializeField] private Material _flagMaterial;
        [SerializeField] private Material _tableClothMaterial1, _tableClothMaterial2;
        [SerializeField] private MeshRenderer[] _flags;
        Material _flagMaterialInstance;
        Material _tableClothInstance1, _tableClothInstance2;
        GameObject _activeThemeInstance;
        string _activeThemeKey;
        TavernThemeData themeToLoad;
        int _loadGeneration;
        readonly System.Threading.Tasks.TaskCompletionSource<bool> _bootThemeLoad = new System.Threading.Tasks.TaskCompletionSource<bool>();

        // Completes once the boot theme is placed or has failed, so the menu can keep its doors shut until then.
        public System.Threading.Tasks.Task BootThemeLoaded => _bootThemeLoad.Task;

        private async void Start()
        {
            try
            {
                _flagMaterialInstance = Instantiate(_flagMaterial);
                CreateTableClothInstances();
                SaveDataHandler.RefreshTavernThemeUnlocks();
                themeToLoad = GetThemeForCurrentSave();
                var loadTimer = System.Diagnostics.Stopwatch.StartNew();
                await LoadTheme(themeToLoad);
                Debug.Log($"[TavernThemeManager] Boot theme '{(themeToLoad == null ? "none" : themeToLoad.ThemeName)}' loaded in {loadTimer.ElapsedMilliseconds} ms");
            }
            finally
            {
                _bootThemeLoad.TrySetResult(true);
            }
        }

        private TavernThemeData GetThemeForCurrentSave()
        {
            if (SaveDataHandler.TryGetActiveTavernTheme(out Race savedRace))
            {
                foreach (TavernThemeData theme in _allThemes)
                {
                    if (theme.Race == savedRace)
                        return theme;
                }
            }

            // Fall back to the Special (None) theme
            foreach (TavernThemeData theme in _allThemes)
            {
                if (theme.Race == Race.Special)
                    return theme;
            }

            return null;
        }

        public async void ApplyTheme(TavernThemeData _theme)
        {
            themeToLoad = _theme;
            await LoadTheme(themeToLoad);
        }

        public void UnloadTheme()
        {
            if (_activeThemeInstance != null)
            {
                Destroy(_activeThemeInstance);
                _activeThemeInstance = null;
            }

            // Release the previous theme's handle. It was loaded persistent (pinned),
            // so ReleaseAll() won't reclaim it — the owner must release it explicitly
            // or switching themes leaks the old bundle.
            if (!string.IsNullOrEmpty(_activeThemeKey))
            {
                AddressablesManager.Instance.Release(_activeThemeKey);
                _activeThemeKey = null;
            }
        }

        private async System.Threading.Tasks.Task LoadTheme(TavernThemeData _theme)
        {
            UnloadTheme();

            if (_theme == null || _theme.ThemeObjects == null || !_theme.ThemeObjects.RuntimeKeyIsValid())
                return;

            int generation = ++_loadGeneration;
            string key = _theme.ThemeObjects.AssetGUID;
            // Persistent: the Tavern scene is permanent, so this instance stays alive
            // across the ReleaseAll() calls on Map/Battle transitions.
            GameObject prefab = await AddressablesManager.Instance.LoadAsync<GameObject>(_theme.ThemeObjects, persistent: true);
            if (prefab == null || generation != _loadGeneration) return;

            _activeThemeKey = key;
            _activeThemeInstance = Instantiate(prefab, _spawnParent);
            ColorFlags();
        }
        private void ColorFlags()
        {
            // Debug.Log($"[TavernThemeManager] Coloring flags for theme: {themeToLoad.ThemeName}");
            //get all flag components in the theme instance and set their colors based on the race data
            _flagMaterialInstance.SetColor("_PrimaryColor", themeToLoad.RaceData.PrimaryColor);
            _flagMaterialInstance.SetColor("_SecondaryColor", themeToLoad.RaceData.SecondaryColor);
            _flagMaterialInstance.SetColor("_OutlineColor", themeToLoad.RaceData.AccentColor);
            // _flagMaterialInstance.SetTexture("_IconSprite", null); 
            
            foreach (MeshRenderer flag in _flags)
            {
                flag.material = _flagMaterialInstance;
            }

            _tableClothInstance1.color = themeToLoad.RaceData.PrimaryColor;
            _tableClothInstance2.color = themeToLoad.RaceData.SecondaryColor;
        }

        #region Tablecloth material instances
        // The two tablecloth materials are project assets. Writing .color on them directly
        // dirtied the .mat files on disk in the Editor on every theme load, so Unity VCS
        // kept auto-checking them out. Swap every tablecloth renderer in the Tavern scene
        // onto a runtime copy and tint that instead, the same way the flags use
        // _flagMaterialInstance.
        private void CreateTableClothInstances()
        {
            _tableClothInstance1 = Instantiate(_tableClothMaterial1);
            _tableClothInstance2 = Instantiate(_tableClothMaterial2);

            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool swapped = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == _tableClothMaterial1)
                        {
                            materials[i] = _tableClothInstance1;
                            swapped = true;
                        }
                        else if (materials[i] == _tableClothMaterial2)
                        {
                            materials[i] = _tableClothInstance2;
                            swapped = true;
                        }
                    }

                    if (swapped) renderer.sharedMaterials = materials;
                }
            }
        }

        private void OnDestroy()
        {
            Destroy(_flagMaterialInstance);
            Destroy(_tableClothInstance1);
            Destroy(_tableClothInstance2);
        }
        #endregion
    }
}
