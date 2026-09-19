using UnityEngine;
using TJ.Map;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Audio;
using System;
using Memori.Localization;

namespace TJ
{
public class GoldManager : MonoBehaviour
{
    [SerializeField] GoldNotificationSpawner goldNotificationSpawner;
    CampaignSaveManager campaignSaveManager;
    public event Action<int> OnGoldAmountChanged;
    public const int DefaultMaxInterest = 5;
    public const int DefaultPotionRewardsOdds = 25;
    private int maxInterest = DefaultMaxInterest;
    private int potionRewardsOdds = DefaultPotionRewardsOdds;
    private int _currentGoldAmount = 0;
    public int CurrentGoldAmount => _currentGoldAmount;

    // GoldManager is a scene-instantiated MonoBehaviour that doesn't exist yet when
    // TabletopTavernData.Awake() applies mod overrides at boot (main menu scene, before the
    // campaign map scene loads). Static fields sidestep that timing gap - same mechanism
    // HeroBonusManager already uses for its boot-loaded, campaign-scene-consumed rule lists.
    private static int? MaxInterestOverride;
    private static int? PotionRewardsOddsOverride;
    public static void ClearEconomyOverrides()
    {
        MaxInterestOverride = null;
        PotionRewardsOddsOverride = null;
    }
    public static void SetMaxInterestOverride(int value) => MaxInterestOverride = value;
    public static void SetPotionRewardsOddsOverride(int value) => PotionRewardsOddsOverride = value;

    public int PotionRewardsOdds => PotionRewardsOddsOverride ?? potionRewardsOdds;

    public void ModifyGold(int amount, string localizedString, bool silent = false)
    {
        if(amount == 0) return;

        campaignSaveManager.ModifyGoldSaveDataValue(amount);
        _currentGoldAmount = campaignSaveManager.SaveData.goldAmount;

        OnGoldAmountChanged?.Invoke(_currentGoldAmount);

        Debug.Log($"Modified gold by {amount}. for: {localizedString}");
        if(!silent) 
        {
            IAudioRequester.Instance.PlaySFX(SFXData.AquireGold);
            goldNotificationSpawner.DisplayGoldNotification(_currentGoldAmount, localizedString);
        }
    }

    public void SetUp()
    {
        campaignSaveManager = CampaignManager.Instance.CampaignSaveManager;
    }

    public void LoadGold()
    {
        _currentGoldAmount = campaignSaveManager.SaveData.goldAmount;
        goldNotificationSpawner.LoadGold(campaignSaveManager.SaveData.goldAmount);
        OnGoldAmountChanged?.Invoke(_currentGoldAmount);
    }
    public int GetMaxInterest()
    {
        int _maxInterest = MaxInterestOverride ?? maxInterest;
        if(CampaignManager.Instance.GearManager.CheckForGear(GearID.DwarvenTaxCollectors)) _maxInterest = 10;

        return _maxInterest;
    }
    public int GetBaseInterest()
    {   
        return Mathf.Min(campaignSaveManager.GetInterestEarned(), GetMaxInterest());
    }
    public int GetTotalInterest()
    {
        int interest = GetBaseInterest();
        if(CampaignManager.Instance.GearManager.CheckForGear(GearID.OmenofFamine)) {
            List<GearID> gearIDs = campaignSaveManager.SaveData.Gear;
            int bonus = 2 * (campaignSaveManager.MaxGear - gearIDs.Count);
            interest += bonus;
        }
        if(CampaignManager.Instance.GearManager.CheckForGear(GearID.IronBank))  interest *= 2;
        
        if (HeroBonusManager.Instance.ActiveHeroID == 12) interest *= 2;

        return interest;
    }
    public void CollectInterest()
    {
        int interest = GetTotalInterest();
        int bonus = 0;

        for (int i = 0; i < campaignSaveManager.SaveData.playerArmy.Length; i++)
        {
            var squad = campaignSaveManager.SaveData.playerArmy[i];
            if (squad.isEmptySquad) continue;
            if (HeroBonusManager.UnitHasAttribute(squad.UnitName, campaignSaveManager.SaveData.heroID, UnitAttribute.DragonsHoard))
            {
                bonus += 3;
            }
        }

        if(interest == 0) return;
        string interestLocalized = LocalizationManager.Instance.GetText("Interest at turn end");
        ModifyGold(interest, interestLocalized);

        if(bonus > 0) ModifyGold(bonus, LocalizationManager.Instance.GetText("DragonsHoard"));
        // Debug.Log($"Collected {interest} gold in interest");
    }
    public bool CheckIfCanAfford(int _cost)
    {
        return campaignSaveManager.SaveData.goldAmount >= _cost;
    }
    
}
}