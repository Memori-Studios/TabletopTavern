using System.Collections.Generic;
using System.Text;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Ordeals
{
    /// <summary>One held Ordeal on the HUD strip, or the "+N" pip that lists the ones past the strip's end.</summary>
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class OrdealIcon : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _overflowText;

        public void Load(OrdealDefinition ordeal, CampaignSaveData run)
        {
            _icon.enabled = true;
            _icon.sprite = SpriteData.GetSprite(ordeal.IconName);
            _overflowText.gameObject.SetActive(false);
            GetComponent<MemoriTooltipTrigger>().SetUpToolTip(new TooltipContent
            {
                Title = LocalizationManager.Instance.GetText(ordeal.NameKey),
                Icon = _icon.sprite,
                Body = KeywordText.ForTooltip(LocalizationManager.Instance.GetText(ordeal.DescriptionKey)),
                Detail = DrawnValue(ordeal.Id, run),
            });
        }
        // The value a card drew when it was taken, which its description cannot know.
        private static string DrawnValue(OrdealId id, CampaignSaveData run)
        {
            if (id == OrdealId.LongNight && run.ordealWeather != Weather.ClearSkies)
                return string.Format(LocalizationManager.Instance.GetText("OrdealLongNightDetail"), LocalizationManager.Instance.GetText(run.ordealWeather.ToString()));
            if (id == OrdealId.SealedPage && run.sealedSpellSlot > 0)
                return string.Format(LocalizationManager.Instance.GetText("OrdealSealedPageDetail"), run.sealedSpellSlot + 1);
            return "";
        }
        public void LoadOverflow(List<OrdealId> rest)
        {
            _icon.enabled = false;
            _overflowText.gameObject.SetActive(true);
            _overflowText.text = $"+{rest.Count}";

            var body = new StringBuilder();
            foreach (OrdealId id in rest)
                body.AppendLine(LocalizationManager.Instance.GetText(OrdealRegistry.Get(id).NameKey));
            GetComponent<MemoriTooltipTrigger>().SetUpToolTip(new TooltipContent
            {
                Title = LocalizationManager.Instance.GetText("Ordeals"),
                Body = body.ToString().TrimEnd(),
            });
        }
    }
}
