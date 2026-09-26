using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TJ.Shop
{
[RequireComponent(typeof(Canvas))]
public class ShopItemInfoCanvas : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText, descriptionText;
    private Canvas canvas;
    private void Start()
    {
        canvas = GetComponent<Canvas>();
        canvas.worldCamera = CampaignManager.Instance.MapCamera.ShopCamera;
    }
    public void SetUp(string _title, string _description)
    {
        titleText.text = _title;
        // A world-space sign over the shop stall: nothing on it is hovered.
        descriptionText.text = KeywordText.Render(_description, false);
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
}
}
