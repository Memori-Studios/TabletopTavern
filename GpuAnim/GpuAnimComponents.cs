using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace TabletopTavern.GpuAnim
{
    #region Animator root
    /// <summary>One clip slot of a visual, in the positional order the unit visual contract fixes.</summary>
    public struct GpuAnimSlot : IBufferElementData
    {
        // First row of the clip in the matrix texture.
        public int Start;
        public int Count;
        public float Fps;
        public bool Loop;
        public bool ReturnToIdle;
        // Fraction of the clip at which a returning slot hands back to the idle slot.
        public float ReturnAt;
    }

    /// <summary>
    /// What the game asks a unit's visual to play, on every visual old or new. Writing the slot that already plays does
    /// nothing; set <see cref="GpuAnimRestart"/> to play it again from the start.
    /// </summary>
    public struct GpuAnimControl : IComponentData
    {
        public int Slot;
        // Persists until the game changes it, like the old package's transition speed.
        public float TransitionSeconds;
        public float StartNormalizedTime;
        public float Speed;
        // Where a returning slot goes; kept at the unit's current stance idle every frame.
        public int IdleSlot;

        public static GpuAnimControl Default => new GpuAnimControl { Slot = 0, Speed = 1f, TransitionSeconds = 0f, IdleSlot = 0 };
    }

    /// <summary>Set to play the current slot again from its start. Separate so a full-struct write of the control never clears it.</summary>
    public struct GpuAnimRestart : IComponentData
    {
        public bool Value;
    }

    /// <summary>On a visual baked with the old package: what the legacy bridge last wrote into it, so a value the game left alone is never rewritten.</summary>
    public struct GpuAnimLegacyState : IComponentData
    {
        public int LastSlot;
        public float LastTransition;
        public float LastStart;
    }

    /// <summary>How many slots the visual has, so a caller can skip a slot the bake lacks.</summary>
    public struct GpuAnimSlotCount : IComponentData
    {
        public int Value;
    }

    /// <summary>Playback position. Owned by the playback job; nothing else writes it.</summary>
    public struct GpuAnimState : IComponentData
    {
        public int CurrentSlot;
        // Absolute row in the matrix texture, fractional between rows.
        public float CurrentFrame;
        public int PrevSlot;
        public float PrevFrame;
        public float BlendLeft;
        public float BlendTotal;
        // The slot the game last asked for, before clamping to the slots this bake has.
        public int RequestedSlot;
        public bool Returned;
        // (rowA, rowB, lerp, enabled) and (rowA, rowB, lerp, weight of the previous clip), the shader's view of the state.
        public float4 CurrentVector;
        public float4 PrevVector;

        public static GpuAnimState Default => new GpuAnimState { CurrentSlot = -1, PrevSlot = -1, RequestedSlot = -1 };
    }

    /// <summary>Sizes of the matrix stream, for validation and the attachment job.</summary>
    public struct GpuAnimInfo : IComponentData
    {
        public int BoneCount;
        public int FrameCount;
    }

    public struct GpuAnimAnchorBlob
    {
        public int AnchorCount;
        public int FrameCount;
        // Row-major 3x4 model-space anchor transforms, frame-major: index = frame * AnchorCount + anchor.
        public BlobArray<float3x4> Matrices;
    }

    public struct GpuAnimAnchors : IComponentData
    {
        public BlobAssetReference<GpuAnimAnchorBlob> Blob;
    }
    #endregion

    #region Mesh and attachment children
    /// <summary>On every rendered mesh entity of a visual: which animator root drives it.</summary>
    public struct GpuAnimMeshLink : IComponentData
    {
        public Entity Animator;
    }

    [MaterialProperty("_GpuAnimCur")]
    public struct GpuAnimCurProperty : IComponentData
    {
        public float4 Value;
    }

    [MaterialProperty("_GpuAnimPrev")]
    public struct GpuAnimPrevProperty : IComponentData
    {
        public float4 Value;
    }

    /// <summary>On an attachment follower: which anchor of which animator it rides.</summary>
    public struct GpuAnimAttachment : IComponentData
    {
        public Entity Animator;
        public int AnchorIndex;
    }

    /// <summary>The prop role from the contract, kept on the entity so the game can add its marker components.</summary>
    public struct GpuAnimRole : IComponentData
    {
        public GpuAnimPropRole Value;
    }

    public enum GpuAnimPropRole : byte { Prop = 0, Bow = 1, Sword = 2, Shield = 3, Saddle = 4 }
    #endregion
}
