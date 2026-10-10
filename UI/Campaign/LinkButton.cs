using UnityEngine;
using Memori.UI;

public class LinkButton : MonoBehaviour
{
    [SerializeField] private MemoriButtonV2 button;
    [SerializeField] private Link link;

    private void Start()
    {
        button.Button.onClick.AddListener(() => Application.OpenURL(link.Url));
    }
    private void OnDestroy()
    {
        button.Button.ClearClickListeners();
    }
}
