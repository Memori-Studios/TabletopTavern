using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ.Event
{
public class EventRewardDisplay : MonoBehaviour
{
        [SerializeField] private TMP_Text eventRewardText;
        [SerializeField] private Image eventOutcomeImage;
        private EventOutcomeModifierEnum _eventOutcomeModifierEnum;
        public EventOutcomeModifierEnum EventOutcomeModifierEnum => _eventOutcomeModifierEnum;

        public void LoadEventReward(EventOutcomeModifier eventRewardOutcome, string _extraInfo = "")
        {
            _eventOutcomeModifierEnum = eventRewardOutcome.EventOutcomeModifierEnum;
            string localizedEventOutcomeModifier = LocalizationManager.Instance.GetText(eventRewardOutcome.EventOutcomeModifierEnum.ToString());

            if(eventRewardOutcome.EventOutcomeModifierEnum == EventOutcomeModifierEnum.ConsumableDrop)
            {
                if(eventRewardOutcome.Value > 0) {
                    eventOutcomeImage.sprite = SpriteData.GetSprite("PositiveReputation");
                    eventOutcomeImage.color = ColorData.GetEventOutcomeColor("PositiveReputation");
                    eventRewardText.text = "+ " + localizedEventOutcomeModifier;
                } else {
                    eventOutcomeImage.sprite = SpriteData.GetSprite("NegativeReputation");
                    eventOutcomeImage.color = ColorData.GetEventOutcomeColor("NegativeReputation");
                    eventRewardText.text = "- " + localizedEventOutcomeModifier;
                }
            }
            else if(eventRewardOutcome.EventOutcomeModifierEnum == EventOutcomeModifierEnum.PrestigeUnit)
            {
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                string unitPresitged = LocalizationManager.Instance.GetText("Unit Prestiged");
                eventRewardText.text = $"{unitPresitged}: {_extraInfo}";
            }
            else if(eventRewardOutcome.EventOutcomeModifierEnum == EventOutcomeModifierEnum.UnitHealth)
            {
                bool isPositive = eventRewardOutcome.Value > 0;
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor("NegativeReputation");

                eventRewardText.text = isPositive ? "+" : "";
                eventRewardText.text += (eventRewardOutcome.Value*100).ToString() + "% ";
                eventRewardText.text += localizedEventOutcomeModifier;

                if (CampaignManager.Instance.CampaignSaveManager.IsEventHealthLossBlocked(eventRewardOutcome.Value))
                {
                    string blockingGear = LocalizationManager.Instance.GetText(GearID.MichaelsSecretStuff + "Name");
                    eventRewardText.text = $"<s>{eventRewardText.text}</s> ({blockingGear})";
                }
            }
            else if(eventRewardOutcome.EventOutcomeModifierEnum is EventOutcomeModifierEnum.LoseGear or EventOutcomeModifierEnum.LoseSquad or EventOutcomeModifierEnum.LosePrestige)
            {
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor("NegativeReputation");
                eventRewardText.text = $"{localizedEventOutcomeModifier}: {_extraInfo}";
            }
            else if(eventRewardOutcome.EventOutcomeModifierEnum is EventOutcomeModifierEnum.NextBattleLeadership or EventOutcomeModifierEnum.NextBattleEnemyLeadership or EventOutcomeModifierEnum.NextBattleMana)
            {
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventRewardText.text = $"{(eventRewardOutcome.Value > 0 ? "+" : "")}{eventRewardOutcome.Value} {localizedEventOutcomeModifier}";
            }
            else if(eventRewardOutcome.EventOutcomeModifierEnum == EventOutcomeModifierEnum.NextBattleFixture)
            {
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventRewardText.text = $"{LocalizationManager.Instance.GetText(eventRewardOutcome.Fixture.ToString())} ({localizedEventOutcomeModifier})";
            }
            else {
                eventOutcomeImage.sprite = SpriteData.GetSprite(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventOutcomeImage.color = ColorData.GetEventOutcomeColor(eventRewardOutcome.EventOutcomeModifierEnum.ToString());
                eventRewardText.text = eventRewardOutcome.Value != 1 ? eventRewardOutcome.Value.ToString() + " " : " ";
                eventRewardText.text += localizedEventOutcomeModifier;
            }
        }
    }
}
