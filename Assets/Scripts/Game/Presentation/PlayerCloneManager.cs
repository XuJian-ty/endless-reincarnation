using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class PlayerCloneManager : MonoBehaviour
    {
        private const string ClonePrefabResourcePath = "Prefabs/PlayerClone";
        private readonly List<PlayerCloneActor> _clones = new List<PlayerCloneActor>();
        private PlayerController _owner;
        private GameObject _clonePrefab;
        private int _processedSummonCloneCount;

        public void Initialize(PlayerController owner)
        {
            _owner = owner;
            _processedSummonCloneCount = 0;
            RefreshClonesImmediate();
        }

        private void Update()
        {
            if (_owner == null || _owner.PlayerModel == null)
                return;

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            RefreshClonesImmediate();
        }

        private void RefreshClonesImmediate()
        {
            if (_owner == null || _owner.PlayerModel == null)
                return;

            int summonCloneCount = _owner.PlayerModel.GetBuffStackCount(BuffIds.SummonClone);
            SummonCloneBuffSettings settings = PlayerBuffRuntimeUtility.GetSummonCloneSettings(_owner.transform);

            for (int i = _clones.Count - 1; i >= 0; i--)
            {
                PlayerCloneActor clone = _clones[i];
                if (clone == null)
                    _clones.RemoveAt(i);
            }

            if (summonCloneCount < _processedSummonCloneCount)
                _processedSummonCloneCount = summonCloneCount;

            while (_processedSummonCloneCount < summonCloneCount)
            {
                GameObject cloneObject = CreateCloneObject(_clones.Count + 1);
                if (cloneObject == null)
                    break;

                PlayerCloneActor clone = cloneObject.GetComponent<PlayerCloneActor>();
                if (clone == null)
                    clone = cloneObject.AddComponent<PlayerCloneActor>();
                _clones.Add(clone);
                _processedSummonCloneCount++;
            }

            for (int i = 0; i < _clones.Count; i++)
            {
                PlayerCloneActor clone = _clones[i];
                if (clone == null)
                    continue;

                if (clone.Owner != _owner)
                    clone.Initialize(_owner, settings, i, _clones.Count);
                else
                    clone.ApplySettings(settings);

                clone.SetFormation(i, _clones.Count);
            }
        }

        private void OnDisable()
        {
            ClearClones();
        }

        private void OnDestroy()
        {
            ClearClones();
        }

        private void ClearClones()
        {
            for (int i = _clones.Count - 1; i >= 0; i--)
            {
                PlayerCloneActor clone = _clones[i];
                if (clone != null)
                    Destroy(clone.gameObject);
            }

            _clones.Clear();
            _processedSummonCloneCount = 0;
        }

        private GameObject CreateCloneObject(int cloneIndex)
        {
            if (_clonePrefab == null)
                _clonePrefab = Resources.Load<GameObject>(ClonePrefabResourcePath);

            if (_clonePrefab == null)
            {
                Debug.LogError($"[PlayerCloneManager] 未找到分身预制体：Resources/{ClonePrefabResourcePath}");
                return null;
            }

            GameObject cloneObject = Instantiate(_clonePrefab, _owner.transform.position, _owner.transform.rotation);
            cloneObject.name = $"PlayerClone_{cloneIndex}";
            Transform summonsRoot = LevelRuntimeHierarchy.GetSummonsRoot();
            if (summonsRoot != null)
                cloneObject.transform.SetParent(summonsRoot, true);

            cloneObject.transform.position = _owner.transform.position;
            cloneObject.transform.rotation = _owner.transform.rotation;
            return cloneObject;
        }
    }
}
