using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TabletopTavern.GpuAnim
{
    #region Playback
    /// <summary>Advances every animator root: slot requests, looping, holding, the return to idle and the blend from the previous slot.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct GpuAnimPlaybackSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PlaybackJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct PlaybackJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref GpuAnimControl control, ref GpuAnimRestart restart, ref GpuAnimState anim, in DynamicBuffer<GpuAnimSlot> slots)
            {
                if (slots.Length == 0) return;
                float dt = DeltaTime * math.max(control.Speed, 0f);

                if (control.Slot != anim.RequestedSlot || restart.Value)
                {
                    anim.RequestedSlot = control.Slot;
                    restart.Value = false;
                    if (anim.CurrentSlot >= 0 && control.TransitionSeconds > 0f)
                    {
                        anim.PrevSlot = anim.CurrentSlot;
                        anim.PrevFrame = anim.CurrentFrame;
                        anim.BlendTotal = control.TransitionSeconds;
                        anim.BlendLeft = control.TransitionSeconds;
                    }
                    else
                    {
                        anim.PrevSlot = -1;
                        anim.BlendLeft = 0f;
                        anim.BlendTotal = 0f;
                    }
                    anim.CurrentSlot = math.clamp(control.Slot, 0, slots.Length - 1);
                    GpuAnimSlot started = slots[anim.CurrentSlot];
                    // The run cycle's random phase stays in the control; a one-shot always plays from its first frame.
                    float startNormalized = started.Loop ? math.saturate(control.StartNormalizedTime) : 0f;
                    anim.CurrentFrame = started.Start + startNormalized * math.max(started.Count - 1, 0);
                    anim.Returned = false;
                }

                GpuAnimSlot slot = slots[anim.CurrentSlot];
                float local = Advance(anim.CurrentFrame - slot.Start, slot, dt);
                anim.CurrentFrame = slot.Start + local;
                anim.CurrentVector = Vector(slot, local, 1f);

                if (!slot.Loop && slot.ReturnToIdle && !anim.Returned && slot.Count > 1 && local / (slot.Count - 1) >= slot.ReturnAt)
                {
                    // The old pipeline's return event wrote idle with a 0.5 s blend; the game reads the slot back from here.
                    anim.Returned = true;
                    control.Slot = math.clamp(control.IdleSlot, 0, slots.Length - 1);
                    control.TransitionSeconds = 0.5f;
                    anim.RequestedSlot = control.Slot;
                    anim.PrevSlot = anim.CurrentSlot;
                    anim.PrevFrame = anim.CurrentFrame;
                    anim.BlendTotal = 0.5f;
                    anim.BlendLeft = 0.5f;
                    anim.CurrentSlot = control.Slot;
                    GpuAnimSlot idle = slots[anim.CurrentSlot];
                    anim.CurrentFrame = idle.Start;
                    slot = idle;
                    local = 0f;
                    anim.CurrentVector = Vector(slot, local, 1f);
                }

                if (anim.PrevSlot >= 0 && anim.BlendLeft > 0f)
                {
                    GpuAnimSlot prev = slots[math.clamp(anim.PrevSlot, 0, slots.Length - 1)];
                    float prevLocal = Advance(anim.PrevFrame - prev.Start, prev, dt);
                    anim.PrevFrame = prev.Start + prevLocal;
                    anim.BlendLeft = math.max(anim.BlendLeft - DeltaTime, 0f);
                    float weight = anim.BlendTotal > 0f ? anim.BlendLeft / anim.BlendTotal : 0f;
                    anim.PrevVector = Vector(prev, prevLocal, weight);
                    if (anim.BlendLeft <= 0f) anim.PrevSlot = -1;
                }
                else
                {
                    anim.PrevVector = float4.zero;
                }
            }

            // Local frame position after dt, wrapped for a loop, clamped for a one-shot.
            private static float Advance(float local, in GpuAnimSlot slot, float dt)
            {
                if (slot.Count <= 1) return 0f;
                local += dt * slot.Fps;
                if (slot.Loop)
                {
                    local = math.fmod(local, slot.Count);
                    if (local < 0f) local += slot.Count;
                    return local;
                }
                return math.clamp(local, 0f, slot.Count - 1);
            }

            // Two texture rows and the lerp between them; a loop wraps its last row to its first.
            private static float4 Vector(in GpuAnimSlot slot, float local, float w)
            {
                int a = (int)math.floor(local);
                int b = slot.Loop ? (a + 1) % math.max(slot.Count, 1) : math.min(a + 1, slot.Count - 1);
                return new float4(slot.Start + a, slot.Start + b, local - a, w);
            }
        }
    }
    #endregion

    #region Copy to meshes
    /// <summary>Copies each animator's state vectors onto its mesh entities' per-instance material properties.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(GpuAnimPlaybackSystem))]
    [BurstCompile]
    public partial struct GpuAnimPropertySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CopyJob { States = SystemAPI.GetComponentLookup<GpuAnimState>(true) }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct CopyJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<GpuAnimState> States;

            private void Execute(in GpuAnimMeshLink link, ref GpuAnimCurProperty cur, ref GpuAnimPrevProperty prev)
            {
                if (!States.TryGetComponent(link.Animator, out GpuAnimState anim)) return;
                cur.Value = anim.CurrentVector;
                prev.Value = anim.PrevVector;
            }
        }
    }
    #endregion

    #region Attachments
    /// <summary>Moves each attachment follower to its anchor's blended model-space transform for this frame.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(GpuAnimPlaybackSystem))]
    [BurstCompile]
    public partial struct GpuAnimAttachmentSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FollowJob
            {
                States = SystemAPI.GetComponentLookup<GpuAnimState>(true),
                Anchors = SystemAPI.GetComponentLookup<GpuAnimAnchors>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct FollowJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<GpuAnimState> States;
            [ReadOnly] public ComponentLookup<GpuAnimAnchors> Anchors;

            private void Execute(in GpuAnimAttachment attachment, ref LocalTransform transform)
            {
                if (!States.TryGetComponent(attachment.Animator, out GpuAnimState anim)) return;
                if (!Anchors.TryGetComponent(attachment.Animator, out GpuAnimAnchors anchors) || !anchors.Blob.IsCreated) return;
                ref GpuAnimAnchorBlob blob = ref anchors.Blob.Value;
                if (attachment.AnchorIndex < 0 || attachment.AnchorIndex >= blob.AnchorCount || blob.FrameCount == 0) return;

                float3x4 m = Sample(ref blob, attachment.AnchorIndex, anim.CurrentVector);
                if (anim.PrevVector.w > 0f)
                {
                    float3x4 p = Sample(ref blob, attachment.AnchorIndex, anim.PrevVector);
                    m = Lerp(m, p, anim.PrevVector.w);
                }
                transform.Position = m.c3;
                transform.Rotation = quaternion.LookRotationSafe(m.c2, m.c1);
                // The anchor matrix carries the bake's root scale; props were baked at scale 1.
                transform.Scale = math.length(m.c0);
            }

            private static float3x4 Sample(ref GpuAnimAnchorBlob blob, int anchor, float4 v)
            {
                int rowA = math.clamp((int)v.x, 0, blob.FrameCount - 1);
                int rowB = math.clamp((int)v.y, 0, blob.FrameCount - 1);
                return Lerp(blob.Matrices[rowA * blob.AnchorCount + anchor], blob.Matrices[rowB * blob.AnchorCount + anchor], v.z);
            }

            private static float3x4 Lerp(float3x4 a, float3x4 b, float t)
            {
                return new float3x4(math.lerp(a.c0, b.c0, t), math.lerp(a.c1, b.c1, t), math.lerp(a.c2, b.c2, t), math.lerp(a.c3, b.c3, t));
            }
        }
    }
    #endregion
}
