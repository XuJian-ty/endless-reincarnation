using System.Collections.Generic;

namespace Game.Presentation
{
    /// <summary>
    /// 管理已脱离原始状态/动作、但仍需继续推进的技能时间轴实例。
    /// 统一处理添加、Tick、停止和相机覆盖查询，避免玩家/分身/敌人各自维护重复逻辑。
    /// </summary>
    public sealed class TimelineRunnerCollection
    {
        private readonly List<SkillTimelineRunner> _runners = new List<SkillTimelineRunner>();

        public void Add(SkillTimelineRunner runner)
        {
            if (runner == null || runner.IsComplete || _runners.Contains(runner))
                return;

            _runners.Add(runner);
        }

        public void DetachOrStop(SkillTimelineRunner runner)
        {
            if (runner == null)
                return;

            runner.StopStateScopedCues();
            if (runner.HasPendingWork)
                Add(runner);
            else
                runner.Stop();
        }

        public void Tick(float deltaTime)
        {
            if (_runners.Count <= 0)
                return;

            for (int i = _runners.Count - 1; i >= 0; i--)
            {
                SkillTimelineRunner runner = _runners[i];
                if (runner == null || runner.IsComplete)
                {
                    _runners.RemoveAt(i);
                    continue;
                }

                runner.Tick(deltaTime);
                if (runner.IsComplete)
                    _runners.RemoveAt(i);
            }
        }

        public void StopAll()
        {
            if (_runners.Count <= 0)
                return;

            for (int i = _runners.Count - 1; i >= 0; i--)
                _runners[i]?.Stop();

            _runners.Clear();
        }

        public bool TryGetCameraOverride(float lookTargetHeight, out SkillTimelineRunner.CameraOverrideRequest request)
        {
            request = default;
            if (_runners.Count <= 0)
                return false;

            for (int i = _runners.Count - 1; i >= 0; i--)
            {
                SkillTimelineRunner runner = _runners[i];
                if (runner != null && !runner.IsComplete && runner.TryGetCurrentCameraOverride(lookTargetHeight, out request))
                    return request.IsActive;
            }

            return false;
        }
    }
}
