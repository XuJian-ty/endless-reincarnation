using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class PlayerCloneManager : MonoBehaviour
    {
        private readonly List<PlayerCloneActor> _clones = new List<PlayerCloneActor>();
        private PlayerController _owner;

        public void Initialize(PlayerController owner)
        {
            _owner = owner;
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

            int desiredCount = _owner.PlayerModel.GetBuffStackCount(BuffIds.SummonClone);
            SummonCloneBuffSettings settings = PlayerBuffRuntimeUtility.GetSummonCloneSettings(_owner.transform);

            for (int i = _clones.Count - 1; i >= 0; i--)
            {
                PlayerCloneActor clone = _clones[i];
                if (clone == null)
                    _clones.RemoveAt(i);
            }

            while (_clones.Count > desiredCount)
            {
                int lastIndex = _clones.Count - 1;
                PlayerCloneActor clone = _clones[lastIndex];
                _clones.RemoveAt(lastIndex);
                if (clone != null)
                    Destroy(clone.gameObject);
            }

            while (_clones.Count < desiredCount)
            {
                GameObject cloneObject = new GameObject($"PlayerClone_{_clones.Count + 1}");
                cloneObject.transform.position = _owner.transform.position;
                cloneObject.transform.rotation = _owner.transform.rotation;
                PlayerCloneActor clone = cloneObject.AddComponent<PlayerCloneActor>();
                _clones.Add(clone);
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
        }
    }
}
