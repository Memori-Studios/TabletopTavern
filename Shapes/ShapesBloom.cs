using UnityEngine;
using Shapes;

namespace TJ.Shapes
{

    [RequireComponent(typeof(ShapeRenderer))]
    public class ShapesBloom : MonoBehaviour
    {
        [SerializeField] private float bloomAmount;
        [SerializeField] private Color color;
        [SerializeField] private bool bloomOnEnable = true;
        public float BloomAmount => bloomAmount;

        private void OnEnable()
        {
            if (bloomOnEnable) Bloom();
        }
        public void SetColor(Color _color)
        {
            color = _color;
        }
        [ContextMenu("Bloom")]
        public void Bloom()
        {
            Bloom(color);
        }
        public void Bloom(Color _color = default)
        {
            if (_color != default)
            {
                color = _color;
            }
            ShapeRenderer shape = GetComponent<ShapeRenderer>();
            shape.Color = new Vector4(color.r, color.g, color.b, bloomAmount);
        }
    }
}