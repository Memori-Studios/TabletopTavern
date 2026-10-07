using UnityEngine;
using Memori.Scenes;

namespace TJ
{
    // Registers the Steam Workshop -> local Mods folder sync against SceneHandler's generic
    // pre-load hook, so it completes before the first scene transition. It does not run before
    // TabletopTavernData.Awake (Core.unity awakes first); the data manager reloads mods afterwards.
    // SceneHandler lives in the separate Memori.Scenes assembly and has no knowledge of this
    // project's modding system; this is the main-assembly side of that hook.
    public static class SteamWorkshopBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneHandler.OnBeforeFirstLoad += SteamWorkshopModSync.SyncSubscribedItemsToModsFolderAsync;
        }
    }
}
