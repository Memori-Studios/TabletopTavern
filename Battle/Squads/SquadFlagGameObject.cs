using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.UI;
using System.Threading.Tasks;
using QuickOutline;
using Memori.Audio;
using Memori.Notifications;
using TJ.Morale;
using Memori.Localization;

namespace TJ
{
    [RequireComponent(typeof(Animator), typeof(BattlefieldBiomeDetector))]
    public class SquadFlagGameObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer flagMeshRenderer;
        [SerializeField] private GameObject normalRarityFlagPostGO, legendaryRarityFlagPostGO;
        [SerializeField] private Transform flagTransform, healthBarTransform;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider moraleBar;
        [SerializeField] private Slider ammoBar;
        [SerializeField] private QuickOutline.Outline outline;
        [SerializeField] private SquadSFXManager squadSFXManager;
        [SerializeField] private Image minimapImage;
        [SerializeField] private ParticleSystem weaponStrengthEffect;
        public int SquadId => squadId;
        public bool IsInCombat { get; private set; }
        public SquadSFXManager SFXManager => squadSFXManager;
        private bool isSelected = false;
        private bool isHovered = false;
        private bool isHidden = false;
        private UnitSize unitSize;
        private Vector3 offset;
        private int squadId;
        private Animator animator;
        private HealthBar healthBarGO;
        private EntityManager EntityManager;
        private Entity squadEntity;
        BattleManager battleManager;
        SquadManager squadManager;
        UnitSelectionManager unitSelectionManager;
        private int ammunition = 0;
        bool broken;
        bool battleEnded;
        const float BAR_UPDATE_SPEED = 0.25f;
        const float MORALE_THRESHOLD = 0.45f;
        float updateTimer = 0f;
        int slowUpdateTick = 0;
        bool isBelowMoraleThreshold = false;

        private MaterialPropertyBlock _block;
        bool isRanged, isArtillery, isGate;

        // Latched from the unit type at SetUp rather than read from MageSquad on demand: by the time
        // the pool runs dry SquadRanOutOfAmmoSystem has already stripped MageSquad, so a live
        // component check would report false exactly when the readout needs to know.
        bool isMage;

        private static readonly int LowMoraleID = Shader.PropertyToID("_LowMorale");
        private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
        private static readonly int CameraHideID = Shader.PropertyToID("_CameraHide");

        public void SetUp(Material _flagMaterial, int _squadId, UnitSize _unitSize, MoraleComponent morale, Entity _entity, int _ammunition, UnitName unitName)
        {
            battleManager = BattleManager.Instance;
            squadManager = battleManager.SquadManager;
            unitSelectionManager = battleManager.UnitSelectionManager;

            squadEntity = _entity;

            EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            VoiceSFX voiceSFX = TabletopTavernData.Instance.SquadAssetsDictionary[unitName].voiceSFX;
            bool isInfantry = TabletopTavernData.Instance.GetUnitSizeFromUnitName(unitName) == UnitSize.Infantry;
            squadSFXManager.Initialize(voiceSFX, isInfantry);
            flagMeshRenderer.material = _flagMaterial;
            squadId = _squadId;
            unitSize = _unitSize;

            bool isLegendary = TabletopTavernData.Instance.GetSquadStats(unitName).RarityTier == UnitRarity.Legendary;
            normalRarityFlagPostGO.SetActive(!isLegendary);
            legendaryRarityFlagPostGO.SetActive(isLegendary);
            ammunition = _ammunition;
            isMage = TabletopTavernConstants.Casts(TabletopTavernData.Instance.GetUnitTypeFromUnitName(unitName));
            broken = false;
            _block = new MaterialPropertyBlock();

            if (unitSize == UnitSize.Monstrous)
            {
                offset = new Vector3(0, 2.5f, 0);
            }
            else if (unitSize == UnitSize.SingleUnit)
            {
                offset = new Vector3(2.5f, 1, 0);
            }

            if(unitName == UnitName.Gate)
            {
                offset = new Vector3(0, 1, 0);
                isGate = true;
            }

            animator = GetComponent<Animator>();
            outline.enabled = false;
            outline.OutlineColor = squadId < 0 ? (Color)ColorData.HexToRgba(ColorData.EnemyTeamOutline) : (Color)ColorData.HexToRgba(ColorData.PlayerTeamOutline);

            this.gameObject.SetActive(true);
            Team team = _squadId < 0 ? Team.Enemy : Team.Player;
            healthBarGO = battleManager.UIManager.LoadHealthbar(healthBarTransform, team, squadId, ammunition, unitName == UnitName.Gate);
            healthBar = healthBarGO.HealthSlider;
            healthBar.value = 1;

            moraleBar = healthBarGO.MoraleSlider;
            moraleBar.maxValue = morale.MaxMorale;
            moraleBar.value = morale.MaxMorale;
            moraleBar.minValue = morale.MoraleThreshold;

            healthBarGO.SetPrestige(squadManager.GetSquadPrestige(squadId), ammunition > 0);

            battleManager.OnSquadBrokenEvent += OnSquadBroken;
            battleManager.OnGamePhaseChanged += OnGamePhaseChanged;
            squadManager.OnTerrifiedSquadsChanged += OnTerrifiedSquadsChanged;
            squadManager.OnChargingSquadsChanged += OnChargingSquadsChanged;
            squadManager.OnDestroyedSquad += OnDestroyedSquad;
            squadManager.OnBattlefieldBonusApplied += OnBattlefieldBonusApplied;
            unitSelectionManager.OnSelectedSquadsChanged += OnSelectedSquadsChanged;
            unitSelectionManager.OnHoverSquadsChanged += OnHoverSquadsChanged;

            GetComponent<BattlefieldBiomeDetector>().SetSquadEntityId(squadId, squadEntity);
            minimapImage.color = ColorData.GetTeamMinimapColor(team == Team.Player);
            squadSFXManager.SetBaseVolume(IAudioRequester.Instance.sFXVolume.GetValue());
            IAudioRequester.Instance.sFXVolume.OnValueChanged += squadSFXManager.SetBaseVolume;

            //if ranged
            if (ammunition>0)
            {
                isRanged = unitSize != UnitSize.Artillery;
                isArtillery = unitSize == UnitSize.Artillery;
                ammoBar = healthBarGO.AmmoSlider;
                ammoBar.gameObject.SetActive(true);
                ammoBar.maxValue = ammunition;
                ammoBar.value = ammunition;
            }
            else
            {
                ammoBar = healthBarGO.AmmoSlider;
                ammoBar.gameObject.SetActive(false);
            }
        }
        private void Start()
        {
            // capsuleCollider.enabled = true;
            // capsuleCollider.isTrigger = true;
        }

        // Billboarding lives here rather than in FixedUpdate so it keeps working while the
        // game is paused (timeScale 0 stops FixedUpdate entirely), and so it runs after the
        // camera has moved this frame.
        void LateUpdate()
        {
            if (Camera.main == null) return;

            //rotate to face camera
            Quaternion rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
            healthBarTransform.LookAt(Camera.main.transform);
            //modify the rotation so it only rotates on the y axis
            rotation.x = 0;
            rotation.z = 0;
            flagTransform.rotation = rotation;
        }

        void FixedUpdate()
        {
            if (!EntityManager.Exists(squadEntity)) Destroy(gameObject);

            void HandleFlagPosition()
            {
                Vector3 targetPosition = (Vector3)EntityManager.GetComponentData<SquadMovementComponent>(squadEntity).SquadCenter + offset;
                if (transform.position != targetPosition) {
                    transform.position = targetPosition;
                    Physics.SyncTransforms();
                }
            }
            void HandleHealthBar()
            {
                if (healthBarGO == null) return;

                SquadStateComponent squadTotalHealth = EntityManager.GetComponentData<SquadStateComponent>(squadEntity);
                healthBar.value = (float)squadTotalHealth.CurrentHealthValue / (float)squadTotalHealth.MaxHealthValue;
            }
            void HandleMoraleBar()
            {
                if (moraleBar == null) return;

                MoraleComponent morale = EntityManager.GetComponentData<MoraleComponent>(squadEntity);
                moraleBar.value = morale.CurrentMorale;

                if(morale.CurrentMorale/morale.MaxMorale < MORALE_THRESHOLD)
                {
                    if(!isBelowMoraleThreshold)
                    {
                        isBelowMoraleThreshold = true;
                        _block.SetFloat(LowMoraleID, 1f);
                        flagMeshRenderer.SetPropertyBlock(_block);
                    }
                }
                else
                {
                    if(isBelowMoraleThreshold)
                    {
                        isBelowMoraleThreshold = false;
                        _block.SetFloat(LowMoraleID, 0f);
                        flagMeshRenderer.SetPropertyBlock(_block);
                    }
                }
            }
            void HandleAmmoBar()
            {
                if(!isRanged && !isArtillery) return;

                if (ammoBar == null) return;

                // Keyed on SquadAmmunition, not RangedSquad, so a mage's charges drive the same bar.
                // A mage never carries RangedSquad, so testing for that component here read as
                // instantly-out-of-ammo and dropped its range drawer on the first update.
                if (ammunition > 0 && EntityManager.HasComponent<SquadAmmunition>(squadEntity))
                {
                    ammoBar.value = EntityManager.GetComponentData<SquadAmmunition>(squadEntity).Value;
                }
                else if (ammoBar.gameObject.activeSelf)
                {
                    if (squadId > 0)
                    {
                        string noAmmoLocalized = LocalizationManager.Instance.GetText(isMage ? "SquadOutOfCharges" : "SquadOutOfAmmo");
                        NotificationManager.Instance.DisplayNotification(noAmmoLocalized);
                    }

                    battleManager.SquadManager.RemoveArcherRangeDrawer(squadId);
                    battleManager.UIManager.UpdateAttackArrowToMelee(squadId, true);

                    ammoBar.gameObject.SetActive(false);
                    healthBarGO.SetOutOfAmmo(true);
                    if(battleManager.SquadManager.SquadRangeDrawers.ContainsKey(squadId))
                        battleManager.SquadManager.SquadRangeDrawers[squadId].SwitchToMelee(true);
                }
            }
            void HandleExhausted()
            {
                if (EntityManager.HasComponent<ExhaustedTag>(squadEntity))
                {
                    if (!healthBarGO.IsExhausted)
                    {
                        // if (squadId > 0 && hasntNotifiedOfExhausted)
                        // {
                        //     hasntNotifiedOfExhausted = false;
                        //     NotificationManager.Instance.DisplayNotification("One of your units has exhausted all Charges!");
                        // }
                        healthBarGO.SetExhausted(true);
                    }
                }
            }
            void HandleWeaponStrengthBonuses()
            {
                // Rage/BloodFrenzy/Slayer tags still drive the persistent healthbar icon on/off
                // (only reliable off-signal); the activation flash itself now comes from
                // OnBattlefieldBonusApplied (see SquadManager), which covers these plus every
                // other battlefield bonus generically.
                if (EntityManager.HasComponent<RageActiveTag>(squadEntity) || EntityManager.HasComponent<BloodFrenzyActiveTag>(squadEntity) || EntityManager.HasComponent<SlayerActiveTag>(squadEntity))
                {
                    if (!healthBarGO.WeaponStrengthBonusActive)
                    {
                        healthBarGO.SetWeaponStrengthActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.WeaponStrengthBonusActive)
                    {
                        healthBarGO.SetWeaponStrengthActive(false);
                    }
                }
            }
            void HandleArmorSundered()
            {
                if (EntityManager.HasComponent<ArmorSunderedTag>(squadEntity))
                {
                    if (!healthBarGO.ArmorSunderedActive)
                    {
                        healthBarGO.SetArmorSunderedActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.ArmorSunderedActive)
                    {
                        healthBarGO.SetArmorSunderedActive(false);
                    }
                }
            }
            void HandleIsBeingFlanked()
            {
                if (EntityManager.IsComponentEnabled<TakingFlankingDamage>(squadEntity))
                {
                    if (!healthBarGO.IsTakingFlankingDamage)
                    {
                        healthBarGO.SetTakingFlankingDamageActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.IsTakingFlankingDamage)
                    {
                        healthBarGO.SetTakingFlankingDamageActive(false);
                    }
                }
            }
            void HandleFlanking()
            {
                if (EntityManager.IsComponentEnabled<IsFlanking>(squadEntity))
                {
                    if (!healthBarGO.IsFlanking)
                    {
                        healthBarGO.SetFlankingActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.IsFlanking)
                    {
                        healthBarGO.SetFlankingActive(false);
                    }
                }
            }
            void HandleFireDamageTaking()
            {
                if (EntityManager.IsComponentEnabled<TakingFireDamage>(squadEntity))
                {
                    if (!healthBarGO.IsTakingFireDamage)
                    {
                        healthBarGO.SetTakingFireDamageActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.IsTakingFireDamage)
                    {
                        healthBarGO.SetTakingFireDamageActive(false);
                    }
                }
            }
            void HandleFireAtWill()
            {
                if(!isRanged) return;
                
                if (EntityManager.HasComponent<RangedSquad>(squadEntity))
                {
                    RangedFireModeSquadComponent rangedFireMode = EntityManager.GetComponentData<RangedFireModeSquadComponent>(squadEntity);
                    if (rangedFireMode.FireMode == RangedFireMode.FireAtWill)
                    {
                        if (!healthBarGO.IsFireAtWill)
                        {
                            healthBarGO.SetFireAtWill(true);
                        }
                    }
                    else
                    {
                        if (healthBarGO.IsFireAtWill)
                        {
                            healthBarGO.SetFireAtWill(false);
                        }
                    }
                }
            }
            void HandleDefensiveStance()
            {
                if(!EntityManager.HasComponent<DefensiveStanceTag>(squadEntity)) return;

                if (EntityManager.IsComponentEnabled<DefensiveStanceTag>(squadEntity))
                {
                    if (!healthBarGO.IsDefensiveStance)
                    {
                        healthBarGO.SetDefensiveStanceActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.IsDefensiveStance)
                    {
                        healthBarGO.SetDefensiveStanceActive(false);
                    }
                }
            }
            void HandleBraced()
            {
                if(!EntityManager.HasComponent<BracedTag>(squadEntity)) return;

                if (EntityManager.IsComponentEnabled<BracedTag>(squadEntity))
                {
                    if (!healthBarGO.IsBraced)
                    {
                        healthBarGO.SetBracedActive(true);
                    }
                }
                else
                {
                    if (healthBarGO.IsBraced)
                    {
                        healthBarGO.SetBracedActive(false);
                    }
                }
            }
            void HandleCameraHide()
            {
                if (!EntityManager.HasComponent<SquadCameraDistanceComponent>(squadEntity)) return;
                float dist = EntityManager.GetComponentData<SquadCameraDistanceComponent>(squadEntity).DistanceToCamera;
                bool inside = dist <= cameraHideRadius;
                if (inside && !_cameraInside) { _cameraInside = true; Hide(); }
                else if (!inside && _cameraInside) { _cameraInside = false; Reveal(); }
            }

            HandleFlagPosition();
            HandleCameraHide();

            if (battleEnded) return;
            if (broken) return;

            updateTimer += Time.fixedDeltaTime;
            if(updateTimer < BAR_UPDATE_SPEED) return;
            updateTimer = 0f;

            HandleHealthBar();
            if (isGate) return;
            HandleMoraleBar();
            HandleAmmoBar();
            HandleExhausted();
            HandleWeaponStrengthBonuses();
            HandleArmorSundered();
            HandleIsBeingFlanked();
            HandleFlanking();
            HandleFireDamageTaking();
            HandleFireAtWill();
            HandleDefensiveStance();
            HandleBraced();

            slowUpdateTick++;
            if (slowUpdateTick >= 4)
            {
                slowUpdateTick = 0;
                if (battleManager.GamePhase == GamePhase.PostGame)
                {
                    battleEnded = true;
                    squadSFXManager.StopCombatSound();
                    squadSFXManager.StopChargeSound();
                    return;
                }

                IsInCombat = EntityManager.HasComponent<InCombat>(squadEntity);
                bool isRangedEngaged = EntityManager.HasComponent<FormationEngagedInRangedCombat>(squadEntity);
                if (IsInCombat)
                {
                    squadSFXManager.StopChargeSound();
                    squadSFXManager.StartCombatSound();
                }
                else if (isRangedEngaged)
                {
                    squadSFXManager.StopChargeSound();
                    squadSFXManager.StopCombatSound();
                }
                else squadSFXManager.StopCombatSound();
            }
        }

        #region Event Handlers
        private void OnSelectedSquadsChanged(List<int> selectedSquadIds)
        {
            if (selectedSquadIds.Contains(squadId))
            {
                if (!isSelected)
                {
                    animator.SetBool("isSelected", true);
                    outline.enabled = !isHidden;
                    isSelected = true;
                }
            }
            else
            {
                if (isSelected)
                {
                    animator.SetBool("isSelected", false);
                    outline.enabled = false;
                    isSelected = false;
                }
            }
        }
        private void OnHoverSquadsChanged(List<int> hoveredSquadIds)
        {
            if (hoveredSquadIds.Contains(squadId))
            {
                if (!isHovered)
                {
                    outline.enabled = !isHidden;
                    isHovered = true;
                }
            }
            else
            {
                if (isHovered)
                {
                    isHovered = false;
                    if (!isSelected)
                    {
                        outline.enabled = false;
                    }
                }
            }
        }
        private void OnSquadBroken(int _brokenSquadId)
        {
            if (_brokenSquadId != squadId) return;

            // Debug.Log($"SquadFlagGameObject: OnSquadBroken {squadId}");
            healthBarGO.OnBroken();
            _block.SetFloat(AlphaID, 0.75f);
            flagMeshRenderer.SetPropertyBlock(_block);
            //change the color to set the alpha to 0.5
            minimapImage.color = new Color(minimapImage.color.r, minimapImage.color.g, minimapImage.color.b, 0.5f);
            broken = true;
            _block.SetFloat(LowMoraleID, 0f);
            flagMeshRenderer.SetPropertyBlock(_block);
            //stop all sfx
            squadSFXManager.StopCombatSound();
            squadSFXManager.StopChargeSound();
        }
        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.PostGame) 
            {
                squadSFXManager.StopCombatSound();
                squadSFXManager.StopChargeSound();
            }
        }
        private void OnChargingSquadsChanged(List<int> _squads)
        {
            if (healthBarGO == null || isGate) return;
            healthBarGO.SetCharge(_squads.Contains(squadId));
        }
        private void OnTerrifiedSquadsChanged(List<int> _squads)
        {
            if (healthBarGO == null || isGate) return;
            healthBarGO.SetTerrified(_squads.Contains(squadId));
        }
        public void OnDestroyedSquad(int _destroyedSquadId)
        {
            if (_destroyedSquadId != squadId) return;
            Destroy(gameObject);
        }
        private void OnBattlefieldBonusApplied(int _squadId, BattlefieldBonusEnum _bonus, UnitStat _stat, float _value)
        {
            if (_squadId != squadId) return;
            weaponStrengthEffect.Play();
        }
        #endregion

        public void OnDestroy()
        {
            if (flagMeshRenderer != null)
                Destroy(flagMeshRenderer.material);

            if (battleManager != null)
            {
                battleManager.OnSquadBrokenEvent -= OnSquadBroken;
                battleManager.OnGamePhaseChanged -= OnGamePhaseChanged;
            }

            if (squadManager != null)
            {
                squadManager.OnTerrifiedSquadsChanged -= OnTerrifiedSquadsChanged;
                squadManager.OnChargingSquadsChanged -= OnChargingSquadsChanged;
                squadManager.OnDestroyedSquad -= OnDestroyedSquad;
                squadManager.OnBattlefieldBonusApplied -= OnBattlefieldBonusApplied;
            }

            if (unitSelectionManager != null)
            {
                unitSelectionManager.OnSelectedSquadsChanged -= OnSelectedSquadsChanged;
                unitSelectionManager.OnHoverSquadsChanged -= OnHoverSquadsChanged;
            }
            if(IAudioRequester.HasInstance)
                IAudioRequester.Instance.sFXVolume.OnValueChanged -= squadSFXManager.SetBaseVolume;
        }
        private Coroutine _cameraHideFadeCoroutine;
        private float _currentCameraHide = 1f;
        private const float FadeDuration = 0.4f;
        [SerializeField] private float cameraHideRadius = 10f;
        private bool _cameraInside = false;

        public void Hide()
        {
            isHidden = true;
            outline.enabled = false;
            StartFade(0f);
        }
        public void Reveal()
        {
            isHidden = false;
            outline.enabled = isSelected || isHovered;
            StartFade(1f);
        }
        private void StartFade(float target)
        {
            if (_cameraHideFadeCoroutine != null) StopCoroutine(_cameraHideFadeCoroutine);
            _cameraHideFadeCoroutine = StartCoroutine(FadeCameraHide(target));
        }
        private System.Collections.IEnumerator FadeCameraHide(float target)
        {
            float start = _currentCameraHide;
            float duration = Mathf.Abs(target - start) * FadeDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (flagMeshRenderer == null) yield break;
                elapsed += Time.deltaTime;
                _currentCameraHide = Mathf.Lerp(start, target, elapsed / duration);
                _block.SetFloat(CameraHideID, _currentCameraHide);
                flagMeshRenderer.SetPropertyBlock(_block);
                yield return null;
            }
            if (flagMeshRenderer == null) yield break;
            _currentCameraHide = target;
            _block.SetFloat(CameraHideID, target);
            flagMeshRenderer.SetPropertyBlock(_block);
        }
    }
}
