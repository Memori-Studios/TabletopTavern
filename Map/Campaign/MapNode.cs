using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Feedbacks;
using Shapes;
using System.Collections.Generic;
using Memori.Audio;
using QuickOutline;
using TMPro;
using Memori.Localization;
using Memori.Tooltip;

namespace TJ.Map
{
[RequireComponent(typeof(MMF_Player))]
    public class MapNode : MonoBehaviour
    {
        [SerializeField] private MapNodeData _mapNodeData; // Data for the node
        public MapNodeData Value => _mapNodeData;
        public bool Selectable => selectable;
        public bool Surprise => surprise;
        public bool WasHidden => _wasHidden;
        [SerializeField] private Color hoverColor, defaultColor, passedColor, completedColor, pathCompletedColor;
        [SerializeField] private Transform iconTransform;
        [SerializeField] private MeshRenderer _mapNodeBase;
        [SerializeField] private Material defaultMaterial, passedMaterial;
        [SerializeField] private GameObject selectionParticles;
        [SerializeField] private QuickOutline.Outline outline;

        [Header("Game Object Icons")]
        [SerializeField] private TownFlag skirmishFlag;
        [SerializeField] private TownFlag townFlag, hordeFlag;
        [SerializeField] private GameObject surpriseGameObject, shopGameObject, eventGameObject, treasureGameObject;
        [SerializeField] private GameObject tavernGameObject, campfireGameObject;

        [Header("Weather & Biome Flags")]
        [SerializeField] private Material miniSkirmishBaseMaterial;
        [SerializeField] private Sprite weatherIconRain, weatherIconFog, weatherIconSnow;
        [SerializeField] private Sprite biomeIconForest, biomeIconRiver, biomeIconSwamp;
        [SerializeField] private GameObject biomeFlag, weatherFlag;
        [SerializeField] private TMP_Text weatherFlagText, biomeFlagText;
        [SerializeField] private MMF_Player mouseOverWeatherMMF_Player, mouseOffWeatherMMF_Player;
        [SerializeField] private MMF_Player mouseOverBiomeMMF_Player, mouseOffBiomeMMF_Player;

        [Header("Town Stuff")]
        [SerializeField] private Transform townTextCanvas;
        [SerializeField] private Shader alwaysOnTopFontShader; // TextMeshPro/Distance Field Overlay
        [SerializeField] private Shader alwaysOnTopUIShader; // MoreMountains/MMZTestAlways
        [SerializeField] private GameObject townGameObject;
        [SerializeField] private GameObject villageGO, castleGO, cityGO;
        [SerializeField] private TMP_Text townNameText;
        [SerializeField] private TMP_Text townSizeText;
        [SerializeField] private MMF_Player mouseOverTownMMF_Player;
        [SerializeField] private MMF_Player mouseOffTownMMF_Player;
        
        private Camera mapSceneCamera;
        private MMF_Player mMF_Player;
        private bool completed = false;
        private bool selectable = false;
        private bool surprise = false;
        private bool _wasHidden = false;
        private BoxCollider boxCollider;
        Race _race;
        Weather _weather;
        Biome _biome;
        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            boxCollider.enabled = false;
            if (mapSceneCamera == null)
            {
                mapSceneCamera = FindFirstObjectByType<MapSceneManager>().MapScecneCamera;
            }
            mMF_Player = GetComponent<MMF_Player>();
            selectionParticles.SetActive(false);
            ApplyAlwaysOnTop(townTextCanvas);
            ApplyAlwaysOnTop(weatherFlagText);
            ApplyAlwaysOnTop(biomeFlagText);
        }
        private void Start()
        {
            boxCollider.enabled = true;
        }
        public void DisableAllIcons()
        {
            surpriseGameObject.SetActive(false);
            skirmishFlag.gameObject.SetActive(false);
            hordeFlag.gameObject.SetActive(false);
            shopGameObject.SetActive(false);
            eventGameObject.SetActive(false);
            treasureGameObject.SetActive(false);
            townGameObject.SetActive(false);
            if (tavernGameObject != null) tavernGameObject.SetActive(false);
            if (campfireGameObject != null) campfireGameObject.SetActive(false);
            outline.enabled = false;
        }
        public void SetUp(MapNodeData mapNodeData, Camera _camera, bool _surprise, Race race)
        {
            _mapNodeData = mapNodeData;
            mapSceneCamera = _camera;
            surprise = _surprise;
            _wasHidden = _surprise;
            _race = race;

            // icon.color = defaultColor;
            DisableAllIcons();
            if (Application.isPlaying)
            {
                MapRegion mapRegion = MapThemeManager.Instance.GetMapRegion(_race);
                int campaignSeed = CampaignManager.Instance.CampaignSaveManager.SaveData.seed;
                int bookNum = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
                _weather = CampaignSaveManager.GenerateNodeWeather(_mapNodeData.index, campaignSeed, bookNum, mapRegion);
                _biome = CampaignSaveManager.GenerateNodeBiome(_mapNodeData.index, campaignSeed, bookNum, mapRegion);
            }
            if (surprise)
            {
                surpriseGameObject.SetActive(true);
                return;
            }

            UpdateIcon();
            RegisterFlagTransform();
        }

        private void RegisterFlagTransform()
        {
            var manager = MapNodeFacingManager.Instance;
            if (manager == null) return;

            switch (_mapNodeData.type)
            {
                case NodeType.Skirmish:
                    manager.Register(skirmishFlag.transform);
                    break;
                case NodeType.Town:
                    manager.Register(townFlag.transform);
                    manager.Register(townTextCanvas, lockY: false);
                    break;
                case NodeType.Horde:
                    manager.Register(hordeFlag.transform);
                    break;
            }

            RegisterText(manager, weatherFlagText);
            RegisterText(manager, biomeFlagText);
        }

        private static void RegisterText(MapNodeFacingManager manager, TMP_Text text)
        {
            if (text == null) return;
            if (text.TryGetComponent<Renderer>(out var r))
                manager.Register(text.transform, r, lockY: false);
            else
                manager.Register(text.transform, lockY: false);
        }

        // Forces every Graphic under canvasRoot (TMP text + UI images) to render through 3D geometry
        // instead of being depth-tested against it, since World Space canvases share the scene depth buffer.
        private void ApplyAlwaysOnTop(Transform canvasRoot)
        {
            if (canvasRoot == null) return;
            foreach (var graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text tmpText)
                {
                    ApplyAlwaysOnTop(tmpText);
                    continue;
                }
                if (alwaysOnTopUIShader == null) continue;
                var mat = new Material(alwaysOnTopUIShader) { renderQueue = 4000 };
                graphic.material = mat;
                _alwaysOnTopMaterials.Add(mat);
            }
        }

        private void ApplyAlwaysOnTop(TMP_Text text)
        {
            if (text == null || alwaysOnTopFontShader == null) return;
            var mat = new Material(text.fontSharedMaterial) { shader = alwaysOnTopFontShader };
            text.fontMaterial = mat;
            _alwaysOnTopMaterials.Add(mat);
        }
        private void UpdateIcon()
        {
            switch(_mapNodeData.type) {
                case NodeType.Skirmish:
                    skirmishFlag.gameObject.SetActive(true);
                    skirmishFlag.SetSkirmishFlag(_race);

                    break;
                case NodeType.Shop:
                    shopGameObject.SetActive(true);
                    break;
                case NodeType.Event:
                    eventGameObject.SetActive(true);
                    break;
                case NodeType.Treasure:
                    treasureGameObject.SetActive(true);
                    break;
                case NodeType.Town:
                    int bookNumber = Application.isPlaying ? CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber : 1;
                    townFlag.SetUp(_mapNodeData.index, bookNumber);
                    townGameObject.SetActive(true);
                    if(Application.isPlaying)
                    {
                        Race townRace = CampaignSaveManager.GenerateTownRace(_mapNodeData.index, bookNumber);
                        townNameText.text = LocalizationManager.Instance.GetText($"{townRace.ToString()}");
                        TownSize townSize = TownSaveData.GenerateTownSize(_mapNodeData.layer);
                        string townSizeColor = townSize switch
                        {
                            TownSize.Village => ColorData.Tier1,
                            TownSize.Castle => ColorData.Tier2,
                            TownSize.City => ColorData.Tier3,
                            _ => ColorData.Tier1
                        };
                        villageGO.SetActive(townSize == TownSize.Village);
                        castleGO.SetActive(townSize == TownSize.Castle);
                        cityGO.SetActive(townSize == TownSize.City);
                        string townSizeString = LocalizationManager.Instance.GetText(townSize.ToString());
                        townSizeText.text = $"<color={townSizeColor}>[{townSizeString}]</color>";
                    }
                    break;
                case NodeType.Horde:
                    hordeFlag.gameObject.SetActive(true);
                    hordeFlag.SetHordeFlag(_race);
                    break;
                case NodeType.Games:
                    if (tavernGameObject != null) tavernGameObject.SetActive(true);
                    break;
                case NodeType.Campfire:
                    if (campfireGameObject != null) campfireGameObject.SetActive(true);
                    break;
            }
            
            if (Application.isPlaying)
            {
                weatherFlag.SetActive(_weather != Weather.ClearSkies);
                if (_weather != Weather.ClearSkies)
                {
                    SetMiniSkirmishWeather(GetWeatherSprite(_weather));
                    weatherFlagText.text = LocalizationManager.Instance.GetText(_weather.ToString());
                }

                biomeFlag.SetActive(_biome != Biome.Plains);
                if (_biome != Biome.Plains)
                {
                    SetMiniSkirmishBiome(GetBiomeSprite(_biome));
                    biomeFlagText.text = LocalizationManager.Instance.GetText(_biome.ToString());
                }
            }
        }
        private Sprite GetWeatherSprite(Weather weather) => weather switch
        {
            Weather.Rain => weatherIconRain,
            Weather.Fog  => weatherIconFog,
            Weather.Snow => weatherIconSnow,
            _            => null
        };
        private Sprite GetBiomeSprite(Biome biome) => biome switch
        {
            Biome.Forest => biomeIconForest,
            Biome.River  => biomeIconRiver,
            Biome.Swamp  => biomeIconSwamp,
            _            => null
        };
        public void SelectNodeLayer()
        {
            // Debug.Log($"[Map] Node {Value.index} ({Value.type}) layer {Value.layer} is now selectable");
            mMF_Player.PlayFeedbacks();
            selectionParticles.SetActive(true);
            selectable = true;
            outline.enabled = true;
            // if(Value.type == NodeType.Town && !surprise) {
            //     icon.gameObject.SetActive(false);
            // } else if(Value.type == NodeType.Horde) {
            //     icon.gameObject.SetActive(false);
            // }
        }
        public void DeselectNodeLayer()
        {
            // Debug.Log($"Deselecting {Value.index}");
            outline.enabled = false;
            mMF_Player.StopFeedbacks();
            selectionParticles.SetActive(false);
            iconTransform.localScale = Vector3.one;
        }
        public void NodeClicked()
        {
            if(completed) return;
            Debug.Log($"[Map] NodeClicked — index {Value.index} ({Value.type}) layer {Value.layer}");

            mMF_Player.StopFeedbacks();
            iconTransform.localScale = Vector3.one;
            surpriseGameObject.SetActive(false);
            UpdateIcon();
        }
        // A Town hosts its garrison battle on this same node, so it rolls the same weather a Skirmish
        // or Horde does and gets the same readout before the player commits to fighting.
        private static bool NodeLeadsToBattle(NodeType type) =>
            type == NodeType.Skirmish || type == NodeType.Horde || type == NodeType.Town;
        private void HoverWeatherFlag(bool _hover)
        {
            if (_weather == Weather.ClearSkies) return;
            if (_hover) { mouseOffWeatherMMF_Player.StopFeedbacks(); mouseOverWeatherMMF_Player.PlayFeedbacks(); }
            else        { mouseOverWeatherMMF_Player.StopFeedbacks(); mouseOffWeatherMMF_Player.PlayFeedbacks(); }
        }
        public void HoverNode(bool _hover)
        {
            if(!surprise && NodeLeadsToBattle(_mapNodeData.type)) {
                CampaignManager.Instance.MapSceneUIManager.HUDPanel.ShowWeatherHover(_weather, _hover);
            }
            if(completed) return;

            // iconHover.gameObject.SetActive(_hover);
            // if(Value.type == NodeType.Town && !surprise) {
            //     iconHover.gameObject.SetActive(false);
            // } else if(Value.type == NodeType.Horde) {
            //     iconHover.gameObject.SetActive(false);
            // }
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.ShowHoveredNodeText(_mapNodeData.type, surprise, _hover);

            if (_mapNodeData.type == NodeType.Town && !surprise)
            {
                if (_hover)
                {
                    mouseOffTownMMF_Player.StopFeedbacks();
                    mouseOverTownMMF_Player.PlayFeedbacks();
                }
                else
                {
                    mouseOverTownMMF_Player.StopFeedbacks();
                    mouseOffTownMMF_Player.PlayFeedbacks();
                }
                // No biome flag: a garrison battle forces Biome.Plains regardless of the node roll.
                HoverWeatherFlag(_hover);
                return;
            }

            HoverWeatherFlag(_hover);
            if (_biome != Biome.Plains)
            {
                if (_hover) { mouseOffBiomeMMF_Player.StopFeedbacks(); mouseOverBiomeMMF_Player.PlayFeedbacks(); }
                else        { mouseOverBiomeMMF_Player.StopFeedbacks(); mouseOffBiomeMMF_Player.PlayFeedbacks(); }
            }

            if (_hover && selectable)
            {
                IAudioRequester.Instance.PlaySFX(SFXData.MouseOverNode);
                outline.OutlineWidth = 5f;
                outline.OutlineColor = hoverColor;
            }
            else
            {
                outline.OutlineWidth = 2f;
                outline.OutlineColor = defaultColor;
            }
        }
        public void ShowPassed()
        {
            outline.enabled = false;
            mMF_Player.StopFeedbacks();
            iconTransform.localScale = Vector3.one;
            // Debug.Log($"Showing passed node {Value.index}");

            selectable = false;
            // icon.color = passedColor;
            // iconHover.gameObject.SetActive(false);
            completed = true;
            _mapNodeBase.material = passedMaterial;
            foreach(Line line in _mapNodeData.connectedNodeLines) {
                line.Color = passedColor;
            }
        }
        public void ShowCompleted(List<int> completedPath, bool activeLayer)
        {
            // icon.color = completedColor;
            // completedIcon.enabled = true;
            // iconHover.gameObject.SetActive(true);
            _mapNodeBase.material = defaultMaterial;
            selectable = false;

            mMF_Player.StopFeedbacks();
            iconTransform.localScale = Vector3.one;
            surpriseGameObject.SetActive(false);

            // Debug.Log($"Checking node {Value.index} for connected nodes");

            for (int i = 0; i < _mapNodeData.connectedNodeIndexes.Count; i++)
            {
                if (completedPath.Contains(_mapNodeData.connectedNodeIndexes[i]))
                {
                    // Debug.Log($"Node {Value.index} is connected to {Value.connectedNodeIndexes[i]}");
                    _mapNodeData.connectedNodeLines[i].Color = pathCompletedColor;
                }
                else if (activeLayer)
                {
                    _mapNodeData.connectedNodeLines[i].Color = completedColor;
                }
                else
                {
                    _mapNodeData.connectedNodeLines[i].Color = passedColor;
                }
            }
            UpdateIcon();
        }
        public void ResetNode()
        {
            // Debug.Log($"Resetting node {Value.index}");
            mMF_Player.StopFeedbacks();
            iconTransform.localScale = Vector3.one;
            // icon.color = defaultColor;
            // completedIcon.enabled = false;
            // iconHover.gameObject.SetActive(false);
            completed = false;
            selectable = false;
            _mapNodeBase.material = defaultMaterial;
            for(int i = 0; i < _mapNodeData.connectedNodeLines.Count; i++) {
                _mapNodeData.connectedNodeLines[i].Color = completedColor;
            }
        }
        public void Reveal()
        {
            if (!surprise) return;
            surprise = false;
            surpriseGameObject.SetActive(false);
            UpdateIcon();
            RegisterFlagTransform();
        }
        private Material _biomeMaterial;
        private Material _weatherMaterial;
        private readonly List<Material> _alwaysOnTopMaterials = new();

        private void OnDestroy()
        {
            var manager = MapNodeFacingManager.Instance;
            if (manager != null)
            {
                manager.Unregister(skirmishFlag.transform);
                manager.Unregister(townFlag.transform);
                manager.Unregister(hordeFlag.transform);
                manager.Unregister(townTextCanvas);
            }
            if (_biomeMaterial != null) Destroy(_biomeMaterial);
            if (_weatherMaterial != null) Destroy(_weatherMaterial);
            foreach (var mat in _alwaysOnTopMaterials)
            {
                if (mat != null) Destroy(mat);
            }
        }

        public void SetMiniSkirmishBiome(Sprite biomeSprite)
        {
            if (_biomeMaterial != null) Destroy(_biomeMaterial);
            _biomeMaterial = new (miniSkirmishBaseMaterial);
            _biomeMaterial.SetTexture("_IconSprite", biomeSprite.texture);
            biomeFlag.GetComponent<MeshRenderer>().material = _biomeMaterial;
        }
        public void SetMiniSkirmishWeather(Sprite weatherSprite)
        {
            if (_weatherMaterial != null) Destroy(_weatherMaterial);
            _weatherMaterial = new (miniSkirmishBaseMaterial);
            _weatherMaterial.SetTexture("_IconSprite", weatherSprite.texture);
            weatherFlag.GetComponent<MeshRenderer>().material = _weatherMaterial;
        }
        public void PlayMouseOverSFXPrefab()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
        }
    }
}
