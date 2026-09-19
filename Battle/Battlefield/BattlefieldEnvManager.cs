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

        public Action<Weather> OnWeatherChanged;

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
            rainAudioSource.volume = IAudioRequester.Instance.effectsVolume.GetValue();

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
                rainAudioSource.volume = _volume;
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
            OnWeatherChanged?.Invoke(selectedWeather);
        }
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