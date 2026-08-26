using System.Threading.Tasks;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.Utilities;
using TMPro;
using TJ.Games;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    public class BattleDiceRollPanel : MonoBehaviour
    {
        [SerializeField] private Dice _dice;
        [SerializeField] private Button _rollButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _startBattleDirectButton;
        [SerializeField] private MemoriCanvasGroup _panelGroup;
        [SerializeField] private GameObject _dieObject;
        [SerializeField] private TMP_Text _winText;
        [SerializeField] private TMP_Text _winSubText;
        [SerializeField] private TMP_Text _loseText;
        [SerializeField] private TMP_Text _loseSubText;
        [SerializeField] private Color _winColor  = Color.green;
        [SerializeField] private Color _loseColor = Color.red;
        [SerializeField] private Color _dimColor  = Color.gray;
        [SerializeField] private Button _rerollButton;
        [SerializeField] private Button _useFateshineElixirButton;
        [SerializeField] private Button _useRewindButton;

        private void Awake()
        {
            _dieObject.SetActive(false);
            _startBattleDirectButton.gameObject.SetActive(false);
            if (_useFateshineElixirButton != null) _useFateshineElixirButton.gameObject.SetActive(false);
            if (_useRewindButton != null) _useRewindButton.gameObject.SetActive(false);
#if !UNITY_EDITOR
            _rerollButton.gameObject.SetActive(false);
#endif
        }

        public bool StartBattleRequested { get; private set; }

        public async Task<int> ShowAndRoll()
        {
            CampaignSaveData saveData = SaveDataHandler.Load();
            bool armed = saveData.fateshineElixirArmed;
            bool hasFateshineElixir = saveData.consumables.Contains(ConsumableEnum.FateshineElixir);

            // Drunk-on-the-map guarantee: auto-apply to this initiative roll and clear the persisted flag.
            if (armed)
            {
                saveData.fateshineElixirArmed = false;
                SaveDataHandler.SaveCampaign(saveData);
                IAudioRequester.Instance.PlaySFX(SFXData.Drink);
                return await AutoRoll(6);
            }

            if (SettingsManager.Instance.AutoRollInitiative.Value && !hasFateshineElixir)
                return await AutoRoll();

            StartBattleRequested = false;
            _panelGroup.CGEnable();
            if (_dieObject   != null) _dieObject.SetActive(true);
            _dice.StartPrespin();
            if (_winText     != null) _winText.color     = _dimColor;
            if (_winSubText  != null) _winSubText.color  = _dimColor;
            if (_loseText    != null) _loseText.color    = _dimColor;
            if (_loseSubText != null) _loseSubText.color = _dimColor;
            _continueButton.gameObject.SetActive(false);
            if (_startBattleDirectButton != null) _startBattleDirectButton.gameObject.SetActive(false);
            _rollButton.gameObject.SetActive(true);

            if (_useFateshineElixirButton != null) _useFateshineElixirButton.gameObject.SetActive(hasFateshineElixir);

            bool useElixir = false;
            var rollTCS = new TaskCompletionSource<bool>();
            _rollButton.onClick.AddListener(() => rollTCS.TrySetResult(true));
            if (_useFateshineElixirButton != null)
                _useFateshineElixirButton.onClick.AddListener(() => { useElixir = true; rollTCS.TrySetResult(true); });
            await rollTCS.Task;
            _rollButton.onClick.RemoveAllListeners();
            _rollButton.gameObject.SetActive(false);
            if (_useFateshineElixirButton != null)
            {
                _useFateshineElixirButton.onClick.RemoveAllListeners();
                _useFateshineElixirButton.gameObject.SetActive(false);
            }
            _dice.StopPrespin();

            int result = useElixir ? 6 : Random.Range(1, 7);
            if (useElixir)
            {
                CampaignSaveData elixirSaveData = SaveDataHandler.Load();
                elixirSaveData.consumables.Remove(ConsumableEnum.FateshineElixir);
                SaveDataHandler.SaveCampaign(elixirSaveData);
                IAudioRequester.Instance.PlaySFX(SFXData.Drink);
            }

            bool reroll = true;
            while (reroll)
            {
                await AnimateRoll(result);

                string resultSFX = result switch
                {
                    1 or 2 or 3 => SFXData.Failure,
                    4 or 5 or 6 => SFXData.Success,
                    _ => SFXData.DiceRoll
                };
                IAudioRequester.Instance.PlaySFX(resultSFX);
                _dice.SetOutlineColor(result >= 4 ? Color.green : Color.red);
                _dice.PulseOutline();

                ShowResult(result);

                _continueButton.gameObject.SetActive(true);

                bool lostRoll = result <= 3;
                bool hasRewind = lostRoll && SaveDataHandler.Load().consumables.Contains(ConsumableEnum.Rewind);
                if (_useRewindButton != null) _useRewindButton.gameObject.SetActive(hasRewind);

                var choiceTCS = new TaskCompletionSource<bool>(); // true = continue, false = reroll
                _continueButton.onClick.AddListener(() => choiceTCS.TrySetResult(true));
                if (hasRewind && _useRewindButton != null)
                    _useRewindButton.onClick.AddListener(() =>
                    {
                        CampaignSaveData rewindSaveData = SaveDataHandler.Load();
                        rewindSaveData.consumables.Remove(ConsumableEnum.Rewind);
                        SaveDataHandler.SaveCampaign(rewindSaveData);
                        IAudioRequester.Instance.PlaySFX(SFXData.Drink);
                        choiceTCS.TrySetResult(false);
                    });
#if UNITY_EDITOR
                _rerollButton.gameObject.SetActive(true);
                _rerollButton.onClick.AddListener(() => choiceTCS.TrySetResult(false));
#endif
                reroll = !await choiceTCS.Task;
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.gameObject.SetActive(false);
                if (_useRewindButton != null)
                {
                    _useRewindButton.onClick.RemoveAllListeners();
                    _useRewindButton.gameObject.SetActive(false);
                }

                if (reroll)
                    result = Random.Range(1, 7);
            }

            if (!StartBattleRequested)
            {
                BattleManager.Instance.SetGamePhase(GamePhase.Deployment);
                BattleManager.Instance.UIManager.ShowStartBattleButton();
            }
            _panelGroup.FadeOutAsync();
            if (_dieObject != null) _dieObject.SetActive(false);
            return result;
        }

        private async Task AnimateRoll(int result)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _dice.PlayLoadFeedback();
            await _dice.AnimateToFace(result);
            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);
        }

        private void ShowResult(int result)
        {
            Debug.Log($"[BattleDiceRollPanel] Player rolled a {result}.");
            bool playerSecond = result >= 4;
            if (_winText     != null) _winText.color     = playerSecond ? _winColor  : _dimColor;
            if (_winSubText  != null) _winSubText.color  = playerSecond ? _winColor  : _dimColor;
            if (_loseText    != null) _loseText.color    = playerSecond ? _dimColor  : _loseColor;
            if (_loseSubText != null) _loseSubText.color = playerSecond ? _dimColor  : _loseColor;
        }

        private async Task<int> AutoRoll(int forcedResult = 0)
        {
            StartBattleRequested = false;
            _panelGroup.CGEnable();
            if (_dieObject != null) _dieObject.SetActive(true);
            _rollButton.gameObject.SetActive(false);
            _continueButton.gameObject.SetActive(false);
            if (_startBattleDirectButton != null) _startBattleDirectButton.gameObject.SetActive(false);
            if (_winText     != null) _winText.color     = _dimColor;
            if (_winSubText  != null) _winSubText.color  = _dimColor;
            if (_loseText    != null) _loseText.color    = _dimColor;
            if (_loseSubText != null) _loseSubText.color = _dimColor;

            int result = forcedResult > 0 ? forcedResult : Random.Range(1, 7);
            await AnimateRoll(result);

            string resultSFX = result switch
            {
                1 or 2 or 3 => SFXData.Failure,
                4 or 5 or 6 => SFXData.Success,
                _ => SFXData.DiceRoll
            };
            IAudioRequester.Instance.PlaySFX(resultSFX);
            _dice.SetOutlineColor(result >= 4 ? Color.green : Color.red);
            _dice.PulseOutline();
            ShowResult(result);

            await Task.Delay(2000);

            BattleManager.Instance.SetGamePhase(GamePhase.Deployment);
            BattleManager.Instance.UIManager.ShowStartBattleButton();
            _panelGroup.FadeOutAsync();
            if (_dieObject != null) _dieObject.SetActive(false);
            return result;
        }
    }
}
