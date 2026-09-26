using Memori.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TJ
{
    // A seasonal scene cannot reference Tavern objects, so it hides them by hierarchy path, e.g. "Tavern/Fireplace/Deer".
    public class HideTavernObjects : MonoBehaviour
    {
        [SerializeField] private string[] tavernObjectPaths;

        private void Awake()
        {
            Scene tavern = SceneManager.GetSceneByBuildIndex((int)SceneIndexes.Tavern);
            if (!tavern.isLoaded)
            {
                Debug.LogError("[HideTavernObjects] The Tavern scene is not loaded, nothing hidden.");
                return;
            }
            foreach (string path in tavernObjectPaths)
            {
                GameObject target = FindInScene(tavern, path);
                if (target == null)
                {
                    Debug.LogError($"[HideTavernObjects] '{path}' was not found in the Tavern scene.");
                    continue;
                }
                target.SetActive(false);
            }
        }

        private static GameObject FindInScene(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root;
                Transform child = root.transform.Find(path.Substring(slash + 1));
                if (child != null) return child.gameObject;
            }
            return null;
        }
    }
}
