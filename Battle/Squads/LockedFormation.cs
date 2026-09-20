using System.Collections.Generic;
using Unity.Mathematics;

namespace TJ.Battle
{
    /// <summary>
    /// The frame maths behind a locked group: members are stored as offsets from the block's centroid
    /// along its mean facing, so the whole block can be moved and rotated as one rigid piece. Pure
    /// so it can be tested without a BattleManager.
    /// </summary>
    public static class LockedFormation
    {
        public struct Pose
        {
            public int SquadId;
            public float3 Center;
            public quaternion Rotation;
        }

        /// <summary>
        /// Centroid and flat mean facing of the poses. Members facing exactly opposite ways cancel to
        /// zero, which would be a NaN rotation, so that case falls back to +Z.
        /// </summary>
        public static bool TryGetBlockPose(List<Pose> poses, out float3 anchor, out quaternion facing)
        {
            anchor = float3.zero;
            facing = quaternion.identity;
            if (poses.Count == 0) return false;

            float3 forward = float3.zero;
            foreach (Pose pose in poses)
            {
                anchor += pose.Center;
                float3 f = math.mul(pose.Rotation, math.forward());
                forward += new float3(f.x, 0f, f.z);
            }
            anchor /= poses.Count;
            if (math.lengthsq(forward) < 1e-4f) forward = math.forward();
            facing = quaternion.LookRotationSafe(math.normalize(forward), math.up());
            return true;
        }

        /// <summary>Replaces <paramref name="slots"/> with every pose expressed in the block frame.</summary>
        public static void Snapshot(List<Pose> poses, Dictionary<int, LockedSlot> slots)
        {
            slots.Clear();
            if (!TryGetBlockPose(poses, out float3 anchor, out quaternion facing)) return;

            quaternion inverseFacing = math.inverse(facing);
            foreach (Pose pose in poses)
            {
                slots[pose.SquadId] = new LockedSlot
                {
                    LocalOffset = math.mul(inverseFacing, pose.Center - anchor),
                    LocalRotation = math.mul(inverseFacing, pose.Rotation),
                };
            }
        }

        /// <summary>A slot placed back into the world for a block standing at <paramref name="anchor"/> facing <paramref name="facing"/>.</summary>
        public static void Project(in LockedSlot slot, float3 anchor, quaternion facing, out float3 center, out quaternion rotation)
        {
            center = anchor + math.mul(facing, slot.LocalOffset);
            rotation = math.mul(facing, slot.LocalRotation);
        }
    }
}
