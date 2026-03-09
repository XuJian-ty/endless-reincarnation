using System;
using Game.UI;

namespace Game.GameFlow
{
    /// <summary>
    /// Level UI model locator. Set by LevelBootstrapper on level entry; clear on level exit.
    /// </summary>
    public static class LevelUIModelLocator
    {
        private static ILevelUIModel _instance;

        public static ILevelUIModel Get() => _instance;

        public static void Set(ILevelUIModel model)
        {
            if (ReferenceEquals(_instance, model))
                return;

            if (_instance is IDisposable disposable)
                disposable.Dispose();

            _instance = model;
        }
    }
}
