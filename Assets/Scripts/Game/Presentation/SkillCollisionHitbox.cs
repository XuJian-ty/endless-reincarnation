using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    [RequireComponent(typeof(Collider))]
    public sealed class SkillCollisionHitbox : MonoBehaviour
    {
        private sealed class ListenerState
        {
            public Transform ownerRoot;
            public int layerMask;
            public float scaleMultiplier = 1f;
            public readonly List<Collider> pendingHits = new List<Collider>(8);
            public readonly HashSet<Collider> pendingSet = new HashSet<Collider>();
        }

        private readonly Dictionary<object, ListenerState> _listeners = new Dictionary<object, ListenerState>();
        private Collider _hitboxCollider;
        private Rigidbody _rigidbody;
        private Vector3 _baseLocalScale = Vector3.one;

        private void Awake()
        {
            EnsureRuntimeSetup();
            RefreshColliderEnabled();
        }

        private void OnEnable()
        {
            EnsureRuntimeSetup();
            RefreshColliderEnabled();
        }

        private void OnDisable()
        {
            _listeners.Clear();
            RefreshColliderEnabled();
        }

        public void OpenWindow(object token, Transform ownerRoot, int layerMask, float scaleMultiplier)
        {
            if (token == null)
                return;

            EnsureRuntimeSetup();

            if (!_listeners.TryGetValue(token, out ListenerState listener))
            {
                listener = new ListenerState();
                _listeners.Add(token, listener);
            }

            listener.ownerRoot = ownerRoot;
            listener.layerMask = layerMask;
            listener.scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            listener.pendingHits.Clear();
            listener.pendingSet.Clear();
            RefreshColliderEnabled();
        }

        public void CloseWindow(object token)
        {
            if (token == null)
                return;

            _listeners.Remove(token);
            RefreshColliderEnabled();
        }

        public int ConsumeHits(object token, Collider[] outBuffer)
        {
            if (token == null || outBuffer == null || outBuffer.Length == 0)
                return 0;

            if (!_listeners.TryGetValue(token, out ListenerState listener))
                return 0;

            int count = Mathf.Min(listener.pendingHits.Count, outBuffer.Length);
            for (int i = 0; i < count; i++)
                outBuffer[i] = listener.pendingHits[i];

            listener.pendingHits.Clear();
            listener.pendingSet.Clear();
            return count;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || _listeners.Count == 0)
                return;

            foreach (KeyValuePair<object, ListenerState> pair in _listeners)
            {
                ListenerState listener = pair.Value;
                if (listener == null)
                    continue;

                if ((listener.layerMask & (1 << other.gameObject.layer)) == 0)
                    continue;

                if (listener.ownerRoot != null && other.transform.root == listener.ownerRoot)
                    continue;

                if (listener.pendingSet.Add(other))
                    listener.pendingHits.Add(other);
            }
        }

        private void EnsureRuntimeSetup()
        {
            if (_hitboxCollider == null)
                _hitboxCollider = GetComponent<Collider>();

            _baseLocalScale = transform.localScale;

            if (_hitboxCollider != null)
                _hitboxCollider.isTrigger = true;

            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            if (_rigidbody == null && GetComponentInParent<Rigidbody>() == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }
        }

        private void RefreshColliderEnabled()
        {
            float appliedScale = 1f;
            foreach (KeyValuePair<object, ListenerState> pair in _listeners)
            {
                ListenerState listener = pair.Value;
                if (listener == null)
                    continue;

                appliedScale = Mathf.Max(appliedScale, listener.scaleMultiplier);
            }

            transform.localScale = _baseLocalScale * appliedScale;
            if (_hitboxCollider != null)
                _hitboxCollider.enabled = _listeners.Count > 0;
        }
    }
}
