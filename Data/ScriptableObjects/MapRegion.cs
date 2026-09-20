using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

namespace TJ
{
    [System.Serializable] public enum Biome { Plains, Forest, River, Swamp }//, Mountain, Village, Ruins }
    [System.Serializable] public enum Weather { ClearSkies, Rain, Fog, Snow }

    [CreateAssetMenu(fileName = "MapRegion", menuName = "GameData/MapRegion", order = 1)]
    public class MapRegion : ScriptableObject
    {
        public Race Race;
        public List<BiomeLikelihood> possibleBiomes;
        public List<WeatherLikelihood> possibleWeathers;

        [Header("Map Scene Settings")]
        public int resetTextureLayerIndex;
        public int nodePointLayerIndex;
        public int pathPointLayerIndex;
        public List<Material> treeMaterials;
        public AssetReferenceGameObject additionalGameObject;
        public bool spawnTrees;
        public int grassDetailLayer;

        [Header("Battlefield Assets")]
        public Material grassMaterial;
        public Texture2D GrassTexture2D;
        public GameObject TreeBase;
        public List<AssetReferenceGameObject> TreeObjectsAddressable;
        public List<AssetReferenceGameObject> ScatterObjectsAddressable;
        public Material RiverMaterial;
        public Material ForestMaterial;
        public List<AssetReferenceGameObject> ForestTreeObjectsAddressable;
        public List<AssetReferenceGameObject> RiverObjectsAddressable;

        [System.Serializable] public struct BiomeLikelihood
        {
            public Biome biome;
            public float likelihood;
        }
        [System.Serializable] public struct WeatherLikelihood
        {
            public Weather weather;
            public float likelihood;
        }
        public Biome GetRandomBiome(System.Random random)
        {
            List<BiomeLikelihood> biomeLikelihoods = new();
            float totalLikelihood = 0f;

            foreach (var biomeLikelihood in possibleBiomes)
            {
                totalLikelihood += biomeLikelihood.likelihood;
                biomeLikelihoods.Add(biomeLikelihood);
            }
            float randomValue = (float)(random.NextDouble() * totalLikelihood);
            float cumulativeLikelihood = 0f;
            foreach (var biomeLikelihood in biomeLikelihoods)
            {
                cumulativeLikelihood += biomeLikelihood.likelihood;
                if (randomValue <= cumulativeLikelihood)
                {
                    return biomeLikelihood.biome;
                }
            }
            return possibleBiomes[0].biome;
        }
        // A weather_overrides.json region table replaces the authored list without touching the asset.
        public List<WeatherLikelihood> GetPossibleWeathers()
        {
            return WeatherOverrideLoader.TryGetRegionWeathers(Race, out List<WeatherLikelihood> overridden) ? overridden : possibleWeathers;
        }
        public Weather GetRandomWeather(System.Random random)
        {
            List<WeatherLikelihood> table = GetPossibleWeathers();
            List<WeatherLikelihood> weatherLikelihoods = new();
            float totalLikelihood = 0f;

            foreach (var weatherLikelihood in table)
            {
                totalLikelihood += weatherLikelihood.likelihood;
                weatherLikelihoods.Add(weatherLikelihood);
            }
            float randomValue = (float)(random.NextDouble() * totalLikelihood);
            float cumulativeLikelihood = 0f;
            foreach (var weatherLikelihood in weatherLikelihoods)
            {
                cumulativeLikelihood += weatherLikelihood.likelihood;
                if (randomValue <= cumulativeLikelihood)
                {
                    return weatherLikelihood.weather;
                }
            }
            return table[0].weather;
        }
    }
}