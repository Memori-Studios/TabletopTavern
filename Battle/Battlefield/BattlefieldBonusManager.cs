using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BattlefieldBonusManager : MonoBehaviour
{
    [SerializeField] private List<AssetReferenceGameObject> possibleBonuses;
    [SerializeField] private int customBattleCount = 1;
    public int CustomBattleCount => customBattleCount;
    private List<GameObject> bonusObjects = new();

    public async Task SetUp(int _seed, SpawnBox _battlefieldSize, int? forcedCount = null, IReadOnlyList<BattlefieldBonusEnum> forcedFixtures = null)
    {
        for (int i = 0; i < bonusObjects.Count; i++)
        {
            Addressables.ReleaseInstance(bonusObjects[i]);
        }
        bonusObjects.Clear();

        System.Random random = new(_seed);
        int bonusCount = forcedCount ?? random.Next(TabletopTavernConstants.BATTLEFIELD_BONUSES_RANGE.x, TabletopTavernConstants.BATTLEFIELD_BONUSES_RANGE.y);
        for (int i = 0; i < bonusCount; i++)
        {
            await Spawn(possibleBonuses[random.Next(0, possibleBonuses.Count)], random, _battlefieldSize);
        }

        if (forcedFixtures == null) return;
        foreach (BattlefieldBonusEnum fixture in forcedFixtures)
        {
            AssetReferenceGameObject bonusRef = await FindFixture(fixture);
            if (bonusRef == null)
            {
                Debug.LogError($"[BattlefieldBonusManager] A map event asked for {fixture}, but no possible bonus prefab carries it.");
                continue;
            }
            await Spawn(bonusRef, random, _battlefieldSize);
        }
    }

    private async Task Spawn(AssetReferenceGameObject _bonusRef, System.Random _random, SpawnBox _battlefieldSize)
    {
        Vector3 position = new Vector3(
            _random.Next((int)_battlefieldSize.min.x, (int)_battlefieldSize.max.x) / 2,
            0,
            _random.Next((int)_battlefieldSize.min.z, (int)_battlefieldSize.max.z) / 2
        );
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(_bonusRef, position, Quaternion.identity);
        await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            bonusObjects.Add(handle.Result);
        }
    }

    private async Task<AssetReferenceGameObject> FindFixture(BattlefieldBonusEnum _fixture)
    {
        foreach (AssetReferenceGameObject bonusRef in possibleBonuses)
        {
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(bonusRef.RuntimeKey);
            await handle.Task;
            bool match = handle.Status == AsyncOperationStatus.Succeeded
                && handle.Result.TryGetComponent(out BattlefieldBonusGameObject bonus)
                && bonus.BattlefieldBonus.BattlefieldBonusEnum == _fixture;
            Addressables.Release(handle);
            if (match) return bonusRef;
        }
        return null;
    }

    public void CleanUp()
    {
        for (int i = 0; i < bonusObjects.Count; i++)
            Addressables.ReleaseInstance(bonusObjects[i]);
        bonusObjects.Clear();
    }
}
