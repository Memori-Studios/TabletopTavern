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
            animator.SetTrigger("Disabled");
            button.onClick.RemoveListener(GameSpeedButtonButtonClicked);
        }
}
}
