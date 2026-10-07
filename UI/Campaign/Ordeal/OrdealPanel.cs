using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.UI;
using Memori.Utilities;
using TJ.Map;
using UnityEngine;

namespace TJ.Ordeals
{
    /// <summary>The pick-one-of-three screen a beaten warlord brings on the March. There is no reroll and no skip.</summary>
    public class OrdealPanel : MapPanel
    {
        [SerializeField] private ChoicePanelView view;
        [SerializeField] private float pickedHoldSeconds = 0.6f;
        [SerializeField] private float fadeOutSeconds = 0.25f;
        // Band colours in OrdealGroup order: enemy armies, your warband, road and coin, magic, double-edged.
        [SerializeField] private Color[] groupColours =
        {
            new(0.941f, 0.541f, 0.502f, 1f),
            new(0.435f, 0.714f, 0.878f, 1f),
            new(0.890f, 0.733f, 0.443f, 1f),
            new(0.690f, 0.549f, 0.902f, 1f),
            new(0.373f, 0.780f, 0.690f, 1f),
        };

        MemoriCanvasGroup memoriCanvasGroup;
        Action<OrdealId> onTaken;
        bool picked;

        private void Awake()
        {
            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            memoriCanvasGroup.CGDisable();
        }
        public void Open(List<OrdealId> offer, int nextBattle, Action<OrdealId> _onTaken)
        {
            onTaken = _onTaken;
            picked = false;
            CampaignSaveData run = CampaignManager.Instance.CampaignSaveManager.SaveData;

            view.Clear();
            view.SetTitle(Text("OrdealPickTitle"), string.Format(Text("OrdealPickSubtitle"), nextBattle));
            int held = run.ordeals != null ? run.ordeals.Count : 0;
            view.SetInfo(string.Format(Text("ordealRenownLine"), Multiplier(held), Multiplier(held + 1)), run);

            string footer = Text("choiceClickToTake");
            string done = Text("choiceTaken");
            foreach (OrdealId id in offer)
            {
                OrdealDefinition ordeal = OrdealRegistry.Get(id);
                ChoiceCardView card = view.AddCard();
                card.Load(SpriteData.GetSprite(ordeal.IconName), Text(ordeal.NameKey), Text(ordeal.GroupKey), GroupColour(ordeal.Group),
                    Text(ordeal.DescriptionKey), footer, done);
                card.SetNotes(RunNotes(ordeal, run));
                OrdealId taken = id;
                card.Chosen += chosen => Take(taken, chosen);
            }
            view.WireNavigation();

            memoriCanvasGroup.CGEnable();
            if (OpenFeedback != null) OpenFeedback.PlayFeedbacks();
            view.PlayOpen(intro: true);
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
            view.FocusFirstCard();
        }
        private async void Take(OrdealId taken, ChoiceCardView chosen)
        {
            if (picked) return;
            picked = true;

            bool firstPop = true;
            foreach (ChoiceCardView card in view.Cards)
            {
                if (card == chosen) continue;
                card.PopAway(firstPop);
                firstPop = false;
            }
            chosen.ShowPicked();
            IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);

            await Task.Delay(Mathf.RoundToInt(pickedHoldSeconds * 1000f));
            if (this == null) return;
            memoriCanvasGroup.FadeOutAsync(fadeOutSeconds);
            await Task.Delay(Mathf.RoundToInt(fadeOutSeconds * 1000f));
            if (this == null) return;

            Action<OrdealId> resolved = onTaken;
            ClosePanel();
            resolved?.Invoke(taken);
        }
        public override void ClosePanel()
        {
            memoriCanvasGroup.CGDisable();
            if (OpenFeedback != null) CloseFeedback();
            view.Clear();
            onTaken = null;
        }

        #region Card text
        // What this card would change for this run that its own text cannot know: owned items it switches off and stacked gold loss.
        private static List<(ChoiceCardView.NoteKind, string)> RunNotes(OrdealDefinition ordeal, CampaignSaveData run)
        {
            var notes = new List<(ChoiceCardView.NoteKind, string)>();

            var switchedOff = new List<string>();
            foreach (GearID gear in ordeal.CounteredGear)
                if (run.Gear != null && run.Gear.Contains(gear) && !run.IsGearBroken(gear))
                    switchedOff.Add(Text(gear + "Name"));
            foreach (ConsumableEnum consumable in ordeal.CounteredConsumables)
                if (run.consumables != null && run.consumables.Contains(consumable))
                    switchedOff.Add(Text(consumable + "Name"));
            if (switchedOff.Count > 0)
                notes.Add((ChoiceCardView.NoteKind.Warning, string.Format(Text("ordealCardStopsWorking"), string.Join(", ", switchedOff))));

            if (ordeal.CounteredFactionPassive is Race race && HeroData.GetRaceFromHero(run.heroID) == race)
                notes.Add((ChoiceCardView.NoteKind.Warning, string.Format(Text("ordealCardFactionOff"), Text(race.ToString()))));

            int own = OrdealRegistry.GoldLostPerTurn(new[] { ordeal.Id });
            int total = OrdealRegistry.GoldLostPerTurn((run.ordeals ?? new List<OrdealId>()).Append(ordeal.Id));
            if (own > 0 && total > own)
                notes.Add((ChoiceCardView.NoteKind.Gold, string.Format(Text("ordealCardGoldStack"), total)));

            if (ordeal.RedrawsMap)
                notes.Add((ChoiceCardView.NoteKind.Map, Text("ordealCardRedrawsMap")));
            return notes;
        }

        private Color GroupColour(OrdealGroup group)
        {
            int index = (int)group;
            return index < groupColours.Length ? groupColours[index] : Color.white;
        }

        private static string Multiplier(int ordealCount) => OrdealRegistry.RenownMultiplier(ordealCount).ToString("0.0#");

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);
        #endregion
    }
}
