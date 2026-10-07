using System.Collections;
using Memori.Audio;
using UnityEngine;

namespace TJ
{
    public class SquadSFXManager : MonoBehaviour
    {
        [SerializeField] private AudioSource movingSource;  // plays only when moving
        [SerializeField] private AudioSource secondaryLoopingSource;  // infantry-only secondary loop
        [SerializeField] private AudioSource combatLoopingSource;     // plays only when in combat
        [Header("Weather march loops")]
        [SerializeField] private AudioClip snowMarchLoop;
        [SerializeField] private AudioClip mudMarchLoop;
        [Header("Heavy armor")]
        [SerializeField] private AudioClip armorRattleLoop;
        // Armor at or above this rattles while the squad moves.
        public const int HeavyArmorThreshold = 80;
        private const float MountCallChance = 0.5f;
        private const float ChargeShoutIntervalMin = 1.5f;
        private const float ChargeShoutIntervalMax = 3f;
        // Less flag travel than this between shouts counts as standing; a mage casting from range keeps its Attack order.
        private const float ChargeShoutMinTravel = 1f;
        // Where a charge shout's linear rolloff reaches silence.
        private const float ChargeShoutMaxDistance = 60f;
        // The roar as a sprint starts: several voices at once, heard from farther than a single shout.
        private const float SprintRoarMaxDistance = 110f;
        private const int SprintRoarModelsPerVoice = 10;
        private const int SprintRoarMaxVoices = 5;
        private const float SprintRoarSpread = 0.3f;
        private const float _fadeOutDuration = 2f;

        private VoiceSFX _voiceSFX;
        private MountSFX _mountSFX;
        private bool _isInfantry;
        // A one-model squad (single monster, mage) has no running, march or rattle loop.
        private bool _hasMoveLoops;
        // Runtime copy of secondaryLoopingSource; only heavy squads get one.
        private AudioSource _rattleSource;
        // Effects slider value for the looping sources. One-shots go through SFXManager, which applies its own channel.
        private float _baseVolume = 1f;
        private Coroutine _chargeShoutCoroutine;
        private Coroutine _movingFadeCoroutine;
        private Coroutine _secondaryFadeCoroutine;
        private Coroutine _combatFadeCoroutine;
        private Coroutine _rattleFadeCoroutine;
        private bool _isMoving;
        private bool _isInCombat;
        private bool _gamePaused;
        // Sources stay off through deployment; an order staged then is replayed when the battle starts.
        private bool _battleStarted;

        public void Initialize(VoiceSFX voiceSFX, bool isInfantry, MountSFX mountSFX, bool heavyArmor, Weather weather, bool singleModel)
        {
            _voiceSFX = voiceSFX;
            _mountSFX = mountSFX;
            _hasMoveLoops = !singleModel;
            _isInfantry = isInfantry && _hasMoveLoops;

            if (_mountSFX != null && _mountSFX.moveLoop != null) movingSource.clip = _mountSFX.moveLoop;
            if (weather == Weather.Snow && snowMarchLoop != null) secondaryLoopingSource.clip = snowMarchLoop;
            else if (weather == Weather.Rain && mudMarchLoop != null) secondaryLoopingSource.clip = mudMarchLoop;
            if (heavyArmor && _hasMoveLoops && armorRattleLoop != null) _rattleSource = CreateRattleSource();

            movingSource.enabled = false;
            secondaryLoopingSource.enabled = false;
            combatLoopingSource.enabled = false;
            if (_rattleSource != null) _rattleSource.enabled = false;

            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
        }

        private AudioSource CreateRattleSource()
        {
            AudioSource source = secondaryLoopingSource.gameObject.AddComponent<AudioSource>();
            source.clip = armorRattleLoop;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = secondaryLoopingSource.spatialBlend;
            source.rolloffMode = secondaryLoopingSource.rolloffMode;
            source.minDistance = secondaryLoopingSource.minDistance;
            source.maxDistance = secondaryLoopingSource.maxDistance;
            source.dopplerLevel = secondaryLoopingSource.dopplerLevel;
            source.spread = secondaryLoopingSource.spread;
            return source;
        }

        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Battle) return;
            _battleStarted = true;
            movingSource.enabled = true;
            secondaryLoopingSource.enabled = true;
            combatLoopingSource.enabled = true;
            if (_rattleSource != null) _rattleSource.enabled = true;
            if (_isMoving) StartChargeSound();
        }

        private void Update()
        {
            bool paused = Time.timeScale == 0;
            if (paused == _gamePaused) return;
            _gamePaused = paused;

            if (paused)
            {
                if (movingSource.isPlaying) movingSource.Pause();
                if (secondaryLoopingSource.isPlaying) secondaryLoopingSource.Pause();
                if (combatLoopingSource.isPlaying) combatLoopingSource.Pause();
                if (_rattleSource != null && _rattleSource.isPlaying) _rattleSource.Pause();
            }
            else
            {
                if (_isMoving && _hasMoveLoops) movingSource.UnPause();
                if (_isInfantry && _isMoving) secondaryLoopingSource.UnPause();
                if (_isInCombat) combatLoopingSource.UnPause();
                if (_rattleSource != null && _isMoving) _rattleSource.UnPause();
            }
        }

        // Single clip for a per-unit event. Death cries are Voices; melee hits and projectile fire are Effects.
        public void PlaySFX(AudioClip clip, Vector3 worldPosition, float maxDistance, AudioChannel channel)
        {
            if (Time.timeScale == 0) return;
            SFXManager.Instance.Play(clip, worldPosition, maxDistance, channel);
        }

        // Bark burst: N bark events accumulated by EntityWatcher, played at the squad center on Voices.
        public void PlayBarks(int count, Vector3 squadCenter, float maxDistance)
        {
            if (Time.timeScale == 0) return;
            if (_voiceSFX == null || _voiceSFX.idleSFX == null || _voiceSFX.idleSFX.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                AudioClip clip = _voiceSFX.idleSFX[Random.Range(0, _voiceSFX.idleSFX.Length)];
                SFXManager.Instance.Play(clip, squadCenter, maxDistance, AudioChannel.Voices);
            }
        }

        public void StartChargeSound()
        {
            _isMoving = true;
            if (!_battleStarted) return;
            RefreshMovingSource();

            if (_isInfantry)
            {
                CancelFade(ref _secondaryFadeCoroutine, secondaryLoopingSource);
                secondaryLoopingSource.volume = _baseVolume;
                secondaryLoopingSource.loop = true;
                secondaryLoopingSource.Play();
            }

            if (_rattleSource != null)
            {
                CancelFade(ref _rattleFadeCoroutine, _rattleSource);
                _rattleSource.volume = _baseVolume;
                _rattleSource.Play();
            }

        }

        // The shouts belong to the sprint, not to the order: a squad roars as it breaks into the charge.
        public void StartSprintRoar(int modelCount)
        {
            if (!_battleStarted) return;
            if (_chargeShoutCoroutine != null) StopCoroutine(_chargeShoutCoroutine);
            _chargeShoutCoroutine = StartCoroutine(SprintRoarThenShouts(modelCount));
        }

        // The mount's cry at the moment of impact; the clash clips themselves carry no animal.
        public void PlayMountCall(Vector3 position)
        {
            if (_mountSFX == null || _mountSFX.calls == null || _mountSFX.calls.Length == 0) return;
            AudioClip call = _mountSFX.calls[Random.Range(0, _mountSFX.calls.Length)];
            SFXManager.Instance.Play(call, position, SprintRoarMaxDistance, AudioChannel.Voices);
        }

        public void StopSprintRoar()
        {
            if (_chargeShoutCoroutine == null) return;
            StopCoroutine(_chargeShoutCoroutine);
            _chargeShoutCoroutine = null;
        }

        public void StopChargeSound()
        {
            _isMoving = false;
            RefreshMovingSource();

            if (_isInfantry) StartFadeOut(ref _secondaryFadeCoroutine, secondaryLoopingSource);
            if (_rattleSource != null) StartFadeOut(ref _rattleFadeCoroutine, _rattleSource);
            StopSprintRoar();
        }

        public void StartCombatSound()
        {
            _isInCombat = true;
            RefreshMovingSource();
            // Re-entering combat mid-fade keeps the bed; left running, the fade would stop it.
            CancelFade(ref _combatFadeCoroutine, combatLoopingSource);
            if (!combatLoopingSource.isPlaying)
            {
                combatLoopingSource.volume = _baseVolume;
                combatLoopingSource.loop = true;
                combatLoopingSource.Play();
            }
        }

        public void StopCombatSound()
        {
            _isInCombat = false;
            RefreshMovingSource();
            StartFadeOut(ref _combatFadeCoroutine, combatLoopingSource);
        }

        private void RefreshMovingSource()
        {
            if (_isMoving && _hasMoveLoops)
            {
                CancelFade(ref _movingFadeCoroutine, movingSource);
                if (!movingSource.isPlaying)
                {
                    movingSource.volume = _baseVolume;
                    movingSource.loop = true;
                    movingSource.Play();
                }
            }
            else
            {
                StartFadeOut(ref _movingFadeCoroutine, movingSource);
            }
        }

        private void CancelFade(ref Coroutine fadeCoroutine, AudioSource source)
        {
            if (fadeCoroutine == null) return;
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
            source.volume = _baseVolume;
        }

        private void StartFadeOut(ref Coroutine fadeCoroutine, AudioSource source)
        {
            if (!source.isPlaying) return;
            // Stops arrive every 4 frames in combat; restarting the fade each time kept the source playing at zero volume forever.
            if (fadeCoroutine != null) return;
            fadeCoroutine = StartCoroutine(FadeOut(source));
        }

        private IEnumerator FadeOut(AudioSource source)
        {
            float startVolume = source.volume;
            float t = 0f;
            while (t < _fadeOutDuration)
            {
                t += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, t / _fadeOutDuration);
                yield return null;
            }
            source.Stop();
            source.volume = _baseVolume;
        }

        private IEnumerator SprintRoarThenShouts(int modelCount)
        {
            bool hasShouts = _voiceSFX != null && _voiceSFX.chargeSFX != null && _voiceSFX.chargeSFX.Length > 0;
            if (hasShouts)
            {
                int voices = Mathf.Clamp(modelCount / SprintRoarModelsPerVoice, 1, SprintRoarMaxVoices);
                int start = Random.Range(0, _voiceSFX.chargeSFX.Length);
                for (int i = 0; i < voices; i++)
                {
                    AudioClip clip = _voiceSFX.chargeSFX[(start + i) % _voiceSFX.chargeSFX.Length];
                    SFXManager.Instance.Play(clip, transform.position, SprintRoarMaxDistance, AudioChannel.Voices);
                    yield return new WaitForSeconds(Random.Range(0f, SprintRoarSpread / voices * 2f));
                }
            }
            if (_mountSFX != null && _mountSFX.calls != null && _mountSFX.calls.Length > 0)
            {
                AudioClip call = _mountSFX.calls[Random.Range(0, _mountSFX.calls.Length)];
                SFXManager.Instance.Play(call, transform.position, SprintRoarMaxDistance, AudioChannel.Voices);
            }
            yield return new WaitForSeconds(Random.Range(ChargeShoutIntervalMin, ChargeShoutIntervalMax));
            yield return ChargeShoutLoop();
        }

        // Reads the flag's live position each shout so the cries travel with the squad.
        private IEnumerator ChargeShoutLoop()
        {
            int lastIndex = -1;
            bool firstShout = true;
            Vector3 lastCheck = transform.position;
            while (true)
            {
                Vector3 squadCenter = transform.position;
                // A battle that auto-pauses on start would otherwise shout once into the pause.
                if (Time.timeScale == 0) { yield return null; continue; }
                bool moving = firstShout || (squadCenter - lastCheck).sqrMagnitude >= ChargeShoutMinTravel * ChargeShoutMinTravel;
                firstShout = false;
                lastCheck = squadCenter;
                if (moving && _voiceSFX != null && _voiceSFX.chargeSFX != null && _voiceSFX.chargeSFX.Length > 0)
                {
                    int index = lastIndex;
                    if (_voiceSFX.chargeSFX.Length > 1)
                        while (index == lastIndex)
                            index = Random.Range(0, _voiceSFX.chargeSFX.Length);
                    else
                        index = 0;

                    lastIndex = index;
                    SFXManager.Instance.Play(_voiceSFX.chargeSFX[index], squadCenter, ChargeShoutMaxDistance, AudioChannel.Voices);
                }
                if (moving && _mountSFX != null && _mountSFX.calls != null && _mountSFX.calls.Length > 0 && Random.value < MountCallChance)
                {
                    AudioClip call = _mountSFX.calls[Random.Range(0, _mountSFX.calls.Length)];
                    SFXManager.Instance.Play(call, squadCenter, ChargeShoutMaxDistance, AudioChannel.Voices);
                }
                yield return new WaitForSeconds(Random.Range(ChargeShoutIntervalMin, ChargeShoutIntervalMax));
            }
        }

        private void OnDestroy()
        {
            StopChargeSound();
            StopCombatSound();
            if (BattleManager.HasInstance)
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
        }

        public void SetBaseVolume(float sfxVolume)
        {
            _baseVolume = sfxVolume;
            if (movingSource.isPlaying) movingSource.volume = _baseVolume;
            if (secondaryLoopingSource.isPlaying) secondaryLoopingSource.volume = _baseVolume;
            if (combatLoopingSource.isPlaying) combatLoopingSource.volume = _baseVolume;
            if (_rattleSource != null && _rattleSource.isPlaying) _rattleSource.volume = _baseVolume;
        }
    }
}
