using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 伤害检测执行器：根据技能库中内嵌的伤害效果数据执行一次命中检测。
    /// 只做检测，不结算伤害；调用方根据返回的命中碰撞体与伤害倍率自行结算。
    /// </summary>
    public static class DamageDetectionRunner
    {
        private const int RaycastHitBufferSize = 32;
        private static readonly RaycastHit[] RaycastHitBuffer = new RaycastHit[RaycastHitBufferSize];
        private static readonly List<Collider> RaycastColliderList = new List<Collider>(RaycastHitBufferSize);

        public static bool TryRunDetection(
            SkillDamageEffect effect,
            Transform attacker,
            SkillCollisionHitbox collisionHitbox,
            object collisionToken,
            Collider[] outBuffer,
            out int hitCount)
        {
            hitCount = 0;
            if (attacker == null || outBuffer == null || outBuffer.Length == 0 || effect == null)
                return false;

            int layerMask = LayerMask.GetMask(effect.hitLayerName);
            if (layerMask == 0)
                return true;

            switch (effect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    hitCount = RunRangeOverlap(attacker, effect, layerMask, outBuffer);
                    return true;
                case DamageDetectionType.Collision:
                    hitCount = RunCollisionHits(collisionHitbox, collisionToken, outBuffer);
                    return true;
                case DamageDetectionType.Raycast:
                    hitCount = RunRaycast(attacker, effect, layerMask, outBuffer);
                    return true;
                default:
                    return true;
            }
        }

        private static int RunRangeOverlap(Transform attacker, SkillDamageEffect effect, int layerMask, Collider[] outBuffer)
        {
            Vector3 origin = attacker.TransformPoint(effect.centerOffset);
            int count;

            switch (effect.shape)
            {
                case AttackShapeType.Sphere:
                    count = Physics.OverlapSphereNonAlloc(origin, effect.sphereRadius, outBuffer, layerMask);
                    return count;

                case AttackShapeType.Sector:
                    float radius = Mathf.Max(effect.sphereRadius, 0.01f);
                    count = Physics.OverlapSphereNonAlloc(origin, radius, outBuffer, layerMask);
                    if (count <= 0) return 0;
                    Vector3 forward = attacker.forward;
                    float halfAngle = effect.sectorAngle * 0.5f;
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
                    Vector3 halfExtents = effect.boxSize * 0.5f;
                    count = Physics.OverlapBoxNonAlloc(origin, halfExtents, outBuffer, attacker.rotation, layerMask);
                    return count;

                default:
                    return 0;
            }
        }

        private static int RunRaycast(Transform attacker, SkillDamageEffect effect, int layerMask, Collider[] outBuffer)
        {
            Vector3 origin = attacker.TransformPoint(effect.rayOriginOffset);
            Vector3 direction = attacker.forward;
            float distance = Mathf.Max(0f, effect.rayMaxDistance);

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

        public static SkillCollisionHitbox BeginCollisionWindow(Transform attacker, SkillDamageEffect effect, object collisionToken)
        {
            if (attacker == null || effect == null || collisionToken == null || string.IsNullOrWhiteSpace(effect.colliderNodeName))
                return null;

            Transform node = FindChildRecursive(attacker, effect.colliderNodeName);
            if (node == null)
                return null;

            Collider sourceCollider = node.GetComponent<Collider>();
            if (sourceCollider == null)
                sourceCollider = node.GetComponentInChildren<Collider>(true);
            if (sourceCollider == null)
                return null;

            int layerMask = LayerMask.GetMask(effect.hitLayerName);
            if (layerMask == 0)
                return null;

            SkillCollisionHitbox hitbox = node.GetComponent<SkillCollisionHitbox>();
            if (hitbox == null)
                hitbox = node.gameObject.AddComponent<SkillCollisionHitbox>();

            hitbox.OpenWindow(collisionToken, attacker.root, layerMask);
            return hitbox;
        }

        public static void EndCollisionWindow(SkillCollisionHitbox collisionHitbox, object collisionToken)
        {
            collisionHitbox?.CloseWindow(collisionToken);
        }

        private static int RunCollisionHits(SkillCollisionHitbox collisionHitbox, object collisionToken, Collider[] outBuffer)
        {
            if (collisionHitbox == null)
                return 0;

            return collisionHitbox.ConsumeHits(collisionToken, outBuffer);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindChildRecursive(child, targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
