using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// Sizes a GridLayoutGroup's cells so a fixed number of columns fills the width. The roster keeps two
    /// rows at every UI Scale instead of wrapping into a third when the canvas narrows.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class CollectionAutoGrid : MonoBehaviour
    {
        [SerializeField] private int columns = 8;
        [SerializeField] private float heightPerWidth = 1.05f;
        [SerializeField] private float maxCellWidth = 124f;

        private GridLayoutGroup _grid;
        private float _lastWidth = -1f;

        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();

        // The width can settle after the layout pass that enabled the roster, for example after a UI Scale change.
        private void LateUpdate()
        {
            if (!Mathf.Approximately(((RectTransform)transform).rect.width, _lastWidth)) Fit();
        }

        private void Fit()
        {
            if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0f || Mathf.Approximately(width, _lastWidth)) return;
            _lastWidth = width;
            float cell = (width - _grid.padding.horizontal - _grid.spacing.x * (columns - 1)) / columns;
            cell = Mathf.Floor(Mathf.Min(cell, maxCellWidth));
            _grid.cellSize = new Vector2(cell, Mathf.Floor(cell * heightPerWidth));
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = columns;
        }
    }
}
