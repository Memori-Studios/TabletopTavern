using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
    /// <summary>
    /// One faction band in the <see cref="SpellBrowseMenu"/>: a colour dot, the faction name, and the
    /// parent every row of that faction is instantiated under.
    ///
    /// Grouping is what makes the faction colouring worth having. Ungrouped, twenty identically-treated
    /// tiles carry no information until you read every cell, and the colour has nothing to reinforce.
    ///
    /// This component owns no layout. How many rows sit per band, and how many bands sit per line, are
    /// authored on the LayoutGroups of this prefab and of the menu's content parent.
    /// </summary>
    public class SpellBrowseGroup : MonoBehaviour
    {
        [SerializeField] private Image colourDot;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Transform tilesParent;

        /// <summary>Where <see cref="SpellBrowseMenu"/> parents this faction's rows.</summary>
        public Transform TilesParent => tilesParent;

        public void SetUp(Race race)
        {
            colourDot.color = ColorData.GetRaceDisplayColor(race);
            labelText.text = SpellRaceLabel.Get(race);
            SizeLabelToText();
        }

        /// <summary>
        /// Pins the label's preferred width to the width its text actually needs.
        ///
        /// Leaving this to the layout group does not work here. A HorizontalLayoutGroup asks TMP for
        /// preferredWidth, but TMP does not generate its text mesh at assignment - it defers to its own
        /// render callback - so it answers zero for any pass that happens first. That is guaranteed
        /// here, because SpellBrowseMenu builds every band while its root is still inactive. The band
        /// then solves against a near-zero label, nothing marks it dirty again, and the rule ends up
        /// drawn through the faction name instead of after it.
        ///
        /// GetPreferredValues measures synchronously and works on an inactive object, which is the
        /// whole point. The LayoutElement is created on demand so the prefab needs no extra wiring.
        ///
        /// Min is set alongside preferred deliberately. Preferred alone is only a request: once the
        /// row's total preferred width exceeds the band, the layout group shrinks every child back
        /// toward its minimum, and a faction name is the one thing here that must never be squeezed.
        /// The rule beside it should be carrying that slack instead - it wants preferred 0 and
        /// flexible 1, so it asks for nothing and takes the remainder.
        /// </summary>
        private void SizeLabelToText()
        {
            LayoutElement labelLayout = labelText.GetComponent<LayoutElement>();
            if(labelLayout == null) labelLayout = labelText.gameObject.AddComponent<LayoutElement>();

            float textWidth = labelText.GetPreferredValues(labelText.text).x;
            labelLayout.preferredWidth = textWidth;
            labelLayout.minWidth = textWidth;
        }
    }
}
