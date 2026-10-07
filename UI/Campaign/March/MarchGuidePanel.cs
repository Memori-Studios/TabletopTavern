using System;
using System.Collections;
using System.Collections.Generic;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.UI;
using TJ.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.March
{
    /// <summary>
    /// The rules of the March, taught on the map itself when each stretch begins: the map dims, spotlights pick out the
    /// Twist labels, the law pips, the army and the gold, and a callout explains each. Shown until the player ticks
    /// "Don't show this again".
    /// </summary>
    public class MarchGuidePanel : MonoBehaviour
    {
        // One spotlight: the hole cut in the dim, its ring, the callout and the line joining them.
        [Serializable]
        private class Mark
        {
            public RectTransform hole;
            public RectTransform ring;
            public RectTransform callout;
            public RectTransform line;
            public TMP_Text title;
            public TMP_Text body;
            public Side side;
            // Moves the callout off a neighbouring mark, in canvas units.
            public Vector2 nudge;
        }
        private enum Side { Left, Right, Above }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text cardLine;
        [SerializeField] private Toggle hideToggle;
        [SerializeField] private GameObject checkMark;
        [SerializeField] private TMP_Text hideLabel;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private Mark twist, laws, army, gold;
        // HUD pieces the marks point at; the Twist mark finds its node on the map.
        [SerializeField] private RectTransform lawsTarget, armyTarget, goldTarget;
        [SerializeField] private float holePadding = 10f;
        [SerializeField] private float minTwistRadius = 100f;
        [SerializeField] private float calloutGap = 70f;
        // Above the squad card counts (101), level with the Ordeal picker it hands over to.
        [SerializeField] private int sortingOrder = 106;
        [SerializeField] private float markGap = 0.12f;

        Action onClosed;
        bool open;
        RectTransform root;
        MapNode twistNode;
        Camera mapCamera;
        readonly Vector3[] corners = new Vector3[4];

        public static bool Hidden => SaveDataHandler.LoadPlayerSaveData().hideMarchGuide;

        private void Awake()
        {
            root = (RectTransform)transform;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            hideToggle.onValueChanged.AddListener(OnToggled);
            continueButton.onClick.AddListener(Close);
            ContainedNavigation.Attach(gameObject);
        }

        public void Open(Action _onClosed)
        {
            onClosed = _onClosed;
            open = true;
            Canvas canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            // The stencil materials that cut the spotlights are stale after the sorting change until rebuilt.
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.SetMaterialDirty();

            FillText();
            FindTwistNode();
            hideToggle.SetIsOnWithoutNotify(false);
            checkMark.SetActive(false);
            Layout();

            group.blocksRaycasts = true;
            group.interactable = true;
            StartCoroutine(UIJuice.Open(group, null));
            var items = new List<RectTransform> { card };
            foreach (Mark mark in new[] { laws, twist, army, gold })
                if (mark.callout.gameObject.activeSelf) items.Add(mark.callout);
            StartCoroutine(UIJuice.Stagger(items, markGap));
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }

        private void FillText()
        {
            title.text = Text("marchGuideTitle");
            subtitle.text = Text("marchIntroEndless");
            cardLine.text = string.Format(Text("marchGuideCard"), MarchRules.WARLORD_EVERY, Mathf.RoundToInt(OrdealRegistry.RENOWN_BONUS_PER_ORDEAL * 100f));
            laws.title.text = Text("marchGuideLawsTitle");
            laws.body.text = Text("marchGuideLaws");
            twist.title.text = Text("marchTwists");
            twist.body.text = Text("marchGuideTwistsCall");
            army.title.text = Text("marchGuideArmyTitle");
            army.body.text = Text("marchGuideArmy");
            gold.title.text = Text("marchGuideGoldTitle");
            gold.body.text = string.Format(Text("marchGuideGoldCall"), MarchRules.STRIKE_TWIST_BASE_COST, MarchRules.STRIKE_TWIST_COST_RISE);
            hideLabel.text = Text("dontShowAgain");
            continueLabel.text = Text("continueButton");
        }

        // The node the player can pick next with a Twist showing; under Fog there may be none.
        private void FindTwistNode()
        {
            twistNode = null;
            mapCamera = null;
            MapSceneManager map = FindFirstObjectByType<MapSceneManager>();
            if (map == null) return;
            mapCamera = map.MapScecneCamera;
            foreach (MapNode node in FindObjectsByType<MapNode>(FindObjectsSortMode.None))
                if (node.Selectable && node.TwistLabel != null) { twistNode = node; break; }
        }

        // The HUD slides in as the guide opens, so the marks follow their targets every frame.
        private void LateUpdate()
        {
            if (open) Layout();
        }

        private void Layout()
        {
            Place(laws, LocalRect(lawsTarget), false);
            Place(army, LocalRect(armyTarget), false);
            Place(gold, LocalRect(goldTarget), false);

            bool showTwist = twistNode != null && mapCamera != null && twistNode.TwistLabel != null;
            SetMarkActive(twist, showTwist);
            if (showTwist)
            {
                Vector2 label = ScreenToLocal(mapCamera.WorldToScreenPoint(twistNode.TwistLabel.position));
                Vector2 flagBase = ScreenToLocal(mapCamera.WorldToScreenPoint(twistNode.transform.position));
                Vector2 centre = (label + flagBase) / 2f;
                float radius = Mathf.Max(minTwistRadius, Vector2.Distance(label, flagBase) / 2f + 45f);
                Place(twist, new Rect(centre - Vector2.one * radius, Vector2.one * radius * 2f), true);
            }
        }

        private void Place(Mark mark, Rect target, bool round)
        {
            if (target.width <= 0f) { SetMarkActive(mark, false); return; }
            SetMarkActive(mark, true);
            Rect padded = round ? target : new Rect(target.x - holePadding, target.y - holePadding, target.width + holePadding * 2f, target.height + holePadding * 2f);
            foreach (RectTransform r in new[] { mark.hole, mark.ring })
            {
                r.anchoredPosition = padded.center;
                r.sizeDelta = padded.size;
            }

            Vector2 size = mark.callout.rect.size;
            Vector2 at = mark.side switch
            {
                Side.Left => new Vector2(padded.xMin - calloutGap - size.x / 2f, padded.center.y),
                Side.Right => new Vector2(padded.xMax + calloutGap + size.x / 2f, padded.center.y - size.y / 2f),
                _ => new Vector2(padded.center.x, padded.yMax + calloutGap + size.y / 2f),
            } + mark.nudge;
            Rect bounds = root.rect;
            at.x = Mathf.Clamp(at.x, bounds.xMin + size.x / 2f + 16f, bounds.xMax - size.x / 2f - 16f);
            at.y = Mathf.Clamp(at.y, bounds.yMin + size.y / 2f + 16f, bounds.yMax - size.y / 2f - 16f);
            mark.callout.anchoredPosition = at;

            // The line runs from the callout's nearest edge to the target's edge.
            Rect callRect = new Rect(at - size / 2f, size);
            Vector2 start = Clamp(padded.center, callRect);
            Vector2 end = round ? padded.center + (start - padded.center).normalized * padded.width / 2f : Clamp(start, padded);
            Vector2 run = end - start;
            mark.line.anchoredPosition = start;
            mark.line.sizeDelta = new Vector2(run.magnitude, mark.line.sizeDelta.y);
            mark.line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg);
        }

        private static Vector2 Clamp(Vector2 point, Rect rect) => new(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));

        private static void SetMarkActive(Mark mark, bool active)
        {
            foreach (RectTransform r in new[] { mark.hole, mark.ring, mark.callout, mark.line })
                if (r.gameObject.activeSelf != active) r.gameObject.SetActive(active);
        }

        // A HUD element's rect in this panel's space; empty when the element is off.
        private Rect LocalRect(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return Rect.zero;
            target.GetWorldCorners(corners);
            Vector2 min = ScreenToLocal(RectTransformUtility.WorldToScreenPoint(null, corners[0]));
            Vector2 max = ScreenToLocal(RectTransformUtility.WorldToScreenPoint(null, corners[2]));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ScreenToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            return local;
        }

        private void OnToggled(bool isOn)
        {
            checkMark.SetActive(isOn);
            IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            if (hideToggle.isOn)
            {
                PlayerSaveData save = SaveDataHandler.LoadPlayerSaveData();
                save.hideMarchGuide = true;
                SaveDataHandler.SavePlayerSaveData(save);
            }
            group.interactable = false;
            group.blocksRaycasts = false;
            IAudioRequester.Instance.PlaySFX(SFXData.CloseUI);
            StartCoroutine(CloseAfterFade());
        }

        private IEnumerator CloseAfterFade()
        {
            yield return UIJuice.Close(group);
            Action closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);
    }
}
