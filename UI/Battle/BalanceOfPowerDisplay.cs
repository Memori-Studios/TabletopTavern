using UnityEngine;
using UnityEngine.UI;
using System;
using Memori.Tooltip;
using Memori.Localization;
using Memori.Utilities;

namespace TJ
{
    public class BalanceOfPowerDisplay : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private GameObject enemyArmyLossesIndicator, playerArmyLossesIndicator;
        [SerializeField] private MemoriTooltipTrigger enemyArmyLossesTooltip, playerArmyLossesTooltip;
        public Action<Team> ArmyLossesTriggered;
        private bool _enemyHasTakenDamage = false;
        public bool EnemyHasTakenDamage => _enemyHasTakenDamage;
        bool playerLossesTriggered;
        bool enemyLossesTriggered;
        const float BAR_UPDATE_SPEED = 0.25f;
        float updateTimer = 0f;
        private void Start() 
        {
            slider.value = 0.5f;
            playerArmyLossesIndicator.SetActive(false);
            enemyArmyLossesIndicator.SetActive(false);

            playerArmyLossesTooltip.SetUpToolTip(
                LocalizationManager.Instance.GetText("PlayerArmyLossesTooltipTitle"),
                LocalizationManager.Instance.GetText("ArmyLossesTooltipDesc"));

            enemyArmyLossesTooltip.SetUpToolTip(
                LocalizationManager.Instance.GetText("EnemyArmyLossesTooltipTitle"),
                LocalizationManager.Instance.GetText("ArmyLossesTooltipDesc"));
            
            BattleManager.Instance.OnGateDestroyed += GateDestroyedHandler;

            CacheBarColors();
            ApplyBarColors();
            ColorVision.Changed += ApplyBarColors;
        }

        #region Colorblind Mode

        // The fill is the player's share and the background behind it is the enemy's.
        private Image _playerFill, _enemyBackground;
        private Color _playerFillOff, _enemyBackgroundOff;

        private void CacheBarColors()
        {
            if (slider.fillRect != null) _playerFill = slider.fillRect.GetComponent<Image>();
            Transform background = slider.transform.Find("Background");
            if (background != null) _enemyBackground = background.GetComponent<Image>();
            if (_playerFill == null || _enemyBackground == null) Debug.LogError("BalanceOfPowerDisplay: bar fill or background Image not found; Colorblind Mode cannot recolour it.");
            if (_playerFill != null) _playerFillOff = _playerFill.color;
            if (_enemyBackground != null) _enemyBackgroundOff = _enemyBackground.color;
            _playerLossImages = TintedImages(playerArmyLossesIndicator, out _playerLossOff);
            _enemyLossImages = TintedImages(enemyArmyLossesIndicator, out _enemyLossOff);
        }

        // The loss skulls share one sprite and differ only by team colour.
        private Image[] _playerLossImages, _enemyLossImages;
        private Color[] _playerLossOff, _enemyLossOff;

        private static Image[] TintedImages(GameObject root, out Color[] offColors)
        {
            var tinted = new System.Collections.Generic.List<Image>();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                Color.RGBToHSV(image.color, out _, out float saturation, out _);
                if (saturation > 0.3f) tinted.Add(image);
            }
            offColors = new Color[tinted.Count];
            for (int i = 0; i < tinted.Count; i++) offColors[i] = tinted[i].color;
            return tinted.ToArray();
        }

        private void ApplyBarColors()
        {
            if (_playerFill != null) _playerFill.color = ColorVision.Good(_playerFillOff);
            if (_enemyBackground != null) _enemyBackground.color = ColorVision.Bad(_enemyBackgroundOff);
            for (int i = 0; i < _playerLossImages.Length; i++) _playerLossImages[i].color = ColorVision.Good(_playerLossOff[i]);
            for (int i = 0; i < _enemyLossImages.Length; i++) _enemyLossImages[i].color = ColorVision.Bad(_enemyLossOff[i]);
        }

        #endregion
        public void UpdateBalanceOfPowerDisplay(BalanceOfPower balanceOfPower)
        {
            updateTimer += Time.fixedDeltaTime;
            if(updateTimer < BAR_UPDATE_SPEED) return;
            updateTimer = 0f;

            // Debug.Log($"Updating Balance of Power Display - Player Health: {balanceOfPower.PlayerCurrentHealth}, Enemy Health: {balanceOfPower.EnemyCurrentHealth}");

            // Calculate the balance of power as a value between 0 and 1, 1 being all player health, 0 being all enemy health
            float totalHealth = balanceOfPower.PlayerCurrentHealth + balanceOfPower.EnemyCurrentHealth;
            float playerHealthRatio = totalHealth > 0 ? (float)balanceOfPower.PlayerCurrentHealth / totalHealth : 0.5f; // Default to 0.5 if both are 0 to show a neutral state
            slider.value = playerHealthRatio;

            if(BattleManager.Instance.GamePhase != GamePhase.Battle) return; // Only trigger losses during battle phase

            if( !_enemyHasTakenDamage && balanceOfPower.EnemyCurrentHealth < balanceOfPower.EnemyMaxHealth)
            {
                _enemyHasTakenDamage = true;
            }

            // Trigger events if one side has lost 75% of health
            if (balanceOfPower.PlayerCurrentHealth <= 0.25f * balanceOfPower.PlayerMaxHealth && !playerLossesTriggered)
            {
                Debug.Log($"Player has lost 75% of health. Triggering player losses event.");
                playerLossesTriggered = true;
                playerArmyLossesIndicator.SetActive(true);
                ArmyLossesTriggered?.Invoke(Team.Player);
            }
            if (balanceOfPower.EnemyCurrentHealth <= 0.25f * balanceOfPower.EnemyMaxHealth && !enemyLossesTriggered)
            {
                Debug.Log($"Enemy has lost 75% of health. Triggering enemy losses event.");
                enemyLossesTriggered = true;
                enemyArmyLossesIndicator.SetActive(true);
                ArmyLossesTriggered?.Invoke(Team.Enemy);
            }

            if(balanceOfPower.PlayerCurrentHealth <= 0f && playerLossesTriggered)
            {
                Debug.LogError($"Player has lost all health. BREAK GLASS HERE");
                BattleManager.Instance.EntityWatcher.EndBattle(false);
                
            }
            if(balanceOfPower.EnemyCurrentHealth <= 0f && enemyLossesTriggered)
            {
                Debug.LogError($"Enemy has lost all health. BREAK GLASS HERE");
                BattleManager.Instance.EntityWatcher.EndBattle(true);
            }
        }
        private void GateDestroyedHandler(int gateIndex) { }
        private void OnDestroy()
        {
            ColorVision.Changed -= ApplyBarColors;
            if(BattleManager.HasInstance)
                BattleManager.Instance.OnGateDestroyed -= GateDestroyedHandler;
        }
    }
}