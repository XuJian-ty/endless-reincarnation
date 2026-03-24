using System.Collections.Generic;
using System;
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
            SkillDetectionMotionFrame? motionFrame,
            out int hitCount)
        {
            hitCount = 0;
            if (attacker == null || outBuffer == null || outBuffer.Length == 0 || effect == null)
                return false;

            int layerMask = ResolveLayerMask(effect.hitLayerName);
            if (layerMask == 0)
                return true;

            switch (effect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    hitCount = RunRangeOverlap(attacker, effect, layerMask, outBuffer, motionFrame);
                    return true;
                case DamageDetectionType.Collision:
                    hitCount = RunCollisionHits(collisionHitbox, collisionToken, outBuffer);
                    return true;
                case DamageDetectionType.Raycast:
                    hitCount = RunRaycast(attacker, effect, layerMask, outBuffer, motionFrame);
                    return true;
                default:
                    return true;
            }
        }

        private static int RunRangeOverlap(Transform attacker, SkillDamageEffect effect, int layerMask, Collider[] outBuffer, SkillDetectionMotionFrame? motionFrame)
        {
            ResolveDetectionPose(attacker, effect, motionFrame, out Vector3 basePosition, out Quaternion baseRotation, out Vector3 motionOffset);
            Quaternion detectionRotation = Quaternion.Euler(effect.rotationEuler);
            Vector3 origin = basePosition + baseRotation * (effect.centerOffset + motionOffset);
            float rangeScale = PlayerBuffRuntimeUtility.GetDamageRangeScale(attacker);
            int count;

            switch (effect.shape)
            {
                case AttackShapeType.Sphere:
                    count = Physics.OverlapSphereNonAlloc(origin, effect.sphereRadius * rangeScale, outBuffer, layerMask, QueryTriggerInteraction.Collide);
                    return count;

                case AttackShapeType.Sector:
                    float radius = Mathf.Max(effect.sphereRadius * rangeScale, 0.01f);
                    count = Physics.OverlapSphereNonAlloc(origin, radius, outBuffer, layerMask, QueryTriggerInteraction.Collide);
                    if (count <= 0) return 0;
                    float halfAngle = effect.sectorAngle * 0.5f;
                    Quaternion inverseRotation = Quaternion.Inverse(detectionRotation);
                    int write = 0;
                    for (int i = 0; i < count; i++)
                    {
                        Collider collider = outBuffer[i];
                        if (collider == null)
                            continue;

                        Vector3 targetPoint = collider.bounds.center;
                        Vector3 local = inverseRotation * (targetPoint - origin);
                        Vector2 planar = new Vector2(local.x, local.z);
                        if (planar.sqrMagnitude < 0.0001f ||
                            Vector2.Angle(Vector2.up, planar) <= halfAngle)
                        {
                            if (write != i) outBuffer[write] = outBuffer[i];
                            write++;
                        }
                    }
                    return write;

                case AttackShapeType.Box:
                    Vector3 halfExtents = effect.boxSize * rangeScale * 0.5f;
                    count = Physics.OverlapBoxNonAlloc(origin, halfExtents, outBuffer, detectionRotation, layerMask, QueryTriggerInteraction.Collide);
                    return count;

                default:
                    return 0;
            }
        }

        private static int RunRaycast(Transform attacker, SkillDamageEffect effect, int layerMask, Collider[] outBuffer, SkillDetectionMotionFrame? motionFrame)
        {
            ResolveDetectionPose(attacker, effect, motionFrame, out Vector3 basePosition, out Quaternion baseRotation, out Vector3 motionOffset);
            Quaternion detectionRotation = Quaternion.Euler(effect.rotationEuler);
            Vector3 origin = basePosition + baseRotation * (effect.rayOriginOffset + motionOffset);
            Vector3 direction = detectionRotation * Vector3.forward;
            float rangeScale = PlayerBuffRuntimeUtility.GetDamageRangeScale(attacker);
            float distance = Mathf.Max(0f, effect.rayMaxDistance * rangeScale);
            float radius = Mathf.Max(0f, effect.rayRadius * rangeScale);

            int numHits = radius > 0.0001f
                ? Physics.SphereCastNonAlloc(origin, radius, direction, RaycastHitBuffer, distance, layerMask, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(origin, direction, RaycastHitBuffer, distance, layerMask, QueryTriggerInteraction.Collide);
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

        private static void ResolveDetectionPose(
            Transform attacker,
            SkillDamageEffect effect,
            SkillDetectionMotionFrame? motionFrame,
            out Vector3 basePosition,
            out Quaternion baseRotation,
            out Vector3 motionOffset)
        {
            bool useCapturedPose = motionFrame.HasValue && effect?.motion != null && effect.motion.IsActive;
            if (useCapturedPose)
            {
                basePosition = motionFrame.Value.OriginPosition;
                baseRotation = motionFrame.Value.OriginRotation;
                motionOffset = effect.motion.direction.normalized * effect.motion.speed * motionFrame.Value.Elapsed;
                return;
            }

            basePosition = attacker.position;
            baseRotation = attacker.rotation;
            motionOffset = Vector3.zero;
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

            int layerMask = ResolveLayerMask(effect.hitLayerName);
            if (layerMask == 0)
                return null;

            SkillCollisionHitbox hitbox = node.GetComponent<SkillCollisionHitbox>();
            if (hitbox == null)
                hitbox = node.gameObject.AddComponent<SkillCollisionHitbox>();

            hitbox.OpenWindow(collisionToken, attacker.root, layerMask, PlayerBuffRuntimeUtility.GetDamageRangeScale(attacker));
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

        private static int ResolveLayerMask(string hitLayerName)
        {
            if (string.IsNullOrWhiteSpace(hitLayerName))
                return 0;

            string normalized = hitLayerName.Trim();
            int directMask = LayerMask.GetMask(normalized);
            if (directMask != 0)
                return directMask;

            int directLayer = LayerMask.NameToLayer(normalized);
            if (directLayer >= 0)
                return 1 << directLayer;

            string[] tokens = normalized.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            int combinedMask = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                int layer = LayerMask.NameToLayer(token);
                if (layer >= 0)
                    combinedMask |= 1 << layer;
            }

            return combinedMask;
        }
    }
}
