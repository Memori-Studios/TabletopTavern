using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace TJ.Spells
{
/// <summary>What the rail needs to know about one selected mage squad, resolved by SpellManager.</summary>
public struct MageTileInfo
{
    public int SquadId;
    public Entity Entity;
    /// <summary>The squad's card in the bottom row; the tile is parented to it.</summary>
    public Transform Card;
    public UnitName UnitName;
    public SpellData Spell;
    public int CardNumber;
    public int MaxCharges;
    public float Range;
    public float Cooldown;
}

/// <summary>
/// One spell tile per selected mage squad, sitting directly above that squad's card in the bottom
/// row, so the tile and the mage read as one thing and no index is needed. Digits run left to right
/// in card order after the hotbar's. Tiles are rebuilt on selection change and polled from ECS every
/// frame for charges and cooldown. This object is only the manager; the tiles live under the cards.
/// </summary>
public class MageCastRail : MonoBehaviour
{
    [SerializeField] private MageCastTile tilePrefab;
    // Gap between the card's top edge and the tile's bottom edge.
    [SerializeField] private float tileLift = 16f;
    // Deprecated with the move onto the cards; kept so the scene reference does not dangle.
    [SerializeField] private RectTransform tileParent;
    [SerializeField] private CanvasGroup canvasGroup;

    private readonly List<MageCastTile> tiles = new();
    public int TileCount => tiles.Count;
    private int firstHotkeyNumber;
    private Action<int> onArmRequested;
    private Action<int, bool> onTileHover;
    private Func<int> armedSquadId;
    private Action rebuildRequested;
    private bool initialized;
    private bool menuOpen;

    public void Initialize(int hotbarSlotCount, Action<int> onArm, Action<int, bool> onHover,
        Func<int> armedSquad, Action onRebuildRequested)
    {
        firstHotkeyNumber = hotbarSlotCount + 1;
        onArmRequested = onArm;
        onTileHover = onHover;
        armedSquadId = armedSquad;
        rebuildRequested = onRebuildRequested;
        initialized = true;
        if(canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Refresh(List<MageTileInfo> mages)
    {
        if(!initialized || tilePrefab == null) return;

        foreach(MageCastTile tile in tiles) if(tile != null) Destroy(tile.gameObject);
        tiles.Clear();

        for(int i = 0; i < mages.Count; i++)
        {
            MageTileInfo info = mages[i];
            if(info.Card == null) continue;
            MageCastTile tile = Instantiate(tilePrefab, info.Card);
            tile.name = $"Mage Cast Tile {info.SquadId}";
            // Centred on the card, standing on its top edge. The card's own layout group only
            // governs the row, so a child placed here is left alone.
            RectTransform rect = (RectTransform)tile.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, tileLift);
            // Past the ten digits there is no key; the tile is click-only and shows no number.
            int hotkeyNumber = firstHotkeyNumber + i;
            if(hotkeyNumber > 10) hotkeyNumber = 0;
            int squadId = info.SquadId;
            tile.Load(info, hotkeyNumber, () => onArmRequested?.Invoke(squadId), on => onTileHover?.Invoke(squadId, on));
            tile.SetMenuOpen(menuOpen);
            tiles.Add(tile);
        }
    }

    public void SetMenuOpen(bool open)
    {
        menuOpen = open;
        foreach(MageCastTile tile in tiles) if(tile != null) tile.SetMenuOpen(open);
    }

    private void Update()
    {
        if(tiles.Count == 0) return;
        World world = World.DefaultGameObjectInjectionWorld;
        if(world == null || !world.IsCreated) return;
        EntityManager entityManager = world.EntityManager;

        int armed = armedSquadId != null ? armedSquadId() : 0;
        bool rebuild = false;
        foreach(MageCastTile tile in tiles)
        {
            Entity squad = tile.SquadEntity;
            // A spent mage traded MageSquad for MeleeSquad; its tile leaves with it.
            if(!entityManager.Exists(squad) || !entityManager.HasComponent<MageSquad>(squad))
            {
                rebuild = true;
                continue;
            }

            int charges = entityManager.HasComponent<SquadAmmunition>(squad)
                ? entityManager.GetComponentData<SquadAmmunition>(squad).Value : 0;
            tile.SetCharges(charges);

            DynamicBuffer<EntityReferenceBufferElement> units = entityManager.GetBuffer<EntityReferenceBufferElement>(squad);
            if(units.Length > 0 && entityManager.HasComponent<MageCast>(units[0].Entity))
            {
                MageCast cast = entityManager.GetComponentData<MageCast>(units[0].Entity);
                bool onCooldown = cast.Timer > 0f;
                tile.RenderCooldown(cast.Cooldown > 0f ? cast.Timer / cast.Cooldown : 0f, onCooldown, cast.Timer);
            }

            tile.SetSelected(armed == tile.SquadId);
            tile.SetPending(entityManager.HasComponent<MageManualCastOrder>(squad)
                && entityManager.IsComponentEnabled<MageManualCastOrder>(squad));
        }

        if(rebuild) rebuildRequested?.Invoke();
    }
}
}
