using System;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>Base for per-playback adapters. External players call Complete or Fail from their callbacks.</summary>
    public abstract class TransitionPlayback : ITransitionPlayback
    {
        private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        private bool started;
        private bool stopped;
        private bool disposed;

        protected TransitionPlayback()
        {
            // A late failure must be observed even after the page stops waiting.
            _ = completion.Task.ContinueWith(task => { _ = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public Task Completion => completion.Task;

        public void Play()
        {
            if (started || stopped || disposed) throw new InvalidOperationException("Playback can only start once.");
            started = true;
            OnPlay();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (started && !stopped && !completion.Task.IsCompleted) OnTick(unscaledDeltaTime);
        }

        public void Stop(TransitionEndReason reason)
        {
            if (stopped) return;
            stopped = true;
            try { OnStop(reason); }
            finally { completion.TrySetCanceled(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Stop(TransitionEndReason.Interrupted); }
            finally { OnDispose(); }
        }

        protected void Complete() => completion.TrySetResult(true);
        protected void Fail(Exception error) => completion.TrySetException(error);
        protected abstract void OnPlay();
        protected virtual void OnTick(float unscaledDeltaTime) { }
        protected abstract void OnStop(TransitionEndReason reason);
        protected virtual void OnDispose() { }
    }
}
