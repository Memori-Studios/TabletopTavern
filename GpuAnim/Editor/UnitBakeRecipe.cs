using System;
using System.Collections.Generic;
using MemoriStudios.TJBake;
using UnityEngine;

namespace TabletopTavern.GpuAnim.Editor
{
    /// <summary>
    /// Everything needed to re-bake one of the game's units with TJBake: the rigs, the slot table, the anchors, each
    /// variant's props and the rider. The bake writes StreamingAssets/UnitVisuals/&lt;unitName&gt;/.
    /// </summary>
    [CreateAssetMenu(menuName = "TJBake/Unit Bake Recipe", fileName = "New Unit Bake Recipe")]
    public sealed class UnitBakeRecipe : ScriptableObject
    {
        [Serializable]
        public sealed class Parameter
        {
            public string name = "";
            public AnimatorControllerParameterType type = AnimatorControllerParameterType.Float;
            public float floatValue;
            public int intValue;
            public bool boolValue;
        }

        [Serializable]
        public sealed class Slot
        {
            public string name = "";
            [Tooltip("Sets the frame count and the return point; it also plays when no state is named.")]
            public AnimationClip clip;
            [Tooltip("Base-layer state to pose through the Animator, so extra layers and blend trees bake as they play.")]
            public string stateName = "";
            public List<Parameter> parameters = new();
        }

        [Serializable]
        public sealed class Anchor
        {
            public string name = "";
            [Tooltip("Path of the bone under the rig root, as Transform.Find takes it.")]
            public string bonePath = "";
        }

        [Serializable]
        public sealed class Prop
        {
            public string name = "";
            [Tooltip("Baked in this prefab's root space, which sits on the anchor.")]
            public GameObject prefab;
            public string anchor = "";
            public TJBakePropRole role = TJBakePropRole.Prop;
        }

        [Serializable]
        public sealed class AttachedRig
        {
            [Tooltip("An animated rig (wings) mounted on an anchor; its bones join the skeleton and its default state plays under every slot.")]
            public GameObject rig;
            public string anchor = "";
        }

        [Serializable]
        public sealed class Variant
        {
            [Tooltip("A rig whose bones carry the same names as the first variant's.")]
            public GameObject rig;
            public List<Prop> props = new();
            public List<AttachedRig> attachedRigs = new();
        }

        [Serializable]
        public sealed class Rig
        {
            public GameObject rig;
            public float rootScale = 1f;
            [Tooltip("Where the rider's root sits in saddle space, before its scale.")]
            public Vector3 rootOffset = Vector3.zero;
            public List<Slot> slots = new();
            public List<Anchor> anchors = new();
            public List<Prop> props = new();
        }

        public string unitName = "";
        [Tooltip("For a rig with no skinned mesh, such as the artillery clock that only times its slots.")]
        public bool placeholderWhenNoMesh;
        [Tooltip("Baked into the animation, so every variant shares it.")]
        public float rootScale = 1f;
        public List<Slot> slots = new();
        public List<Anchor> anchors = new();
        [Tooltip("1 to 3. Squads mix them.")]
        public List<Variant> variants = new();
        [Tooltip("Mounted units only.")]
        public bool hasRider;
        public Rig rider = new();
    }
}
