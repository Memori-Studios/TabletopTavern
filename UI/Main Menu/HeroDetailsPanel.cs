using Memori.SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Memori.Notifications;
using Memori.Localization;
using MoreMountains.Feedbacks;
using Memori.Audio;
using UnityEngine.EventSystems;
using Memori.Steamworks;
using Memori.Tooltip;
using Memori.UI;
using Memori.Metaprogression;
using System;

namespace TJ.MainMenu
{
    public class HeroDetailsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text heroBonusText1;
        [SerializeField] private TMP_Text heroBonusText2;
        [SerializeField] private TMP_Text heroGoldText;
        [SerializeField] private SquadDisplayCardMenu uniqueUnitDisplayCardMenu;
        [SerializeField] private TMP_Text signatureUnitNameText, heroLoreText;
        // [SerializeField] private MemoriTooltipTrigger uniqueUnitTooltipTrigger;
        // [SerializeField] private MemoriTooltipTrigger heroTooltipTrigger;

        private SquadToLoad uniqueSquad;

        public void SetUp(Hero _hero)
        {
            string leaderLocalized = LocalizationManager.Instance.GetText("Leader");
            string heroNameLocalized = LocalizationManager.Instance.GetText(_hero.HeroName);
            heroNameText.text = $"<color {ColorData.Secondary}>{leaderLocalized}:</color> <color {ColorData.Primary}>{heroNameLocalized}</color>";
            
            string heroBonusText1string = HeroBonusText.Get(_hero, 0);
            string heroBonusText2string = HeroBonusText.Get(_hero, 1);
            string raceBonusTextstring = LocalizationManager.Instance.GetText(_hero.Race+ "BonusDescription");
            ColorData.XMLTagColorApplicator(ref heroBonusText1string);
            ColorData.XMLTagColorApplicator(ref heroBonusText2string);
            ColorData.XMLTagColorApplicator(ref raceBonusTextstring);
            heroBonusText1.text = heroBonusText1string;
            heroBonusText2.text = heroBonusText2string;

            string starting = LocalizationManager.Instance.GetText("Treasury");
            heroGoldText.text = $"{starting}: {_hero.StartingGold} <sprite name=GoldSprite>";

            uniqueSquad = new SquadToLoad(
                _hero.SignatureUnit, 
                _prestige: 0, 
                _unitIndex: 0
            );
            uniqueUnitDisplayCardMenu.SetUp(uniqueSquad, false, _isEnemy: true);
            uniqueUnitDisplayCardMenu.LockCard(true);
            // uniqueUnitDisplayCardMenu.gameObject.AddComponent<TroopHoverPlayPanel>().SetUp(99, this);
            signatureUnitNameText.text = LocalizationManager.Instance.GetText(uniqueSquad.UnitName.ToString());
            // string titleLocalized = LocalizationManager.Instance.GetText("SignatureUnit");
            // string descriptionLocalized = LocalizationManager.Instance.GetText("SignatureUnitDesc");
            // uniqueUnitTooltipTrigger.SetUpToolTip($"{titleLocalized}", $"{descriptionLocalized}");

            // string heroDescription = LocalizationManager.Instance.GetText(_hero.HeroDescription);
            // heroTooltipTrigger.SetUpToolTip($"{heroNameLocalized}", $"{heroDescription}");
            heroLoreText.text = LocalizationManager.Instance.GetLoreString(_hero.HeroPrefabName.ToString());
        }
    }
}