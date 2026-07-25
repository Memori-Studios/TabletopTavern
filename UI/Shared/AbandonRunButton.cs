using UnityEngine;
using UnityEngine.UI;
using Memori.SaveData;
using TabletopTavern.Analytics;

namespace TJ
{
    public class AbandonRunButton : MonoBehaviour
    {
        [SerializeField] private CampaignSaveManager campaignSaveManager;
        SettingsManager settingsManager;
        [SerializeField] private Button button;
        public void SetUp(SettingsManager _settingsManager)
        {
            settingsManager = _settingsManager;
            button.onClick.AddListener(AbandonRun);
        }
        public void AbandonRun()
        {
            campaignSaveManager = FindFirstObjectByType<CampaignSaveManager>();

            if (SaveDataHandler.CampaignSaveExists())
            {
                var abandonedSave = SaveDataHandler.Load();
                GameEventTracker.RunEnded(abandonedSave.heroID, (int)abandonedSave.difficultyLevel, RunResult.Abandon, abandonedSave.RunStats.chaptersCompleted);
            }
            
            if(campaignSaveManager == null) 
            {
                SaveDataHandler.DeleteCampaignSave();
            } else {
                // #if UNITY_EDITOR
                //     campaignSaveManager.OverrideCampaignSave();
                // #else
                    campaignSaveManager.DeleteCampaignSave();
                // #endif
            }
            settingsManager.AbandonRun();
        }
        
    }
}
