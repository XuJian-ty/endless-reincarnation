using System.Collections.Generic;
using UnityEngine;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// 让技能运行时生成的音效/特效在游戏暂停时暂停，在恢复时继续。
    /// 这里只处理 Cue 本体及其子节点上的 AudioSource / ParticleSystem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillCuePauseProxy : MonoBehaviour
    {
        private readonly List<AudioSource> _pausedAudioSources = new List<AudioSource>();
        private readonly List<ParticleSystem> _pausedParticleSystems = new List<ParticleSystem>();
        private bool _isPausedByGameplayState;

        private void OnEnable()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm != null)
                gsm.OnStateChanged += OnGameStateChanged;

            ApplyPauseState(gsm?.IsGameplayPaused == true);
        }

        private void OnDisable()
        {
            GameStateMachine gsm = GameStateMachine.GetInstance();
            if (gsm != null)
                gsm.OnStateChanged -= OnGameStateChanged;

            _pausedAudioSources.Clear();
            _pausedParticleSystems.Clear();
            _isPausedByGameplayState = false;
        }

        private void OnGameStateChanged(GameStateMachine.State state)
        {
            ApplyPauseState(state == GameStateMachine.State.Paused);
        }

        private void ApplyPauseState(bool paused)
        {
            if (_isPausedByGameplayState == paused)
                return;

            _isPausedByGameplayState = paused;
            if (paused)
                PauseOwnedCues();
            else
                ResumeOwnedCues();
        }

        private void PauseOwnedCues()
        {
            _pausedAudioSources.Clear();
            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource audioSource = audioSources[i];
                if (audioSource == null || !audioSource.isPlaying)
                    continue;

                audioSource.Pause();
                _pausedAudioSources.Add(audioSource);
            }

            _pausedParticleSystems.Clear();
            ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null || particleSystem.isPaused || !particleSystem.IsAlive(true))
                    continue;

                particleSystem.Pause(true);
                _pausedParticleSystems.Add(particleSystem);
            }
        }

        private void ResumeOwnedCues()
        {
            for (int i = 0; i < _pausedAudioSources.Count; i++)
            {
                AudioSource audioSource = _pausedAudioSources[i];
                if (audioSource != null && audioSource.gameObject.activeInHierarchy)
                    audioSource.UnPause();
            }

            _pausedAudioSources.Clear();

            for (int i = 0; i < _pausedParticleSystems.Count; i++)
            {
                ParticleSystem particleSystem = _pausedParticleSystems[i];
                if (particleSystem != null && particleSystem.gameObject.activeInHierarchy)
                    particleSystem.Play(true);
            }

            _pausedParticleSystems.Clear();
        }
    }
}
