using System.Collections;
using UnityEngine;
using TMPro;
using QuickOutline;
using Memori.UI;

namespace TJ.Shop
{
    // A refused purchase shakes the shelf item's model too; the hover feedbacks own the item's root, so the model moves.
    public static class ShopRefusal
    {
        const float AmplitudeOfWidth = 0.08f, Duration = 0.25f;

        // Sideways on screen: the shelf models face the shop camera, so their own axes can point at it.
        public static IEnumerator ShakeModel(Transform model)
        {
            if (model == null) yield break;
            Camera camera = CampaignManager.Instance.MapCamera.ShopCamera;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Bounds body = default;
            bool measured = false;
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
            {
                if (!measured) { body = part.bounds; measured = true; }
                else body.Encapsulate(part.bounds);
            }
            float width = measured ? Mathf.Max(body.size.x, body.size.z) : 0.3f;
            float amplitude = AmplitudeOfWidth * width;
            Vector3 rest = model.position;
            for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
            {
                model.position = rest + right * (amplitude * Mathf.Sin(t * 40f) * (1f - t / Duration));
                yield return null;
            }
            model.position = rest;
        }
    }

    [RequireComponent(typeof(Canvas))]
    public class ShopPriceCanvas : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI priceText;
        private Outline outline;
        private Canvas canvas;
        GoldManager goldManager;
        // Embargo: the price is struck through, so nothing may parse the label as a number.
        bool soldOut;
        private void Start()
        {
            canvas = GetComponent<Canvas>();
            canvas.worldCamera = CampaignManager.Instance.MapCamera.ShopCamera;
            goldManager = CampaignManager.Instance.GoldManager;
            goldManager.OnGoldAmountChanged += UpdateAffordability;
        }
        public void SetSoldOut(bool _soldOut)
        {
            soldOut = _soldOut;
            if (!soldOut) return;
            outline = GetComponentInChildren<Outline>();
            // The label ships with rich text off; the strike-through needs it.
            priceText.richText = true;
            priceText.text = $"<s>{priceText.text}</s>";
            priceText.color = Color.gray;
            outline.OutlineColor = Color.gray;
        }
        public void Refuse()
        {
            // World-space canvas: the shake is sized to the label, not in screen pixels.
            StartCoroutine(UIJuice.Shake(priceText.rectTransform, priceText.rectTransform.rect.width * 0.15f));
        }
        public void SetUp(string price)
        {
            if (soldOut) return;
            outline = GetComponentInChildren<Outline>();
            priceText.text = price;
            bool canAfford = CampaignManager.Instance.GoldManager.CheckIfCanAfford(int.Parse(priceText.text));
            Color color = ColorData.GetColorBasedOnAffordability(canAfford);
            priceText.color = color;
            outline.OutlineColor = color;
        }
        void Update()
        {
            //face the camera
            Vector3 cameraPosition = canvas.worldCamera.transform.position;
            Vector3 canvasPosition = canvas.transform.position;
            Vector3 direction = cameraPosition - canvasPosition;
            direction.y = 0; // Keep the canvas upright
            direction.Normalize();
            canvas.transform.rotation = Quaternion.LookRotation(direction);
        }
        public void UpdateAffordability(int _goldAmount)
        {
            if (soldOut) return;
            bool canAfford = CampaignManager.Instance.GoldManager.CheckIfCanAfford(int.Parse(priceText.text));
            Color color = ColorData.GetColorBasedOnAffordability(canAfford);
            priceText.color = color;
            outline.OutlineColor = color;
        }
        public void OnDestroy()
        {
            if(goldManager != null) {
                goldManager.OnGoldAmountChanged -= UpdateAffordability;
            }
        }
    }
}
