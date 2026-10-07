using System.Collections.Generic;
using UnityEngine;

namespace TJ.Ordeals
{
    /// <summary>The run's Ordeals as a row of icons on the map HUD, each with its tooltip. Hidden while the run has none.</summary>
    public class OrdealStrip : MonoBehaviour
    {
        // Past this, the last icon becomes a "+N" pip that lists the rest.
        private const int MAX_ICONS = 8;

        [SerializeField] private Transform iconParent;
        [SerializeField] private OrdealIcon iconPrefab;

        CampaignSaveManager campaignSaveManager;
        readonly List<OrdealIcon> icons = new();

        public void SetUp(CampaignSaveManager _campaignSaveManager)
        {
            if (campaignSaveManager != null) campaignSaveManager.OnOrdealsChanged -= Refresh;
            campaignSaveManager = _campaignSaveManager;
            campaignSaveManager.OnOrdealsChanged += Refresh;
            Refresh();
        }
        private void OnDestroy()
        {
            if (campaignSaveManager != null) campaignSaveManager.OnOrdealsChanged -= Refresh;
        }
        public void Refresh()
        {
            foreach (OrdealIcon icon in icons)
                if (icon != null) Destroy(icon.gameObject);
            icons.Clear();

            // The March's two laws lead the row, so every greyed potion and gear slot has its reason one hover away.
            List<OrdealId> held = new();
            if (campaignSaveManager.SaveData.InMarch) held.AddRange(OrdealRegistry.MarchLaws);
            if (campaignSaveManager.SaveData.ordeals != null) held.AddRange(campaignSaveManager.SaveData.ordeals);
            gameObject.SetActive(held.Count > 0);

            int shown = held.Count > MAX_ICONS ? MAX_ICONS - 1 : held.Count;
            for (int i = 0; i < shown; i++)
            {
                OrdealIcon icon = Instantiate(iconPrefab, iconParent);
                icon.Load(OrdealRegistry.Get(held[i]), campaignSaveManager.SaveData);
                icons.Add(icon);
            }
            if (held.Count > MAX_ICONS)
            {
                OrdealIcon more = Instantiate(iconPrefab, iconParent);
                more.LoadOverflow(held.GetRange(shown, held.Count - shown));
                icons.Add(more);
            }
        }
    }
}
