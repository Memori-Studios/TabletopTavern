using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    /// <summary>The kill and loss tabs over one army bar card at the end of a battle. Built by EndBattlePanelBuilder.</summary>
    public class EndBattleSquadBadge : MonoBehaviour
    {
        [SerializeField] private GameObject lostTab;
        [SerializeField] private TMP_Text lostText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private Image killsIcon;
        [SerializeField] private Image killsEdge;
        [SerializeField] private GameObject mostSlainFrame;
        [SerializeField] private TMP_Text mostSlainCaption;
        [SerializeField] private Color killsColour = new Color32(0xEC, 0xE6, 0xD8, 0xFF);
        [SerializeField] private Color mostSlainColour = new Color32(0xE9, 0xC0, 0x6A, 0xFF);

        /// <summary>No losses leaves the loss tab's slot empty, so every kills tab sits on one line.</summary>
        public void Set(int kills, int lost, bool mostSlain, string mostSlainText)
        {
            lostTab.SetActive(lost > 0);
            lostText.text = "-" + lost;
            killsText.text = kills.ToString();
            Color colour = mostSlain ? mostSlainColour : killsColour;
            killsText.color = colour;
            killsIcon.color = colour;
            if (killsEdge != null && mostSlain) killsEdge.color = mostSlainColour;
            mostSlainFrame.SetActive(mostSlain);
            mostSlainCaption.gameObject.SetActive(mostSlain);
            mostSlainCaption.text = mostSlainText;
        }
    }
}
