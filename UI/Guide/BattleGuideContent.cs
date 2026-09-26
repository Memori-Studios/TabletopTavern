using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace TJ
{
    public enum GuideBlockType { Keys, Terms, TermsInline, ListUp, ListDown, Formula }

    [System.Serializable]
    public class GuideRow
    {
        public string labelKey;
        public string textKey;
        /// <summary>Space-separated tokens: @Action (live binding), LMB / RMB / MMB / WHEEL, "+", "/", "-", ~key (localized word), anything else is a literal keycap.</summary>
        public string keys;
    }

    [System.Serializable]
    public class GuideBlock
    {
        public GuideBlockType type;
        public string titleKey;
        // A full-width block takes a row of its own instead of sharing it with the next block.
        public bool fullWidth;
        public string formulaKey;
        public string textKey;
        public List<GuideRow> rows = new();
    }

    [System.Serializable]
    public class GuideTopic
    {
        // Saved in PlayerSaveData.BattlefieldInfoSectionsViewed, so an id must never change once shipped.
        public string id;
        public string chapterKey;
        public string titleKey;
        public string summaryKey;
        public string captionKey;
        public VideoClip video;
        public BattlefieldTutorial.BattlefieldInfoCondition condition;
        // Lower pops first as a pre-battle tip; ties keep list order.
        public int tipPriority = 2;
        public bool showAsTip = true;
        public List<string> related = new();
        public List<GuideBlock> blocks = new();
    }

    [CreateAssetMenu(menuName = "Tabletop Tavern/Battle Guide Content")]
    public class BattleGuideContent : ScriptableObject
    {
        public List<GuideTopic> topics = new();

        public GuideTopic Find(string id)
        {
            foreach (GuideTopic topic in topics)
                if (topic.id == id) return topic;
            return null;
        }
    }
}
