using Memori.UI;
using TMPro;
using UnityEngine;
using Memori.SaveData;
using Memori.Audio;
using System.Collections;
using Memori.Localization;
using Memori.Input;

namespace TJ.Map
{
// Which way a callout opens from its target; TowardCenter keeps it on screen for any target.
public enum CalloutSide { TowardCenter, Left, Right }

public class TutorialManager : Memori.Utilities.Singleton<TutorialManager>
{
    [Header("Active Tutorial Steps")]
    [SerializeField] private TutorialStep[] activeTutorialSteps;
    [SerializeField] private TutorialStepEnum activeStepEnum;
    [SerializeField] private int activeStepNumber;

    [Header("Main Tutorial Panel")]
    [SerializeField] private TMP_Text stepDescription;
    [SerializeField] private Animator tutorialPanelAnimator;
    [SerializeField] private MemoriButtonV2 completeStepButton;
    // [SerializeField] private ImageHighlighter closeButtonHighlighter;

    [Header("Tooltip")]
    [SerializeField] private Animator tutorialTooltipAnimator;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private MemoriButtonV2 completeTooltipButton;
    [SerializeField] private Transform tutorialTooltipPlacementTransform;

    [Header("Scene")]
    [SerializeField] private GameObject sceneGameObjects;

    // Gap between the callout's frame and the element it points at, in canvas units.
    private const float TOOLTIP_GAP = 25f;
    // Text margin on the close button's side, in text units, so a long last line never runs under it.
    private const float TOOLTIP_CLOSE_BUTTON_CLEARANCE = 130f;
    private Vector4 _tooltipTextMargin;
    private Coroutine _cameraTutorial;
    private Coroutine _placeTooltip;
    // Reused every frame the callout follows its target, so placement allocates nothing.
    private readonly System.Collections.Generic.List<UnityEngine.UI.Graphic> _tooltipGraphics = new();
    private readonly Vector3[] _frameCorners = new Vector3[4];
    private bool _stepPanelOpen;
    // True from the moment a step chain loads until it finishes or is turned off, so optional tips can wait their turn.
    public bool IsShowingStep => _stepPanelOpen;

    private void Start()
    {
        tutorialPanelAnimator.gameObject.SetActive(false);
        tutorialTooltipAnimator.gameObject.SetActive(false);
        // Long translations must shrink to fit the callout, as they do in the main panel.
        tooltipText.enableAutoSizing = true;
        tooltipText.fontSizeMin = stepDescription.fontSizeMin;
        tooltipText.fontSizeMax = tooltipText.fontSize;
        _tooltipTextMargin = tooltipText.margin;

        completeStepButton.Button.onClick.AddListener(LocalCompleteStep);
        completeTooltipButton.Button.onClick.AddListener(CloseTooltip);
    }
    public void DelayedStart()
    {
        PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
        if(saveData.tutorialStepCompleted.Contains(TutorialData.MoveCamera.stepID)) return;

        if (_cameraTutorial != null) StopCoroutine(_cameraTutorial);
        _cameraTutorial = StartCoroutine(CameraTutorial());
    }
    // Reads the same InputHandler values MapCamera moves on, so rebound keys still complete the steps.
    private IEnumerator CameraTutorial()
    {
        yield return new WaitForSecondsRealtime(3f);

        LoadStepsFromRandomSpot(new TutorialStep[3] { TutorialData.MoveCamera, TutorialData.RotateCamera, TutorialData.SelectNode });
        yield return new WaitUntil(() => activeStepEnum == TutorialStepEnum.MoveCamera);
        yield return new WaitUntil(() => activeStepEnum != TutorialStepEnum.MoveCamera
            || InputHandler.Instance.MoveX != 0f || InputHandler.Instance.MoveZ != 0f);
        CompleteStepCheck(TutorialStepEnum.MoveCamera);
        yield return new WaitUntil(() => activeStepEnum != TutorialStepEnum.RotateCamera
            || InputHandler.Instance.EnableCameraRotation_Input);
        CompleteStepCheck(TutorialStepEnum.RotateCamera);
        _cameraTutorial = null;
    }
    public void LoadStepsFromRandomSpot(TutorialStep[] _tutorialSteps)
    {
        PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
        if(saveData.tutorialStepCompleted.Contains(_tutorialSteps[^1].stepID)) return;

        stepDescription.text = "";
        completeStepButton.gameObject.SetActive(false);

        activeTutorialSteps = _tutorialSteps;
        _stepPanelOpen = true;
        tutorialPanelAnimator.gameObject.SetActive(true);
        tutorialPanelAnimator.SetBool("Active", true);

        activeStepNumber = 0;
        StartCoroutine(DelayedLoadText());
    }
    public IEnumerator DelayedLoadText()
    {
        yield return new WaitForSeconds(0.5f);
        LoadStep();
    }
    public void LoadStep()
    {
        stepDescription.text = LocalizationManager.Instance.GetText($"tutorialStep{activeTutorialSteps[activeStepNumber].stepID}Desc");
        activeStepEnum = activeTutorialSteps[activeStepNumber].tutorialStepEnum;

        completeStepButton.gameObject.SetActive(true);
        // closeButtonHighlighter.FlashHighlightImage();
 
        sceneGameObjects.SetActive(true);
    }
    public void LoadTooltip(TutorialStep _tutorialStep, Transform _tooltipPlacementTransform, CalloutSide _side, params object[] _formatArgs)
    {
        PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
        if(saveData.tutorialStepCompleted.Contains(_tutorialStep.stepID)) return;

        saveData.tutorialStepCompleted.Add(_tutorialStep.stepID);
        SaveDataHandler.SavePlayerSaveData(saveData);

        string text = LocalizationManager.Instance.GetText($"tutorialStep{_tutorialStep.stepID}Desc");
        tooltipText.text = _formatArgs.Length > 0 ? string.Format(text, _formatArgs) : text;

        // Activate before setting the bool: enabling an Animator resets its parameters.
        tutorialTooltipAnimator.gameObject.SetActive(true);
        tutorialTooltipAnimator.SetBool("Active", true);
        completeTooltipButton.gameObject.SetActive(true);
        if (_placeTooltip != null) StopCoroutine(_placeTooltip);
        _placeTooltip = StartCoroutine(FollowTooltipTarget(_tooltipPlacementTransform, _side));
    }
    // Re-places the callout every frame it is open: a layout group can move the target after it appears,
    // and the open animation grows the frame, so the near corner is pinned rather than measured once.
    private IEnumerator FollowTooltipTarget(Transform _target, CalloutSide _side)
    {
        Canvas targetCanvas = _target != null ? _target.GetComponentInParent<Canvas>() : null;
        Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
        while (_target != null && completeTooltipButton.gameObject.activeSelf)
        {
            PlaceTooltip(RectTransformUtility.WorldToScreenPoint(targetCamera, _target.position), _side);
            yield return null;
        }
        _placeTooltip = null;
    }
    private void PlaceTooltip(Vector2 _target, CalloutSide _side)
    {
        tutorialTooltipPlacementTransform.position = _target;

        bool openLeft = _side == CalloutSide.Left || (_side == CalloutSide.TowardCenter && _target.x > Screen.width * 0.5f);
        bool openDown = _target.y > Screen.height * 0.5f;
        RectTransform box = (RectTransform)tutorialTooltipAnimator.transform;
        box.pivot = new Vector2(openLeft ? 1f : 0f, openDown ? 1f : 0f);
        box.anchoredPosition = Vector2.zero;

        // Keep the close button at the far end of the box so it never covers the target.
        RectTransform closeButton = (RectTransform)completeTooltipButton.transform;
        Vector2 closePosition = closeButton.anchoredPosition;
        closeButton.anchoredPosition = new Vector2(Mathf.Abs(closePosition.x) * (openLeft ? -1f : 1f), closePosition.y);
        Vector4 margin = _tooltipTextMargin;
        if (openLeft) margin.x = Mathf.Max(margin.x, TOOLTIP_CLOSE_BUTTON_CLEARANCE);
        else margin.z = Mathf.Max(margin.z, TOOLTIP_CLOSE_BUTTON_CLEARANCE);
        tooltipText.margin = margin;

        // The frame art overhangs the box rect, so measure it and put its near corner TOOLTIP_GAP from the target.
        Rect frame = VisibleFrame(box, closeButton);
        float gap = TOOLTIP_GAP * tutorialTooltipPlacementTransform.lossyScale.x;
        float dx = openLeft ? _target.x - gap - frame.xMax : _target.x + gap - frame.xMin;
        float dy = openDown ? _target.y - gap - frame.yMax : _target.y + gap - frame.yMin;
        box.position += new Vector3(dx, dy, 0f);
    }
    private Rect VisibleFrame(RectTransform _box, Transform _exclude)
    {
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        _box.GetComponentsInChildren(_tooltipGraphics);
        foreach (UnityEngine.UI.Graphic graphic in _tooltipGraphics)
        {
            if (graphic.transform.IsChildOf(_exclude)) continue;
            graphic.rectTransform.GetWorldCorners(_frameCorners);
            Vector3[] corners = _frameCorners;
            xMin = Mathf.Min(xMin, corners[0].x);
            yMin = Mathf.Min(yMin, corners[0].y);
            xMax = Mathf.Max(xMax, corners[2].x);
            yMax = Mathf.Max(yMax, corners[2].y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
    public void CompleteStepCheck(TutorialStepEnum _tutorialStepEnum)
    {
        if(_tutorialStepEnum != activeStepEnum) return;

        LocalCompleteStep();
    }
    private void LocalCompleteStep()
    {
        PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
        saveData.tutorialStepCompleted.Add(activeTutorialSteps[activeStepNumber].stepID);
        SaveDataHandler.SavePlayerSaveData(saveData);

        activeStepNumber++;
        completeStepButton.gameObject.SetActive(false);
        stepDescription.text = "";

        if(activeStepNumber == activeTutorialSteps.Length) {
            tutorialPanelAnimator.SetBool("Active", false);
            activeStepEnum = TutorialStepEnum.Blank;
            _stepPanelOpen = false;
            // The stage only feeds this panel's portrait; left on, its camera renders every frame.
            sceneGameObjects.SetActive(false);
            return;
        }

        tutorialPanelAnimator.SetBool("Next", true);
        LoadStep();
    }
    public void CloseTooltip()
    {
        if (!tutorialTooltipAnimator.gameObject.activeSelf || !completeTooltipButton.gameObject.activeSelf) return;
        tutorialTooltipAnimator.SetBool("Active", false);
        completeTooltipButton.gameObject.SetActive(false);
    }
    public void TurnOff()
    {
        _stepPanelOpen = false;
        tutorialPanelAnimator.SetBool("Active", false);
        tutorialTooltipAnimator.SetBool("Active", false);
        sceneGameObjects.SetActive(false);
        completeTooltipButton.gameObject.SetActive(false);
        completeStepButton.gameObject.SetActive(false);
    }
}
}
