using UnityEngine;
using Memori.UI;
using UnityEngine.UI;
using Memori.Scenes;
using System.Threading.Tasks;
using Memori.Utilities;
using Memori.Input;

namespace TJ.Map
{
[RequireComponent(typeof(MemoriCanvasGroup))]
public class MapEscapePanel : MonoBehaviour
{
    [SerializeField] private Button returnToMainMenuButton, cancelExitButton, abandonCampaignButton;
    MemoriCanvasGroup panelCanvasGroup;
    
    [Header("Abandon Run")]
    public MemoriCanvasGroup abandonRunConfirmationCanvasGroup;
    [SerializeField] private Button abandonRunYesButton, abandonRunNoButton;
    CampaignSaveManager campaignSaveManager;
    public void SetUp(CampaignSaveManager _campaignSaveManager)
    {
        panelCanvasGroup = GetComponent<MemoriCanvasGroup>();
        campaignSaveManager = _campaignSaveManager;

        returnToMainMenuButton.ClearClickListeners();
        returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);

        cancelExitButton.ClearClickListeners();
        cancelExitButton.onClick.AddListener(CancelExit);

        abandonCampaignButton.ClearClickListeners();
        abandonCampaignButton.onClick.AddListener(AbandonRunConfirmationPopUp);

        abandonRunYesButton.onClick.AddListener(AbandonRun);
        abandonRunNoButton.onClick.AddListener(CancelAbandonRun);
    }
    // private void Update()
    // {
    //     if (InputHandler.Instance.SettingsButtonPressed) {
    //         if(panelCanvasGroup.alpha == 1) {
    //             CancelExit();
    //         } else {
    //             panelCanvasGroup.CGEnable();
    //         }
    //     }
    // }
    private void ReturnToMainMenu()
    {
        SceneHandler.Instance.ExitMap();
        // campaignSaveManager.OverrideCampaignSave();
    }
    private void CancelExit()
    {
        abandonRunConfirmationCanvasGroup.CGDisable();
        panelCanvasGroup.CGDisable();
    }
    private void AbandonRunConfirmationPopUp()
    {
        abandonRunConfirmationCanvasGroup.CGEnable();
    }
    public void AbandonRun()
    {
        campaignSaveManager.DeleteCampaignSave();
        abandonRunConfirmationCanvasGroup.CGDisable();
        ReturnToMainMenu();
    }
    public void CancelAbandonRun()
    {
        abandonRunConfirmationCanvasGroup.CGDisable();
    }
}
}
