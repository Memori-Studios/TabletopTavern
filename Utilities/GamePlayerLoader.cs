using Memori.SaveData;
using Memori.Utilities;
using UnityEngine;
using System.Threading.Tasks;
using MagicaCloth2;
using Memori.Scenes;

namespace TJ
{
public class GamePlayerLoader : Singleton<GamePlayerLoader>
{
    [Header("Player Hero")]
    [SerializeField] private GameObject playerHeroGameObject;
    [SerializeField] private Transform playerBattlePosition, playerMapPosition, playerGamesPosition;

    [Header("Enemy Hero")]
    [SerializeField] private GameObject enemyHeroGameObject;
    [SerializeField] private Transform enemyBattlePosition, enemyMapPosition, enemyGamesPosition;

    private int _loadGeneration;
    private GameStateEnum _previousState;

    // Hero prefabs are loaded persistent (pinned) because these instances live in the permanent
    // Tavern backdrop and have to survive the ReleaseAll() that every scene transition fires.
    // Released here, coupled to destroying the instance each one backs, or they leak.
    private string _playerPrefabKey, _enemyPrefabKey;

    private void Start()
    {
        if (m_aboutToDestroy) return;

        // Seeded from the live state so the MainMenu -> Map reload still fires when this
        // component starts after SceneHandler has already broadcast MainMenu. Left at the
        // default (Load) it would miss that transition and keep a stale enemy general.
        _previousState = SceneHandler.Instance.CurrentGameState;
        LoadGamePlayer();
        SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        // A duplicate rejected in Awake never loaded anything; tearing down here would
        // destroy the serialized references it shares with the surviving instance.
        if (m_aboutToDestroy) return;

        if (SceneHandler.HasInstance)
            SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;

        DestroyHeroes();
    }

    public async void LoadGamePlayer()
    {
        int gen = ++_loadGeneration;
        DestroyHeroes();

        bool hasCampaign = SaveDataHandler.CampaignSaveExists();
        CampaignSaveData saveData = hasCampaign ? SaveDataHandler.Load() : null;

        // Resolved here rather than via SaveDataHandler.GetPlayerHeroPrefabAsync so both heroes
        // come from one read of the campaign save, and so the prefab key is known for release.
        int playerHeroID = hasCampaign ? saveData.heroID : HeroData.DefaultHeroID;
        Hero enemyHero = TabletopTavernData.Instance.GetEnemyHeroForCampaign(
            hasCampaign ? saveData.heroID : 0,
            hasCampaign ? saveData.bookNumber : 0,
            hasCampaign ? saveData.seed : 0,
            justGetRandomHero: !hasCampaign
        );

        (GameObject instance, string key) player = await LoadHeroAsync(playerHeroID, playerBattlePosition);
        if (gen != _loadGeneration) { DiscardHero(player); return; }
        playerHeroGameObject = player.instance;
        _playerPrefabKey = player.key;

        (GameObject instance, string key) enemy = await LoadHeroAsync(enemyHero.HeroID, enemyBattlePosition);
        if (gen != _loadGeneration) { DiscardHero(enemy); return; }
        enemyHeroGameObject = enemy.instance;
        _enemyPrefabKey = enemy.key;

        PositionPlayer(SceneHandler.Instance.CurrentGameState);
    }

    private async Task<(GameObject instance, string key)> LoadHeroAsync(int heroID, Transform parent)
    {
        string key = TabletopTavernData.Instance.GetHeroPrefabKey(heroID);
        GameObject prefab = await TabletopTavernData.Instance.LoadHeroPrefabAsync(heroID, persistent: true);
        if (prefab == null)
        {
            // LoadAsync already dropped the handle and unpinned the key on failure.
            Debug.LogError($"[GamePlayerLoader] Failed to load hero prefab for hero {heroID}.");
            return (null, null);
        }

        GameObject instance = Instantiate(prefab, parent);
        instance.SwapLayer("TavernGOs");

        MagicaClothHandler[] clothHandlers = instance.GetComponentsInChildren<MagicaClothHandler>(true);
        foreach (MagicaClothHandler clothHandler in clothHandlers)
            clothHandler.DisableClothSimulation();

        return (instance, key);
    }

    /// <summary>Tears down a hero a superseded load produced, so it does not leak the pinned key.</summary>
    private void DiscardHero((GameObject instance, string key) hero)
    {
        if (hero.instance != null) Destroy(hero.instance);
        // HasInstance, not Instance: on teardown there is nothing left to release, and
        // Instance would fabricate an empty manager just to be handed a key.
        if (!string.IsNullOrEmpty(hero.key) && AddressablesManager.HasInstance)
            AddressablesManager.Instance.Release(hero.key);
    }

    private void DestroyHeroes()
    {
        DiscardHero((playerHeroGameObject, _playerPrefabKey));
        playerHeroGameObject = null;
        _playerPrefabKey = null;

        DiscardHero((enemyHeroGameObject, _enemyPrefabKey));
        enemyHeroGameObject = null;
        _enemyPrefabKey = null;
    }

    public void MoveToGames()
    {
        MoveHero(playerHeroGameObject, playerGamesPosition);
        MoveHero(enemyHeroGameObject, enemyGamesPosition);
    }

    public void MoveToMap()
    {
        MoveHero(playerHeroGameObject, playerMapPosition);
        MoveHero(enemyHeroGameObject, enemyMapPosition);
    }

    private void PositionPlayer(GameStateEnum currentState)
    {
        if (currentState == GameStateEnum.Battle)
        {
            MoveHero(playerHeroGameObject, playerBattlePosition);
            MoveHero(enemyHeroGameObject, enemyBattlePosition);
        }
        else if (currentState == GameStateEnum.Map || currentState == GameStateEnum.MainMenu)
        {
            MoveToMap();
        }
    }

    public void PlayPlayerAnimation(string animationName)
    {
        if (playerHeroGameObject == null) return;
        Animator animator = playerHeroGameObject.GetComponentInChildren<Animator>();
        if (animator != null) animator.CrossFade(animationName, 0.2f);
    }

    public void PlayEnemyAnimation(string animationName)
    {
        if (enemyHeroGameObject == null) return;
        Animator animator = enemyHeroGameObject.GetComponentInChildren<Animator>();
        if (animator != null) animator.CrossFade(animationName, 0.2f);
    }

    private void MoveHero(GameObject hero, Transform target)
    {
        if (hero == null || target == null) return;
        hero.transform.SetParent(target);
        hero.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private void OnGameStateChanged(GameStateEnum newState)
    {
        if (newState == GameStateEnum.Map && _previousState == GameStateEnum.MainMenu)
            LoadGamePlayer();
        else
            PositionPlayer(newState);

        _previousState = newState;
    }
}
}
