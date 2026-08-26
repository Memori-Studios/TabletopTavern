using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Memori.SaveData;
using Unity.Entities;
using Unity.Mathematics;
using Memori.Audio;
using Memori.Input;
using TJ.Morale;
using TJ.MainMenu;

namespace TJ
{
public class SquadDisplayCardBattle : SquadDisplayCard, IDragHandler, IEndDragHandler, IBeginDragHandler
{
    [SerializeField] private int unitPrestige;
    [SerializeField] private GameObject dummySquadCard, brokenGO;

    [Header("Morale Bar")]
    [SerializeField] protected Slider moraleSlider;

    [Header("Squad Quick Info")]
    [SerializeField] private SquadDisplayCardBattleQuickInfo _quickInfo;
    [SerializeField] private GameObject _outOfAmmoIcon;

    SquadEntity squadEntity;
    Vector3 initialPosition;
    LayoutElement layoutElement;
    GameObject cachedDummySquadCard;
    Canvas canvas;
    int _dragGroupNumber; // group number (1-6) of this card during a drag, or -1 if ungrouped
    const float BAR_UPDATE_SPEED = 0.25f;
    float updateTimer = 0f;
    bool isMoving;
    bool isCharging;
    bool isBracing;
    bool defensiveStance;
    bool isFiring;
    bool volleyFiring, rapidFiring;
    bool inMeleeCombat;
    bool ceaseFiring;
    bool _wasRanged;
    bool _wasCaster;
    bool _rangedCheckDone;
    bool outOfAmmo;
    
    public void FixedUpdate()
    {
        updateTimer += Time.fixedDeltaTime;
        if(updateTimer < BAR_UPDATE_SPEED) return;
        updateTimer = 0f;
        
        if(squadEntity.SelfEntity == Entity.Null) return;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if(!entityManager.HasComponent<SquadStateComponent>(squadEntity.SelfEntity)){
            // Debug.Log($"SquadDisplayCardBattle: squadEntity.SelfEntity: {squadEntity.SelfEntity} does not have SquadTotalHealth component");
            Destroy(gameObject);
            return;
        };
        if(!entityManager.HasComponent<MoraleComponent>(squadEntity.SelfEntity)){
            // Debug.Log($"SquadDisplayCardBattle: squadEntity.SelfEntity: {squadEntity.SelfEntity} does not have MoraleComponent component");
            Destroy(gameObject);
            return;
        };

        if (!_rangedCheckDone)
        {
            _rangedCheckDone = true;
            _wasRanged = entityManager.HasComponent<RangedSquad>(squadEntity.SelfEntity);
            // A mage loses MageSquad rather than RangedSquad when it runs dry, so it needs its own
            // latch - without it a spent mage never reads as spent.
            _wasCaster = entityManager.HasComponent<MageSquad>(squadEntity.SelfEntity);
        }

        isMoving = entityManager.HasComponent<SquadMoveOverrideTag>(squadEntity.SelfEntity);
        isCharging = entityManager.HasComponent<ChargeSquad>(squadEntity.SelfEntity);
        isBracing = entityManager.IsComponentEnabled<BracedTag>(squadEntity.SelfEntity);
        defensiveStance = entityManager.IsComponentEnabled<DefensiveStanceTag>(squadEntity.SelfEntity);
        inMeleeCombat = entityManager.HasComponent<InCombat>(squadEntity.SelfEntity);
        ceaseFiring = entityManager.IsComponentEnabled<CeaseFireTag>(squadEntity.SelfEntity);
        outOfAmmo = (_wasRanged && !entityManager.HasComponent<RangedSquad>(squadEntity.SelfEntity))
                 || (_wasCaster && !entityManager.HasComponent<MageSquad>(squadEntity.SelfEntity));

        isFiring = !outOfAmmo && entityManager.HasComponent<FormationEngagedInRangedCombat>(squadEntity.SelfEntity);
        if(isFiring) {
            if(!entityManager.HasComponent<SquadOverridesComponent>(squadEntity.SelfEntity))
            {
                volleyFiring = false;
                rapidFiring = false;
            }
            else
            {
                bool hasVolleyFiring = entityManager.GetComponentData<SquadOverridesComponent>(squadEntity.SelfEntity).FireMode == RangedFireMode.Volley;
                volleyFiring = hasVolleyFiring;
                rapidFiring = !hasVolleyFiring;
            }
        }
        else
        {
            volleyFiring = false;
            rapidFiring = false;
        }

        SquadStateComponent squadTotalHealth = entityManager.GetComponentData<SquadStateComponent>(squadEntity.SelfEntity);
        _presenter.HealthSlider.value = (float)squadTotalHealth.CurrentHealthValue/(float)squadTotalHealth.MaxHealthValue;
        MoraleComponent moraleComponent = entityManager.GetComponentData<MoraleComponent>(squadEntity.SelfEntity);
        moraleSlider.value = moraleComponent.CurrentMorale / moraleComponent.MaxMorale;

        _quickInfo.UpdateSquadDisplay(isMoving, isCharging, isBracing, defensiveStance, volleyFiring, rapidFiring, inMeleeCombat, ceaseFiring);

        if (_outOfAmmoIcon != null)
            _outOfAmmoIcon.SetActive(outOfAmmo);
    }
    public void SetUp(SquadEntity _squadEntity, int _unitCount)
    {
        canvas = GetComponent<Canvas>();
        layoutElement = GetComponent<LayoutElement>();
        squadEntity = _squadEntity;
        var doubleClickHandler = GetComponent<SquadDisplayCardBattleDoubleClickHandler>();
        if (doubleClickHandler != null) doubleClickHandler.SetUp(_squadEntity.SelfEntity);
        SquadToLoad squadToLoad = new ()
        {
            UniqueID = _squadEntity.SquadId.ToString(),
            UnitName = _squadEntity.UnitName,
            // currentUnitCount = _unitCount,
            maxUnitCount = _squadEntity.initialSquadSize,
            // percentDamageTaken = 0,
            UnitPrestige = unitPrestige
        };
        base.SetUp(squadToLoad, _squadEntity.SquadId);
        _presenter.HealthSlider.maxValue = 1;
        _presenter.HealthSlider.wholeNumbers = false;
        moraleSlider.maxValue = 1;
        moraleSlider.wholeNumbers = false;
        this.name = _squadEntity.UnitName.ToString() + " " + _squadEntity.SquadId;
        SetBroken(false);
    }
    public void SetUnitPrestige(int _unitPrestige)
    {
        unitPrestige = _unitPrestige;
    }
    public override void SelectSquadButtonClicked()
    {
        if(BattleManager.Instance.GroupManager.GroupHovered != 0) return;

        // Debug.Log($"Select Squad Button Clicked squad {squadId}");
        UnitSelectionManager.Instance.SquadCardSelected(squadId);
    }
    public override void OnPointerEnter(PointerEventData eventData)
    {
        if(BattleManager.Instance.GroupManager.GroupHovered != 0) return;
        if(_quickInfo.IsPointerOver) return;

        // Debug.Log($"Pointer Enter squad {squadId}");
        base.OnPointerEnter(eventData);
        BattleManager.Instance.UnitSelectionManager.HoverSquad(squadId, true);
    }
    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        BattleManager.Instance.UnitSelectionManager.HoverSquad(0, true);
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragGroupNumber = BattleManager.Instance.GroupManager.GetGroupNumberForSquad(squadEntity.SquadId);

        initialPosition = transform.position;

        cachedDummySquadCard = Instantiate(dummySquadCard, transform.position, Quaternion.identity);
        cachedDummySquadCard.transform.SetParent(transform.parent);
        cachedDummySquadCard.transform.SetSiblingIndex(transform.GetSiblingIndex());

        transform.SetSiblingIndex(transform.parent.transform.childCount - 1);
        BattleInputManager.Instance.SetRearrangingSquads(true);

        layoutElement.ignoreLayout = true;
        canvas.sortingOrder = 100;
        IAudioRequester.Instance.PlaySFX(SFXData.SquadHovered);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if(cachedDummySquadCard == null) return;

        transform.position = InputHandler.Instance.MousePosition;
        initialPosition = cachedDummySquadCard.transform.position;

        float distanceInX = initialPosition.x - transform.position.x;

        if(distanceInX < -60)
        {
            int index = cachedDummySquadCard.transform.GetSiblingIndex();
            // The dragged card parks at the last sibling index and the dummy holds its slot, so the
            // last real card sits at childCount - 2. Without this the dummy can be pushed past the
            // end, where GetGroupNumberForSquadAtIndex returns -1 and reads as "ungrouped".
            if (index >= transform.parent.childCount - 2) return;
            // Block if the card to the right is in a different group (or grouped when we're not).
            int neighborGroup = BattleManager.Instance.GroupManager.GetGroupNumberForSquadAtIndex(index + 1);
            if(neighborGroup != _dragGroupNumber) return;
            cachedDummySquadCard.transform.SetSiblingIndex(index+1);
            initialPosition = cachedDummySquadCard.transform.position;
            IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
        }

        if (distanceInX > 60)
        {
            int index = cachedDummySquadCard.transform.GetSiblingIndex();
            if(index < 1) return;
            // Block if the card to the left is in a different group (or grouped when we're not).
            int neighborGroup = BattleManager.Instance.GroupManager.GetGroupNumberForSquadAtIndex(index - 1);
            if(neighborGroup != _dragGroupNumber) return;
            cachedDummySquadCard.transform.SetSiblingIndex(index-1);
            initialPosition = cachedDummySquadCard.transform.position;
            IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if(cachedDummySquadCard == null) return;

        int newIndex = cachedDummySquadCard.transform.GetSiblingIndex();
        transform.position = cachedDummySquadCard.transform.position;
        transform.SetSiblingIndex(newIndex);
        layoutElement.ignoreLayout = false;
        // Detach from parent immediately so it no longer occupies a layout slot.
        // Destroy() is deferred — if we don't unparent first, ForceRebuildLayoutImmediate
        // in OnSquadOrderReceived will include the dummy, shifting card positions by one
        // slot and misplacing GroupUI brackets.
        cachedDummySquadCard.transform.SetParent(null);
        Destroy(cachedDummySquadCard);
        cachedDummySquadCard = null;
        canvas.sortingOrder = 1;
        BattleInputManager.Instance.SetRearrangingSquads(false);
        IAudioRequester.Instance.PlaySFX(SFXData.RepositionCommand);
        BattleManager.Instance.SquadOrderManager.MoveSquad(squadEntity.SquadId, newIndex);
    }
    private void OnDisable()
    {
        if(cachedDummySquadCard == null) return;

        cachedDummySquadCard.transform.SetParent(null);
        Destroy(cachedDummySquadCard);
        cachedDummySquadCard = null;
        layoutElement.ignoreLayout = false;
        canvas.sortingOrder = 1;
        BattleInputManager.Instance.SetRearrangingSquads(false);
    }
    public bool RemovedByUIManager { get; set; }

    private void OnDestroy()
    {
        // Card can be destroyed by FixedUpdate when ECS components are gone (e.g. withdraw),
        // bypassing the normal UIManager.RemoveSquad path. Notify UIManager so it cleans up
        // squadDisplays and SquadOrderManager. If UIManager already removed it first via
        // RemoveSquad, the flag is set and we skip to avoid SetParent on a destroying object.
        if (RemovedByUIManager) return;
        RemovedByUIManager = true;
        if(BattleManager.HasInstance && BattleManager.Instance.UIManager != null)
            BattleManager.Instance.UIManager.RemoveSquad(squadId);
    }
    public void SetBroken(bool isBroken)
    {
        if(isBroken)
        {
            brokenGO.SetActive(true);
            _presenter.HealthSlider.gameObject.SetActive(false);
        }
        else
        {
            brokenGO.SetActive(false);
            _presenter.HealthSlider.gameObject.SetActive(true);
        }
    }
}
}
