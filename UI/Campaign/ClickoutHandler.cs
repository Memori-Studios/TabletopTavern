using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))] // Ensures it's on a UI element
public class ClickOutHandler : MonoBehaviour
{
    [Header("Menu Control")]
    [SerializeField] private UnityEvent onMenuClose; // Optional: Wire up custom close logic (e.g., animate out)
    public UnityEvent OnMenuClose => onMenuClose;

    private Canvas canvas; // Cached for performance
    private bool isMenuOpen = true; // Track state to avoid checks when closed

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogError("MenuClickOutsideHandler requires a Canvas parent!");
    }

    private void OnEnable()
    {
        isMenuOpen = true;
    }

    private void Update()
    {
        if (!isMenuOpen) return;

        if (global::Memori.Input.GameCursor.GetButtonDown(0) && EventSystem.current != null)
        {
            if (!IsPointerOverMenu())
            {
                CloseMenu();
            }
        }
    }

    private bool IsPointerOverMenu()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Transform menuTransform = transform;
        foreach (RaycastResult result in results)
        {
            // Check if hit UI element is this menu or any of its children
            if (result.gameObject.transform.IsChildOf(menuTransform) || result.gameObject.transform == menuTransform)
            {
                return true;
            }
        }
        return false;
    }

    public void CloseMenu()
    {
        isMenuOpen = false;
        onMenuClose?.Invoke();
        gameObject.SetActive(false); // Default: Hide the menu
    }

    // Call this to open/re-enable (e.g., from button)
    public void OpenMenu()
    {
        gameObject.SetActive(true);
    }
}