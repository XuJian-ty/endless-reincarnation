using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 伤害检测执行器：根据「伤害名」从配置库取一条数据，再按该条的检测方式与形状执行检测，返回命中的碰撞体与配置条目。
    /// 只做检测，不结算伤害；调用方根据返回的 entry.damageMultiplier 与 hitColliders 自行结算。
    /// 参数只需伤害名与攻击者 Transform，检测方式与所有几何参数均来自配置。
    /// </summary>
    public static class DamageDetectionRunner
    {
        private const int RaycastHitBufferSize = 32;
        private static readonly RaycastHit[] RaycastHitBuffer = new RaycastHit[RaycastHitBufferSize];
        private static readonly List<Collider> RaycastColliderList = new List<Collider>(RaycastHitBufferSize);

        /// <summary>
        /// 执行一次伤害检测。根据配置中的 detectionType 与 shape 等决定具体检测逻辑与使用的数据。
        /// </summary>
        /// <param name="damageName">伤害名，与动画帧伤害配置库中的 damageName 一致</param>
        /// <param name="attacker">攻击者 Transform，用于原点、朝向、本地偏移变换</param>
        /// <param name="database">动画帧伤害数据配置库，若为 null 则返回 false</param>
        /// <param name="outBuffer">用于写入命中碰撞体的缓冲区，调用方复用以避免 GC</param>
        /// <param name="entry">命中时对应的配置条目（含倍率、击退等），未命中或无配置时仍可能非 null</param>
        /// <param name="hitCount">本次命中的碰撞体数量，写入 outBuffer[0..hitCount-1]</param>
        /// <returns>是否成功查表并完成检测；false 表示无配置或层级无效，此时 entry 可能为 null、hitCount 为 0</returns>
        public static bool TryRunDetection(
            string damageName,
            Transform attacker,
            AnimationFrameDamageDatabaseSO database,
            Collider[] outBuffer,
            out AnimationFrameDamageEntry entry,
            out int hitCount)
        {
            entry = null;
            hitCount = 0;
            if (attacker == null || outBuffer == null || outBuffer.Length == 0) return false;
            if (database == null || string.IsNullOrEmpty(damageName))
                return false;

            entry = database.GetEntry(damageName);
            if (entry == null) return false;

            int layerMask = LayerMask.GetMask(entry.hitLayerName);
            if (layerMask == 0)
                return true; // 配置有效但层级未建，返回 entry 供 fallback，命中数为 0

            switch (entry.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    hitCount = RunRangeOverlap(attacker, entry, layerMask, outBuffer);
                    return true;
                case DamageDetectionType.Collision:
                    // 碰撞检测由「在指定帧区间内启用子物体 Collider + OnTriggerEnter」在别处处理，此处仅返回配置
                    return true;
                case DamageDetectionType.Raycast:
                    hitCount = RunRaycast(attacker, entry, layerMask, outBuffer);
                    return true;
                default:
                    return true;
            }
        }

        private static int RunRangeOverlap(Transform attacker, AnimationFrameDamageEntry entry, int layerMask, Collider[] outBuffer)
        {
            Vector3 origin = attacker.TransformPoint(entry.CenterOffset);
            int count;

            switch (entry.shape)
            {
                case AttackShapeType.Sphere:
                    count = Physics.OverlapSphereNonAlloc(origin, entry.sphereRadius, outBuffer, layerMask);
                    return count;

                case AttackShapeType.Sector:
                    float radius = Mathf.Max(entry.sphereRadius, 0.01f);
                    count = Physics.OverlapSphereNonAlloc(origin, radius, outBuffer, layerMask);
                    if (count <= 0) return 0;
                    Vector3 forward = attacker.forward;
                    float halfAngle = entry.sectorAngle * 0.5f;
                    int write = 0;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 toCollider = outBuffer[i].transform.position - origin;
                        if (toCollider.sqrMagnitude < 0.0001f ||
                            Vector3.Angle(forward, toCollider) <= halfAngle)
                        {
                            if (write != i) outBuffer[write] = outBuffer[i];
                            write++;
                        }
                    }
                    return write;

                case AttackShapeType.Box:
                    Vector3 halfExtents = entry.BoxSize * 0.5f;
                    count = Physics.OverlapBoxNonAlloc(origin, halfExtents, outBuffer, attacker.rotation, layerMask);
                    return count;

                default:
                    return 0;
            }
        }

        private static int RunRaycast(Transform attacker, AnimationFrameDamageEntry entry, int layerMask, Collider[] outBuffer)
        {
            Vector3 origin = attacker.TransformPoint(entry.RayOriginOffset);
            Vector3 direction = attacker.forward;
            float distance = Mathf.Max(0f, entry.rayMaxDistance);

            int numHits = Physics.RaycastNonAlloc(origin, direction, RaycastHitBuffer, distance, layerMask);
            if (numHits <= 0) return 0;

            RaycastColliderList.Clear();
            for (int i = 0; i < numHits; i++)
            {
                Collider c = RaycastHitBuffer[i].collider;
                if (c != null && !RaycastColliderList.Contains(c))
                    RaycastColliderList.Add(c);
            }
            int n = Mathf.Min(RaycastColliderList.Count, outBuffer.Length);
            for (int i = 0; i < n; i++)
                outBuffer[i] = RaycastColliderList[i];
            return n;
        }
    }
}
