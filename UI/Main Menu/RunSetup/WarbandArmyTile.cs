using Memori.Localization;
using Memori.SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One squad in the warband's starting army: portrait in a rarity-coloured frame, squad size and name.</summary>
    public class WarbandArmyTile : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text sizeText;
        [SerializeField] private TMP_Text nameText;

        public void Set(SquadToLoad squad)
        {
            UnitName unit = squad.UnitName;
            portrait.sprite = TabletopTavernData.Instance.GetUnitIcon(unit);
            frame.color = (Color)ColorData.GetRarityTierColor(TabletopTavernData.Instance.GetSquadStats(unit).RarityTier);
            sizeText.text = squad.maxUnitCount.ToString();
            nameText.text = LocalizationManager.Instance.GetText(unit.ToString());
        }
    }
}
