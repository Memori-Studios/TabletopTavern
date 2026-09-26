using UnityEngine;
using UnityEngine.UI;
using Memori.Scenes;
using System.Threading.Tasks;
using Memori.Utilities;
using Unity.Scenes;
using Unity.Entities;
using System.Collections.Generic;
using System;

namespace TJ.Battle
{
public class ExitBattle : MonoBehaviour
{
    [SerializeField] private Button returnToMainMenuButton, cancelExitButton;
    MemoriCanvasGroup panelCanvasGroup;

    private void Awake()
    {
        panelCanvasGroup = GetComponent<MemoriCanvasGroup>();

        returnToMainMenuButton.onClick.RemoveAllListeners();
        returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        cancelExitButton.onClick.RemoveAllListeners();
        cancelExitButton.onClick.AddListener(CancelExit);
    }
    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.Escape)) {
    //         if(panelCanvasGroup.alpha == 1) {
    //             CancelExit();
    //         } else {
    //             cachedTimeScale = Time.timeScale;
    //             Time.timeScale = 0;
    //             panelCanvasGroup.CGEnable();
    //         }
    //     }
    // }
    public void OpenExitPanel()
    {
        panelCanvasGroup.CGEnable();
    }
    private void ReturnToMainMenu()
    {
        BattleManager.Instance.BattleCleanUpManager.UnloadSubscene();
        SceneHandler.Instance.ExitCustomBattle();
    }
    private void CancelExit()
    {
        panelCanvasGroup.CGDisable();
    }
    
}
}