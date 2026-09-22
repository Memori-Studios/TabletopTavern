using Memori.Audio;
using Memori.SaveData;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Collections;

namespace TJ
{
    public class BattlefieldEnvManager : MonoBehaviour
    {
        BattleFieldPreset battleFieldPreset;
        [SerializeField] private Light sun;
        [SerializeField] private float noonIntensity, eveningIntensity;
        [SerializeField] private float noonTemp, eveningTemp;
        [SerializeField] private Volume noonVolume, eveningVolume;

        [Header("Clear Skies")]
        [SerializeField] private Light clearSkyLight;

        [Header("Rain")]
        [SerializeField] private Volume rainVolume;
        [SerializeField] private GameObject rainObject;
        [SerializeField] private AudioSource rainAudioSource;
        // Placeholder loop sits near full scale; scale keeps rain a bed under the battle mix.
        private const float RainVolumeScale = 0.12f;
        [SerializeField] private BattlefieldBonusGameObject[] rainBattlefieldBonusObjects;
        private BattlefieldBonusGameObject[] cachedRainObjects;
        [SerializeField] private Light rainLight;

        [Header("Snow")]
        [SerializeField] private Volume snowVolume;
        [SerializeField] private GameObject snowObject;
        [SerializeField] private BattlefieldBonusGameObject[] snowBattlefieldBonusObjects;
        private BattlefieldBonusGameObject[] cachedSnowObjects;
        [SerializeField] private Light snowLight;

        [Header("Fog")]
        [SerializeField] private Volume fogVolume;
        [SerializeField] private GameObject fogObject, fogVoidObject;
        [SerializeField] private BattlefieldBonusGameObject[] fogBattlefieldBonusObjects;
        private BattlefieldBonusGameObject[] cachedFogObjects;
        [SerializeField] private Light fogLight;

        [Header("Weather audio")]
        [SerializeField] private SFXCue thunderCue;
        [SerializeField] private AudioClip clearSkiesWindLoop;
        [SerializeField] private AudioClip snowWindLoop;
        // Wind sits under the battle mix like the rain bed.
        private const float WindVolumeScale = 0.12f;
        private const float ThunderIntervalMin = 12f;
        private const float ThunderIntervalMax = 35f;
        private AudioSource windSource;
        private Coroutine thunderCoroutine;

        public Action<Weather> OnWeatherChanged;
        public Weather CurrentWeather => battleFieldPreset.weather;

        private void Awake() 
        {
            cachedRainObjects = new BattlefieldBonusGameObject[rainBattlefieldBonusObjects.Length];
            cachedSnowObjects = new BattlefieldBonusGameObject[snowBattlefieldBonusObjects.Length];
            cachedFogObjects = new BattlefieldBonusGameObject[fogBattlefieldBonusObjects.Length];
        }
        
        public void LoadTimeOfDay(BattleFieldPreset.TimeOfDay timeOfDay)
        {
            if (timeOfDay == BattleFieldPreset.TimeOfDay.Noon)
            {
                sun.colorTemperature = noonTemp;
                sun.intensity = noonIntensity;
                noonVolume.weight = 1;
                eveningVolume.weight = 0;
            }
            else
            {
                sun.colorTemperature = eveningTemp;
                sun.intensity = eveningIntensity;
                noonVolume.weight = 0;
                eveningVolume.weight = 1;
            }
        }
        public void LoadBattleConditions()
        {
            IAudioRequester.Instance.effectsVolume.OnValueChanged += RainSoundLevelChange;
            rainAudioSource.volume = RainVolumeScale * IAudioRequester.Instance.effectsVolume.GetValue();

            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            battleFieldPreset = SaveDataHandler.Load().battleFieldPreset;
            if (playerSaveData.customBattle) {
                battleFieldPreset.weather = Weather.ClearSkies;
            }

#if !UNITY_EDITOR
            Debug.Log($"Loading battle conditions: {battleFieldPreset.weather}");
#endif
            ToggleWeather(battleFieldPreset.weather);
        }
        public void RainSoundLevelChange(float _volume)
        {
            if(rainAudioSource != null)
                rainAudioSource.volume = RainVolumeScale * _volume;
            if (windSource != null)
                windSource.volume = WindVolumeScale * _volume;
        }
        public void ToggleWeather(Weather selectedWeather)
        {
#if !UNITY_EDITOR
            Debug.Log($"Toggling weather to: {selectedWeather.ToString()}");
#endif
            battleFieldPreset.weather = selectedWeather;
            switch (selectedWeather)
            {
                case Weather.ClearSkies:
                    ToggleRain(false);
                    ToggleSnow(false);
                    ToggleFog(false);
                    break;
                case Weather.Rain:
                    ToggleRain(true);
                    ToggleSnow(false);
                    ToggleFog(false);
                    break;
                case Weather.Snow:
                    ToggleRain(false);
                    ToggleSnow(true);
                    ToggleFog(false);
                    break;
                case Weather.Fog:
                    ToggleFog(true);
                    ToggleRain(false);
                    ToggleSnow(false);
                    break;
                default:
                    ToggleRain(false);
                    ToggleSnow(false);
                    ToggleFog(false);
                    break;
            }
            UpdateWeatherAudio(selectedWeather);
            OnWeatherChanged?.Invoke(selectedWeather);
        }

        #region Weather audio
        // ToggleWeather also runs from the Inspector; only a Play session gets sound.
        private void UpdateWeatherAudio(Weather weather)
        {
            if (!Application.isPlaying) return;

            bool thunder = weather == Weather.Rain;
            if (thunder && thunderCoroutine == null && thunderCue != null)
                thunderCoroutine = StartCoroutine(ThunderLoop());
            if (!thunder && thunderCoroutine != null)
            {
                StopCoroutine(thunderCoroutine);
                thunderCoroutine = null;
            }

            AudioClip wind = weather == Weather.Snow ? snowWindLoop : weather == Weather.Rain ? null : clearSkiesWindLoop;
            if (wind == null)
            {
                if (windSource != null) windSource.Stop();
                return;
            }
            if (windSource == null)
            {
                windSource = gameObject.AddComponent<AudioSource>();
                windSource.playOnAwake = false;
                windSource.loop = true;
                windSource.spatialBlend = 0f;
            }
            if (windSource.clip == wind && windSource.isPlaying) return;
            windSource.clip = wind;
            windSource.volume = WindVolumeScale * IAudioRequester.Instance.effectsVolume.GetValue();
            windSource.Play();
        }

        // WaitForSeconds follows timeScale, so a paused battle gets no thunder.
        private IEnumerator ThunderLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(ThunderIntervalMin, ThunderIntervalMax));
                IAudioRequester.Instance.Play(thunderCue);
            }
        }
        #endregion
        public void ToggleRain(bool _isRaining)
        {
            for(int i = 0; i < cachedRainObjects.Length; i++)
            {
                if(cachedRainObjects[i] != null)
                {
                    if(Application.isPlaying)
                    {
                        // Only destroy if the game is running, otherwise it will throw an error in editor mode
                        Destroy(cachedRainObjects[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(cachedRainObjects[i].gameObject);
                    }
                }
            }

            if (_isRaining)
            {
                clearSkyLight.enabled = false;
                rainLight.enabled = true;
                rainVolume.weight = 1;
                rainObject.SetActive(true);
                cachedRainObjects = new BattlefieldBonusGameObject[rainBattlefieldBonusObjects.Length];
                for(int i = 0; i < rainBattlefieldBonusObjects.Length; i++)
                {
                    cachedRainObjects[i] = Instantiate(rainBattlefieldBonusObjects[i], transform);
                }
            }
            else
            {
                clearSkyLight.enabled = true;
                rainLight.enabled = false;
                rainVolume.weight = 0;
                rainObject.SetActive(false);

                EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                //get all squad entities
                EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
                NativeArray<Entity> squadEntities = query.ToEntityArray(Allocator.TempJob);
                for (int i = 0; i < squadEntities.Length; i++)
                {
                    entityManager.AddComponentData(squadEntities[i], new RemoveBattlefieldBonusRain());
                }
                squadEntities.Dispose();
                query.Dispose();
            }
        }
        public void ToggleSnow(bool _isSnowing)
        {
            for(int i = 0; i < cachedSnowObjects.Length; i++)
            {
                if(cachedSnowObjects[i] != null)
                {
                    if(Application.isPlaying)
                    {
                        // Only destroy if the game is running, otherwise it will throw an error in editor mode
                        Destroy(cachedSnowObjects[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(cachedSnowObjects[i].gameObject);
                    }
                }
            }

            if (_isSnowing)
            {
                clearSkyLight.enabled = false;
                snowLight.enabled = true;
                snowVolume.weight = 1;
                snowObject.SetActive(true);
                cachedSnowObjects = new BattlefieldBonusGameObject[snowBattlefieldBonusObjects.Length];
                for(int i = 0; i < snowBattlefieldBonusObjects.Length; i++)
                {
                    cachedSnowObjects[i] = Instantiate(snowBattlefieldBonusObjects[i], transform);
                }
            }
            else
            {
                clearSkyLight.enabled = true;
                snowLight.enabled = false;
                snowVolume.weight = 0;
                snowObject.SetActive(false);

                EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                //get all squad entities
                EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
                NativeArray<Entity> squadEntities = query.ToEntityArray(Allocator.TempJob);
                for (int i = 0; i < squadEntities.Length; i++)
                {
                    entityManager.AddComponentData(squadEntities[i], new RemoveBattlefieldBonusSnow());
                }
                squadEntities.Dispose();
                query.Dispose();
            }
        }
        public void ToggleFog(bool _isFoggy)
        {
            for(int i = 0; i < cachedFogObjects.Length; i++)
            {
                if(cachedFogObjects[i] != null)
                {
                    if(Application.isPlaying)
                    {
                        // Only destroy if the game is running, otherwise it will throw an error in editor mode
                        Destroy(cachedFogObjects[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(cachedFogObjects[i].gameObject);
                    }
                }
            }

            if (_isFoggy)
            {
                clearSkyLight.enabled = false;
                fogLight.enabled = true;
                fogVolume.weight = 1;
                fogObject.SetActive(true);
                fogVoidObject.SetActive(true);
                cachedFogObjects = new BattlefieldBonusGameObject[fogBattlefieldBonusObjects.Length];
                for(int i = 0; i < fogBattlefieldBonusObjects.Length; i++)
                {
                    cachedFogObjects[i] = Instantiate(fogBattlefieldBonusObjects[i], transform);
                }
            }
            else
            {
                clearSkyLight.enabled = true;
                fogLight.enabled = false;
                fogVolume.weight = 0;
                fogObject.SetActive(false);
                fogVoidObject.SetActive(false);
                EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                //get all squad entities
                EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>());
                NativeArray<Entity> squadEntities = query.ToEntityArray(Allocator.TempJob);
                for (int i = 0; i < squadEntities.Length; i++)
                {
                    entityManager.AddComponentData(squadEntities[i], new RemoveBattlefieldBonusFog());
                }
                squadEntities.Dispose();
                query.Dispose();
            }
        }
        private void OnDestroy() 
        {
            if(IAudioRequester.HasInstance)
            {
                IAudioRequester.Instance.effectsVolume.OnValueChanged -= RainSoundLevelChange;
            }
        }
    }
}