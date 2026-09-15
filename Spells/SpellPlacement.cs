using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// A formation the player drew with the PositionDrawer before a placement spell resolved:
    /// Starstep's destination or Raise Dead's spawn footprint. Captured by SpellManager on the
    /// right-click that confirms the placement and handed to ActiveSpell, which applies it when the
    /// spell lands (after the warm-up, so Raise Dead's 3s delay still shows before the squad appears).
    /// Positions are world points, one per unit index, straight off PositionDrawer.UnitPrefabPointPositions.
    /// </summary>
    public class SpellPlacement
    {
        public List<float3> Positions;
        public Quaternion Rotation;
        public int2 WidthAndDepth;
        public Vector3 Center;
    }
}
