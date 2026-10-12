using System;
using System.Threading.Tasks;

namespace MUI
{
    public enum TransitionEndReason
    {
        Completed,
        Interrupted,
        Skipped,
        Failed
    }

    /// <summary>
    /// One playback, owned by the UI thread. Creation must not start work.
    /// Stop must synchronously stop all writers and callbacks, even when Completion is pending.
    /// Interrupted preserves the current pose; Dispose only releases resources.
    /// </summary>
    public interface ITransitionPlayback : IDisposable
    {
        Task Completion { get; }
        void Play();
        void Tick(float unscaledDeltaTime);
        void Stop(TransitionEndReason reason);
    }

    /// <summary>Optional playback protocol; null means no animation. Legacy sampling remains supported.</summary>
    public interface ITransitionPlaybackView : IEnterTransitionView, IExitTransitionView
    {
        bool TransitionsEnabled { get; }
        ITransitionPlayback CreateEnterPlayback();
        ITransitionPlayback CreateExitPlayback();
    }
}
