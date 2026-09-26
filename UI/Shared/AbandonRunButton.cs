using UnityEngine;
using UnityEngine.UI;
using Memori.SaveData;
using Memori.Scenes;
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
                ReportAbandonedBattle(abandonedSave);
                GameEventTracker.RunClosed(abandonedSave, RunResult.Abandon, "abandonInRun");
                SaveDataHandler.RecordAbandonedRun(abandonedSave);
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
        // Walking away mid-battle is the one way a fought battle ends with no result, so it is reported here.
        private static void ReportAbandonedBattle(CampaignSaveData run)
        {
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.Battle || !BattleManager.HasInstance) return;
            if (BattleManager.Instance.BattleSaveManager.IsCustomBattle) return;
            GameEventTracker.BattleEnded(run, GameEventTracker.TryBuild("battleEnded", () => BattleManager.Instance.ArmySpawnManager.BuildAbandonReport()));
        }
        
    }
}
