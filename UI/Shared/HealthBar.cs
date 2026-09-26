using UnityEngine;
using UnityEngine.UI;
using Memori.Utilities;

namespace TJ
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider, moraleSlider, ammoSlider;
        [SerializeField] private Slider cooldownSlider;
        public Slider HealthSlider => healthSlider;
        public Slider MoraleSlider => moraleSlider;
        public Slider AmmoSlider => ammoSlider;
        public Slider CooldownSlider => cooldownSlider;
        [SerializeField] private Image brokenImage;
        [SerializeField] private Image fill;
        public Image Fill => fill;
        [SerializeField] private Color playerColor, enemyColor;
        [SerializeField] private Transform squadTransform;
        public Transform SquadTransform => squadTransform;

        [Header("Status Effect Icons")]
        [SerializeField] private GameObject fireAtWillGO;
        [SerializeField] private GameObject chargeGO, terrifiedGO, exhaustedGO, weaponStrengthGO, armorSunderedGO, isTakingFlankingDamageGO, isFlankingGO, isOnFireGO, defensiveStanceGO, bracedGO, outOfAmmoGO;
        [Header("Spell Status Icons")]
        // Filled in cast order from the squad's SpellStatusBufferElement (see SquadFlagGameObject.HandleSpellStatus).
        [SerializeField] private Image[] spellStatusSlots;

        [Header("Prestige Icons")]
        [SerializeField] private GameObject prestige1GO;
        [SerializeField] private GameObject prestige2GO, prestige1RangedGO, prestige2RangedGO;
        // Casters get their own pair: the archer frames are drawn around a 3-bar stack and the cooldown row sits outside them.
        [SerializeField] private GameObject prestige1MageGO, prestige2MageGO;
        private bool _isExhausted = false;
        public bool IsExhausted => _isExhausted;
        private bool _weaponStrengthBonusActive = false;
        public bool WeaponStrengthBonusActive => _weaponStrengthBonusActive;
        private bool _armorSunderedActive = false;
        public bool ArmorSunderedActive => _armorSunderedActive;
        private bool _isTakingFlankingDamage = false;
        public bool IsTakingFlankingDamage => _isTakingFlankingDamage;
        private bool _isFlanking = false;
        public bool IsFlanking => _isFlanking;
        private bool _isTakingFireDamage = false;
        public bool IsTakingFireDamage => _isTakingFireDamage;
        private bool _isFireAtWill = false;
        public bool IsFireAtWill => _isFireAtWill;
        private bool _isDefensiveStance = false;
        public bool IsDefensiveStance => _isDefensiveStance;
        private bool _isBraced = false;
        public bool IsBraced => _isBraced;
        private bool _isOutOfAmmo = false;
        public bool IsOutOfAmmo => _isOutOfAmmo;
        
        private bool isPlayer;

        public void SetUp(Team _team, Transform _squadTransform, int ammunition, bool isGate, bool hasCooldown)
        {
            squadTransform = _squadTransform;
            isPlayer = _team == Team.Player;
            ApplyTeamColor();
            ColorVision.Changed -= ApplyTeamColor;
            ColorVision.Changed += ApplyTeamColor;
            DisableAllStatusIcons();

            // Only artillery and casters have a cycle worth a bar, so for everything else the row is
            // switched off entirely rather than sitting empty under the ammo bar.
            if (cooldownSlider != null)
            {
                cooldownSlider.gameObject.SetActive(hasCooldown);
                cooldownSlider.minValue = 0f;
                cooldownSlider.maxValue = 1f;
                cooldownSlider.value = 1f;
            }
            
            if (ammunition > 0)
            {
                ammoSlider.gameObject.SetActive(true);
                ammoSlider.maxValue = ammunition;
                ammoSlider.value = ammunition;
            }
            else
            {
                ammoSlider.gameObject.SetActive(false);
            }
            
            if(isGate)
            {
                moraleSlider.gameObject.SetActive(false);
            }
        }
        private void DisableAllStatusIcons()
        {
            chargeGO.SetActive(false);
            terrifiedGO.SetActive(false);
            exhaustedGO.SetActive(false);
            weaponStrengthGO.SetActive(false);
            armorSunderedGO.SetActive(false);
            SetSpellStatus(null, 0);
            isTakingFlankingDamageGO.SetActive(false);
            isFlankingGO.SetActive(false);
            isOnFireGO.SetActive(false);
            fireAtWillGO.SetActive(false);
            bracedGO.SetActive(false);
            defensiveStanceGO.SetActive(false);
            if (outOfAmmoGO != null) outOfAmmoGO.SetActive(false);
        }
        public void OnBroken()
        {
            if (healthSlider == null || healthSlider.gameObject == null) return;

            brokenImage.enabled = true;
            healthSlider.gameObject.SetActive(false);
            moraleSlider.gameObject.SetActive(false);
            ammoSlider.gameObject.SetActive(false);
            if (cooldownSlider != null) cooldownSlider.gameObject.SetActive(false);

            DisableAllStatusIcons();
            DisablePrestigeIcons();
        }
        public void SetCharge(bool isCharge)
        {
            if(IsExhausted) return;
            chargeGO.SetActive(isCharge);
        }
        public void SetTerrified(bool isTerrified)
        {
            terrifiedGO.SetActive(isTerrified);
        }
        public void SetExhausted(bool isExhausted)
        {
            _isExhausted = isExhausted;
            exhaustedGO.SetActive(isExhausted);
            chargeGO.SetActive(false);
        }
        public void SetWeaponStrengthActive(bool isActive)
        {
            _weaponStrengthBonusActive = isActive;
            if(weaponStrengthGO != null)
            weaponStrengthGO.SetActive(isActive);
        }
        public void SetArmorSunderedActive(bool isActive)
        {
            _armorSunderedActive = isActive;
            if(armorSunderedGO != null)
            armorSunderedGO.SetActive(isActive);
        }
        // Shows the first spellStatusSlots.Length entries of active and hides the rest. Sprites are resolved
        // here, not by the caller, so the poller only compares ids.
        public int SpellStatusSlotCount => spellStatusSlots == null ? 0 : spellStatusSlots.Length;
        public void SetSpellStatus(int[] activeSpellIds, int activeCount)
        {
            if (spellStatusSlots == null) return;
            for (int i = 0; i < spellStatusSlots.Length; i++)
            {
                Image slot = spellStatusSlots[i];
                if (slot == null) continue;
                Sprite sprite = null;
                Color color = Color.white;
                bool show = activeSpellIds != null && i < activeCount
                    && TJ.Spells.SpellStatusIcons.TryGet(activeSpellIds[i], out sprite, out color);
                if (show)
                {
                    slot.sprite = sprite;
                    slot.color = color;
                }
                slot.gameObject.SetActive(show);
            }
        }
        public void SetTakingFlankingDamageActive(bool isActive)
        {
            _isTakingFlankingDamage = isActive;
            if (isTakingFlankingDamageGO != null)
            isTakingFlankingDamageGO.SetActive(isActive);
        }
        public void SetFlankingActive(bool isActive)
        {
            _isFlanking = isActive;
            if (isFlankingGO != null)
            isFlankingGO.SetActive(isActive);
        }
        public void SetTakingFireDamageActive(bool isActive)
        {
            _isTakingFireDamage = isActive;
            if (isOnFireGO != null)
            isOnFireGO.SetActive(isActive);
        }
        public void SetFireAtWill(bool isFireAtWill)
        {
            _isFireAtWill = isFireAtWill;
            if (fireAtWillGO != null)
            fireAtWillGO.SetActive(isFireAtWill);
        }
        public void SetDefensiveStanceActive(bool isActive)
        {
            _isDefensiveStance = isActive;
            if (defensiveStanceGO != null)
            defensiveStanceGO.SetActive(isActive);
        }
        public void SetBracedActive(bool isActive)
        {
            _isBraced = isActive;
            if (bracedGO != null)
            bracedGO.SetActive(isActive);
        }
        public void SetOutOfAmmo(bool isOutOfAmmo)
        {
            _isOutOfAmmo = isOutOfAmmo;
            if (outOfAmmoGO != null)
            outOfAmmoGO.SetActive(isOutOfAmmo);
        }
        public void SetPrestige(int prestige, bool isRanged, bool isCaster)
        {
            bool melee = !isRanged && !isCaster;
            if (prestige1GO != null) prestige1GO.SetActive(melee && prestige == 1);
            if (prestige2GO != null) prestige2GO.SetActive(melee && prestige == 2);
            if (prestige1RangedGO != null) prestige1RangedGO.SetActive(isRanged && !isCaster && prestige == 1);
            if (prestige2RangedGO != null) prestige2RangedGO.SetActive(isRanged && !isCaster && prestige == 2);
            if (prestige1MageGO != null) prestige1MageGO.SetActive(isCaster && prestige == 1);
            if (prestige2MageGO != null) prestige2MageGO.SetActive(isCaster && prestige == 2);
        }
        public void DisablePrestigeIcons()
        {
            if (prestige1GO != null) prestige1GO.SetActive(false);
            if (prestige2GO != null) prestige2GO.SetActive(false);
            if (prestige1RangedGO != null) prestige1RangedGO.SetActive(false);
            if (prestige2RangedGO != null) prestige2RangedGO.SetActive(false);
            if (prestige1MageGO != null) prestige1MageGO.SetActive(false);
            if (prestige2MageGO != null) prestige2MageGO.SetActive(false);
        }
        private Image _enemyMarker;

        // Colorblind Mode also marks enemy bars with a diamond, so team reads by shape as well as colour.
        private void ApplyTeamColor()
        {
            fill.color = ColorVision.Team(isPlayer, isPlayer ? playerColor : enemyColor);

            bool showMarker = !isPlayer && ColorVision.IsOn;
            if (showMarker && _enemyMarker == null) _enemyMarker = CreateEnemyMarker();
            if (_enemyMarker != null)
            {
                _enemyMarker.gameObject.SetActive(showMarker);
                _enemyMarker.color = fill.color;
            }
        }
        private Image CreateEnemyMarker()
        {
            RectTransform bar = healthSlider.transform as RectTransform;
            float size = bar.rect.height * 1.6f;
            var marker = new GameObject("Enemy Marker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rt = marker.rectTransform;
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-size, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            marker.raycastTarget = false;
            return marker;
        }
        private void OnDestroy()
        {
            ColorVision.Changed -= ApplyTeamColor;
        }
    }
}