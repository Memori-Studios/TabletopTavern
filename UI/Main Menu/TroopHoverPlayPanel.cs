using Memori.SaveData;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TJ.MainMenu
{
public class TroopHoverPlayPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private int index;
    PlayPanel playPanel;
    public void SetUp(int _index, PlayPanel _playPanel)
    {
        index = _index;
        playPanel = _playPanel;
        // A squad tile answers the cursor like every other control; the wide signature card grows less.
        Memori.UI.UIHoverBloom.Attach(gameObject, null, _index == PlayPanel.SIGNATURE_UNIT_HOVER_INDEX ? 1.03f : 1.04f, false);
    }
    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        Memori.Audio.IAudioRequester.Instance.PlaySFX(Memori.Audio.SFXData.LightMouseOver);
        if(index == -1)
        {
            SquadToLoad squadToAdd = GetComponent<SquadDisplayCardMenu>().GetSquadToLoad();
            playPanel.StartingArmySection.PointerOverTroop(squadToAdd);
        }
        else
        {
            playPanel.StartingArmySection.PointerOverTroop(index);
        }
    }
    public virtual void OnPointerExit(PointerEventData eventData)
    {
        playPanel.StartingArmySection.PointerOffTroop();
    }
}
}