using GPUECSAnimationBaker.Engine.AnimatorSystem;
using TabletopTavern.GpuAnim;
using Unity.Entities;

/// <summary>Gives an animator baked with the old package the components every game system now writes.</summary>
public static class GpuAnimLegacy
{
    /// <summary>Call on the prefab entity before instancing, so every instance carries them.</summary>
    public static void Attach(EntityManager em, Entity animator)
    {
        if (animator == Entity.Null || !em.Exists(animator) || em.HasComponent<GpuAnimControl>(animator)) return;
        if (!em.HasComponent<GpuEcsAnimatorControlComponent>(animator)) return;
        GpuEcsAnimatorControlComponent package = em.GetComponentData<GpuEcsAnimatorControlComponent>(animator);
        int slots = em.HasBuffer<GpuEcsAnimationDataBufferElement>(animator) ? em.GetBuffer<GpuEcsAnimationDataBufferElement>(animator).Length : 0;
        em.AddComponentData(animator, new GpuAnimControl
        {
            Slot = package.animatorInfo.animationID, TransitionSeconds = package.transitionSpeed,
            StartNormalizedTime = package.startNormalizedTime, Speed = 1f,
        });
        em.AddComponentData(animator, new GpuAnimRestart());
        em.AddComponentData(animator, new GpuAnimSlotCount { Value = slots });
        em.AddComponentData(animator, new GpuAnimLegacyState
        {
            LastSlot = package.animatorInfo.animationID, LastTransition = package.transitionSpeed, LastStart = package.startNormalizedTime,
        });
    }
}
