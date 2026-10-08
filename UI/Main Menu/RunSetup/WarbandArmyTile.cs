using System.Collections;
using Memori.Localization;
using Memori.SaveData;
using Memori.UI;
using Memori.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>One squad in the warband's starting army: portrait in a rarity-coloured frame and name.</summary>
    public class WarbandArmyTile : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text nameText;
        // Optional. The shared Land Flare, played in the squad's rarity colour as it joins the army.
        [SerializeField] private UIFlare landFlare;

        private Color rarityColour;

        public void Set(SquadToLoad squad)
        {
            UnitName unit = squad.UnitName;
            portrait.sprite = TabletopTavernData.Instance.GetUnitIcon(unit);
            rarityColour = (Color)ColorData.GetRarityTierColor(TabletopTavernData.Instance.GetSquadStats(unit).RarityTier);
            frame.color = rarityColour;
            nameText.text = LocalizationManager.Instance.GetText(unit.ToString());
        }

        // The squad lands in its slot: a punch, a flare and a coin drop.
        public void PlayLanded()
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(UIJuice.Punch(transform, 1.12f, 0.06f, 0.18f));
            if (landFlare != null) landFlare.Play(rarityColour);
            IAudioRequester.Instance.PlaySFX(SFXData.CoinDrop);
        }

        // The squad leaves: the tile shrinks and fades, then removes itself.
        public void PlayRemoved()
        {
            if (!isActiveAndEnabled) { Destroy(gameObject); return; }
            StartCoroutine(Removed());
        }

        private IEnumerator Removed()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Mathf.Min(Time.unscaledDeltaTime, UIJuice.MaxStep) / 0.12f)
            {
                float s = Mathf.Lerp(1f, 0.85f, t);
                transform.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = 1f - t;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
