using System.Collections.Generic;
using ProjectBase;
using UnityEngine;

namespace Game.UI
{
    internal sealed class UiGameObjectPool
    {
        private readonly Dictionary<GameObject, GameObject> _instancePrefabs = new Dictionary<GameObject, GameObject>();

        public GameObject Acquire(GameObject prefab, Transform parent, string instanceName = null)
        {
            if (prefab == null || parent == null)
                return null;

            GameObject instance = PoolMgr.GetInstance().GetObjSync(prefab, parent);
            if (instance == null)
                return null;

            if (!string.IsNullOrEmpty(instanceName))
                instance.name = instanceName;

            _instancePrefabs[instance] = prefab;
            return instance;
        }

        public void Release(GameObject instance, Transform parent)
        {
            if (instance == null)
                return;

            if (!_instancePrefabs.TryGetValue(instance, out GameObject prefab) || prefab == null)
            {
                Object.Destroy(instance);
                return;
            }

            PoolMgr.GetInstance().PushObj(prefab, instance);
            _instancePrefabs.Remove(instance);
        }

        public void Clear()
        {
            foreach (KeyValuePair<GameObject, GameObject> pair in _instancePrefabs)
            {
                if (pair.Key == null || pair.Value == null)
                    continue;

                PoolMgr.GetInstance().PushObj(pair.Value, pair.Key);
            }

            _instancePrefabs.Clear();
        }
    }
}
