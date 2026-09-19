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
        private const float ChargeShoutIntervalMin = 1.5f;
        private const float ChargeShoutIntervalMax = 3f;
        // Where a charge shout's linear rolloff reaches silence.
        private const float ChargeShoutMaxDistance = 60f;
        private const float _fadeOutDuration = 2f;

        private VoiceSFX _voiceSFX;
        private bool _isInfantry;
        // Effects slider value for the looping sources. One-shots go through SFXManager, which applies its own channel.
        private float _baseVolume = 1f;
        private Coroutine _chargeShoutCoroutine;
        private Coroutine _movingFadeCoroutine;
        private Coroutine _secondaryFadeCoroutine;
        private Coroutine _combatFadeCoroutine;
        private bool _isMoving;
        private bool _isInCombat;
        private bool _gamePaused;

        public void Initialize(VoiceSFX voiceSFX, bool isInfantry)
        {
            _voiceSFX = voiceSFX;
            _isInfantry = isInfantry;

            movingSource.enabled = false;
            secondaryLoopingSource.enabled = false;
            combatLoopingSource.enabled = false;

            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
        }

        private void OnGamePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Battle) return;
            movingSource.enabled = true;
            secondaryLoopingSource.enabled = true;
            combatLoopingSource.enabled = true;
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
            }
            else
            {
                if (_isMoving) movingSource.UnPause();
                if (_isInfantry && _isMoving) secondaryLoopingSource.UnPause();
                if (_isInCombat) combatLoopingSource.UnPause();
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

        public void StartChargeSound(Vector3 squadCenter)
        {
            _isMoving = true;
            RefreshMovingSource();

            if (_isInfantry)
            {
                CancelFade(ref _secondaryFadeCoroutine, secondaryLoopingSource);
                secondaryLoopingSource.volume = _baseVolume;
                secondaryLoopingSource.loop = true;
                secondaryLoopingSource.Play();
            }

            if (_chargeShoutCoroutine != null) StopCoroutine(_chargeShoutCoroutine);
            _chargeShoutCoroutine = StartCoroutine(ChargeShoutLoop(squadCenter));
        }

        public void StopChargeSound()
        {
            _isMoving = false;
            RefreshMovingSource();

            if (_isInfantry) StartFadeOut(ref _secondaryFadeCoroutine, secondaryLoopingSource);
            if (_chargeShoutCoroutine != null)
            {
                StopCoroutine(_chargeShoutCoroutine);
                _chargeShoutCoroutine = null;
            }
        }

        public void StartCombatSound()
        {
            _isInCombat = true;
            RefreshMovingSource();
            if (!combatLoopingSource.isPlaying)
            {
                CancelFade(ref _combatFadeCoroutine, combatLoopingSource);
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
            if (_isMoving)
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
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
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

        private IEnumerator ChargeShoutLoop(Vector3 squadCenter)
        {
            int lastIndex = -1;
            while (true)
            {
                if (_voiceSFX != null && _voiceSFX.chargeSFX != null && _voiceSFX.chargeSFX.Length > 0)
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
        }
    }
}
