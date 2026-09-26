using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Memori.Localization;
using Memori.UI;
using Memori.Tooltip;
using Memori.Audio;

namespace TJ.Battle
{
public class GroupUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [SerializeField] private TMP_Text groupNumberText;
    [SerializeField] private RectTransform groupBackgroundImage;
    [SerializeField] private Image backgroundImage, mainImage;
    [SerializeField] private MemoriButtonV2 lockButton;
    [SerializeField] private MemoriTooltipTrigger lockTooltip;
    [SerializeField] private GameObject lockedIcon, unlockedIcon, lockedHighlight;
    [SerializeField] [Range(0f, 1f)] private float deselectedBrightness = 0.4f;
    GroupManager groupManager;
    int groupID;
    Color _color;
    public int GroupID => groupID;
    public void SetUpGroupUI(int _groupNumber, int _squadCount, GroupManager _groupManager, Color _color, bool isLocked)
    {
        groupManager = _groupManager;
        groupID = _groupNumber;
        this._color = _color;

        // Group 10 is selected with the 0 key, so its badge shows 0.
        groupNumberText.text = _groupNumber == 10 ? "0" : _groupNumber.ToString();
        groupBackgroundImage.sizeDelta = new Vector2((_squadCount*60) + ((_squadCount-1) * 5), 25);
        SetSelected(false);

        ShowLockState(isLocked);
        if (lockTooltip != null)
            lockTooltip.SetUpToolTip(LocalizationManager.Instance.GetText("LockFormation"), LocalizationManager.Instance.GetText("LockFormationDescription"));
        if (lockButton == null) return;

        lockButton.Button.onClick.RemoveAllListeners();
        lockButton.Button.onClick.AddListener(() =>
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            groupManager.ToggleLockGroup(groupID);
        });
    }
    // Locked shows the closed padlock and the bracket outline; unlocked shows the open padlock alone.
    private void ShowLockState(bool isLocked)
    {
        if (lockedIcon != null) lockedIcon.SetActive(isLocked);
        if (unlockedIcon != null) unlockedIcon.SetActive(!isLocked);
        if (lockedHighlight != null) lockedHighlight.SetActive(isLocked);
    }
    public void SetSelected(bool selected)
    {
        Color c = selected ? _color : _color * deselectedBrightness;
        c.a = _color.a;
        backgroundImage.color = c;
        mainImage.color = c;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Debug.Log("Pointer Enter group " + groupID);
        BattleManager.Instance.UnitSelectionManager.HoverSquad(0, true);
        groupManager.HoverGroup(groupID);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        // Debug.Log("Pointer Exit group " + groupID);
        groupManager.UnhoverGroup();
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        // Debug.Log("Pointer Down");
        groupManager.SelectGroup(groupID);
    }
}
}