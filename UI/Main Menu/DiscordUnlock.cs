using UnityEngine;
using Memori.UI;
using TMPro;
using UnityEngine.UI;
using Memori.Notifications;
using Memori.SaveData;
using Memori.Utilities;

namespace TJ.MainMenu
{
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class DiscordUnlock : MonoBehaviour
    {
        [SerializeField] private MemoriButtonV2 enterButton, cancelButton;
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private PlayPanel playPanel;
        [SerializeField] private Link links;
        MemoriCanvasGroup memoriCanvasGroup;

        string correctCode = "thanks";
        void Start()
        {
            enterButton.Button.onClick.AddListener(OnSubmitCode);
            cancelButton.Button.onClick.AddListener(OnCancel);

            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            memoriCanvasGroup.CGDisable();
        }
        public void ShowPanel()
        {
            codeInputField.text = string.Empty;
            enterButton.Button.interactable = true;
            cancelButton.Button.interactable = true;
            memoriCanvasGroup.CGEnable();
        }
        public void OnSubmitCode()
        {
            string enteredCode = codeInputField.text.Trim().ToLower();
            if (enteredCode == correctCode)
            {
                CorrectCodeEntered();
                codeInputField.text = string.Empty;
                enterButton.Button.interactable = false;
                cancelButton.Button.interactable = false;
            }
            else
            {
                NotificationManager.Instance.ErrorNotification("Incorrect code. Please try again.");
            }
        }
        public void OnCancel()
        {
            codeInputField.text = string.Empty;
            memoriCanvasGroup.CGDisable();
        }
        private void CorrectCodeEntered()
        {
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.gameCompletions++;
            if(!saveData.unlockConditionsCompleted.Contains(UnlockCondition.DiscordExclusive)){
                saveData.unlockConditionsCompleted.Add(UnlockCondition.DiscordExclusive);
            }
            SaveDataHandler.SavePlayerSaveData(saveData);
            playPanel.ReloadHeroOnDiscordUnlock();

            NotificationManager.Instance.ErrorNotification("Hero unlocked!");
            memoriCanvasGroup.CGDisable();
        }
        private void OnDestroy()
        {
            enterButton.Button.ClearClickListeners();
            cancelButton.Button.ClearClickListeners();
        }
    }
}
