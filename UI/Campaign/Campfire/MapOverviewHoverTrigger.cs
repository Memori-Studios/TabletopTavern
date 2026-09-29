using TJ.Map;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.Campfire
{
    public class MapOverviewHoverTrigger : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private MapOverviewPanel mapOverviewPanel;
        [SerializeField] private MapSceneUIManager mapSceneUIManager;

        // A trigger inside a prefab cannot reference the scene's panel, so its owner passes it in.
        public void SetUp(MapOverviewPanel _mapOverviewPanel, MapSceneUIManager _mapSceneUIManager)
        {
            mapOverviewPanel = _mapOverviewPanel;
            mapSceneUIManager = _mapSceneUIManager;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            mapOverviewPanel.Open(
                mapSceneUIManager.MapSceneManager.MapLayers,
                CampaignManager.Instance.CampaignSaveManager.SaveData,
                mapSceneUIManager.LayerNodeSelected);
        }
    }
}
