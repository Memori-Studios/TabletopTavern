using TMPro;
using UnityEngine;


namespace TJ.Map
{
public class UnitBonusUI : MonoBehaviour
{
    [SerializeField] private TMP_Text bonusNameText, bonusDescriptionText;
    public void LoadUnitBonusUI(string _bonusName, string _bonusDescription)
    {
        bonusNameText.text = _bonusName;
        KeywordText.Apply(bonusDescriptionText, _bonusDescription);
    }
}
}