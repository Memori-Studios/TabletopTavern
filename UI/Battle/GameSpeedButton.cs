using Memori.Localization;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TJ
{
[RequireComponent(typeof(Button))]
public class GameSpeedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject highlight, selected, disabled;
    GameSpeedManager gameSpeedManager;
    float gameSpeed;
    public float GameSpeed => gameSpeed;
    Button button;
    Animator animator;
    private void Awake()
    {
        button = GetComponent<Button>();
        animator = GetComponent<Animator>();
        button.onClick.AddListener(GameSpeedButtonButtonClicked);
        highlight.SetActive(false);
        selected.SetActive(false);
        MemoriTooltipTrigger trigger = GetComponent<MemoriTooltipTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<MemoriTooltipTrigger>();
        trigger.SetContentProvider(BuildHotkeyTooltip);
        // disabled.SetActive(true);
        // button.interactable = false;
        // animator.SetTrigger("Disabled");
    }
    public void SetUpGameSpeedButton(GameSpeedManager _gameSpeedManager, float _gameSpeed)
    {
        gameSpeedManager = _gameSpeedManager;
        gameSpeed = _gameSpeed;

        if(gameSpeed == 1){
            selected.SetActive(true);
        }

        disabled.SetActive(false);
        button.interactable = true;
        animator.SetTrigger("Normal");
    }
    public void GameSpeedButtonButtonClicked()
    {
        // Debug.Log($"GameSpeedButton clicked: {gameSpeed}");
        gameSpeedManager.PlayerSetTimeScale(this);
    }
    public void Select()
    {
        selected.SetActive(true);
        animator.SetTrigger("Pressed");
    }
    public void Deselect()
    {
        selected.SetActive(false);
        // Any -> Pressed cannot fire into itself, so a repeat Select leaves the trigger armed to re-press this button later.
        animator.ResetTrigger("Pressed");
        animator.SetTrigger("Normal");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        highlight.SetActive(true);
    }
        public void OnPointerExit(PointerEventData eventData)
        {
            highlight.SetActive(false);
        }
        public void Lock()
        {
            disabled.SetActive(true);
            button.interactable = false;
            selected.SetActive(false);
            animator.ResetTrigger("Pressed");
            animator.SetTrigger("Disabled");
            button.onClick.RemoveListener(GameSpeedButtonButtonClicked);
        }

        // Built on each open so a rebind or language change shows without a battle reload.
        private static TooltipContent BuildHotkeyTooltip()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string Key(string action) => $"' {BattleGuideKeys.ToSearchText("@" + action).Trim()} '";
            return new TooltipContent
            {
                Title = loc.GetText("GameSpeedHotkeysTitle"),
                Body = $"{Key("PauseGame")}  {loc.GetText("GameSpeedPauseResume")}\n"
                     + $"{Key("SpeedDown")}  {loc.GetText("GameSpeedSlowDown")}\n"
                     + $"{Key("SpeedUp")}  {loc.GetText("GameSpeedSpeedUp")}",
            };
        }
}
}
