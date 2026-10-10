using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Utilities;
using UnityEngine.EventSystems;
using Memori.Audio;
using Memori.Steamworks;

namespace TJ
{

    public class CustomBattleSquadSpawnCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HoverScale = 1.15f;
        private const float ScaleDuration = 0.07f;
        private Coroutine _scaleCoroutine;
        public Button button;
        public Image unitTypeImage, ironLegionImage, greenTideImage, ravenhostImage, taelindorBackground, sanguineCourtImage, sakuraDynastyBackground, drakosaurBroodBackground, deepstoneHoldBackground;
        public UnitName UnitName;
        public GameObject soonBlocker;
        public Image unitTypeImageDarkener;

        private void Start()
        {
            unitTypeImageDarkener.enabled = true;
        }

        public void SetUp(UnitName _unitName, System.Action switchCursorModeToSpawn)
        {
            UnitName = _unitName;
            Race race = TabletopTavernData.Instance.GetRaceFromUnitName(_unitName);
            ironLegionImage.enabled = race == Race.IronLegion;
#if FACTIONUPDATE
            // The League borrows Iron Legion's card background until it has its own art.
            ironLegionImage.enabled |= race == Race.OlympianLeague;
#endif
            greenTideImage.enabled = race == Race.Gruntkin;
            ravenhostImage.enabled = race == Race.RavenHost;
            taelindorBackground.enabled = race == Race.TaelindorForest;
            sanguineCourtImage.enabled = race == Race.SanguineCourt;
            sakuraDynastyBackground.enabled = race == Race.SakuraDynasty;
            drakosaurBroodBackground.enabled = race == Race.DrakosaurBrood;
            deepstoneHoldBackground.enabled = race == Race.DeepstoneHold;

            unitTypeImage.sprite = TabletopTavernData.Instance.GetUnitIcon(_unitName);
            int tier = TabletopTavernData.Instance.GetUnitTierFromUnitName(_unitName);
            UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(_unitName);

            // bool comingSoonRace = race == Race.NewOne;
            // if(comingSoonRace)
                // soonBlocker.SetActive(true);
                
            soonBlocker.SetActive(false);
            button.onClick.AddListener(() => BattleManager.Instance.SpawnManager.SelectUnitToSpawn(_unitName));
            button.onClick.AddListener(() => switchCursorModeToSpawn?.Invoke());
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            unitTypeImageDarkener.enabled = false;
            SquadStats _squad = TabletopTavernData.Instance.GetSquadStats(UnitName);
            BattleManager.Instance.UIManager.SquadBattleInfo.SetUpSpawn(_squad, BattleManager.Instance.UnitsToSpawnPrestige);

            IAudioRequester.Instance.PlaySFX(SFXData.HoverHero);
            ScaleIcon(HoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            unitTypeImageDarkener.enabled = true;
            BattleManager.Instance.UIManager.SquadBattleInfo.Unhover();
            ScaleIcon(1f);
        }

        private void ScaleIcon(float targetScale)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
        }

        private IEnumerator ScaleRoutine(float targetScale)
        {
            Vector3 target = Vector3.one * targetScale;
            Vector3 start = unitTypeImage.transform.localScale;
            float elapsed = 0f;
            while (elapsed < ScaleDuration)
            {
                elapsed += Time.deltaTime;
                unitTypeImage.transform.localScale = Vector3.Lerp(start, target, elapsed / ScaleDuration);
                yield return null;
            }
            unitTypeImage.transform.localScale = target;
        }
    }
}
