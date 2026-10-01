using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TJ.MainMenu
{
    public sealed class SteamNewsPost
    {
        public string Id;
        public string Title;
        public DateTime PostedUtc;
        public string Url;
        public Texture2D Art;
    }

    // The newest Steam announcement, fetched once per launch; the cache owns the art so it outlives the menu scene.
    public static class SteamNewsFeed
    {
        private const string EventsUrl = "https://store.steampowered.com/events/ajaxgetadjacentpartnerevents/?appid={0}&count_before=0&count_after=1&lang_list=0";
        private const string ImageUrl = "https://clan.akamai.steamstatic.com/images/{0}/{1}";
        private const string PostUrl = "https://steamcommunity.com/games/{0}/announcements/detail/{1}";
        private const ulong ClanSteamIdBase = 103582791429521408UL;
        private const float TimeoutSeconds = 6f;

        private static Task<SteamNewsPost> _latest;

        // Null when Steam is unreachable, slow or returns nothing usable; the card then never shows.
        public static Task<SteamNewsPost> GetLatest(uint appId)
        {
            _latest ??= Fetch(string.Format(EventsUrl, appId), appId);
            return _latest;
        }

        #region Fetch
        private static async Task<SteamNewsPost> Fetch(string eventsUrl, uint appId)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            try
            {
                string json = await Download(eventsUrl, deadline, bytes: false) as string;
                if (json == null) return null;

                EventsResponse response = JsonUtility.FromJson<EventsResponse>(json);
                if (response == null || response.success != 1 || response.events == null || response.events.Length == 0)
                    return Fail("no events in the response");

                EventEntry entry = response.events[0];
                EventJson data = string.IsNullOrEmpty(entry.jsondata) ? null : JsonUtility.FromJson<EventJson>(entry.jsondata);
                string capsule = data != null && data.localized_capsule_image != null && data.localized_capsule_image.Length > 0
                    ? data.localized_capsule_image[0] : null;
                if (string.IsNullOrEmpty(capsule) || entry.announcement_body == null || !ulong.TryParse(entry.clan_steamid, out ulong clanSteamId))
                    return Fail("the newest event has no cover art or announcement");

                string imageUrl = string.Format(ImageUrl, clanSteamId - ClanSteamIdBase, capsule);
                if (await Download(imageUrl, deadline, bytes: true) is not byte[] imageBytes) return null;

                var art = new Texture2D(2, 2, TextureFormat.RGBA32, true)
                {
                    name = "Steam News Art",
                    hideFlags = HideFlags.DontUnloadUnusedAsset,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!art.LoadImage(imageBytes, true))
                {
                    UnityEngine.Object.Destroy(art);
                    return Fail("the cover art could not be decoded");
                }

                return new SteamNewsPost
                {
                    Id = entry.announcement_body.gid,
                    Title = entry.event_name,
                    PostedUtc = DateTimeOffset.FromUnixTimeSeconds(entry.announcement_body.posttime).UtcDateTime,
                    Url = string.Format(PostUrl, appId, entry.announcement_body.gid),
                    Art = art
                };
            }
            catch (Exception e)
            {
                return Fail(e.Message);
            }
        }

        // Returns the body as a string or bytes, or null after logging why.
        private static async Task<object> Download(string url, float deadline, bool bytes)
        {
            int seconds = Mathf.Max(1, Mathf.CeilToInt(deadline - Time.realtimeSinceStartup));
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = seconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"{request.error} ({url})");
                return null;
            }
            return bytes ? request.downloadHandler.data : request.downloadHandler.text;
        }

        private static SteamNewsPost Fail(string reason)
        {
            Debug.LogWarning($"[SteamNewsFeed] No news card: {reason}");
            return null;
        }
        #endregion

        #region Response shape
        [Serializable] private class EventsResponse { public int success; public EventEntry[] events; }
        [Serializable] private class EventEntry { public string event_name; public string clan_steamid; public string jsondata; public AnnouncementBody announcement_body; }
        [Serializable] private class AnnouncementBody { public string gid; public long posttime; }
        [Serializable] private class EventJson { public string[] localized_capsule_image; }
        #endregion
    }
}
