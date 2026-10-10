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
    public class NewsletterUnlock : MonoBehaviour
    {
        [SerializeField] private MemoriButtonV2 enterButton, cancelButton;
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private PlayPanel playPanel;

        MemoriCanvasGroup memoriCanvasGroup;

        string correctCode = "muchothanks";
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
            if(!saveData.unlockConditionsCompleted.Contains(UnlockCondition.NewsletterExclusive)){
                saveData.unlockConditionsCompleted.Add(UnlockCondition.NewsletterExclusive);
            }
            SaveDataHandler.SavePlayerSaveData(saveData);
            playPanel.ReloadHeroOnNewsletterUnlock();

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
