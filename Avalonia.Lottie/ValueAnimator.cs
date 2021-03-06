using System;
using System.Threading;

namespace Avalonia.Lottie
{
    public abstract class ValueAnimator : Animator, IDisposable
    {
        protected ValueAnimator()
        {
            _interpolator = new AccelerateDecelerateInterpolator();
        }

        public class ValueAnimatorUpdateEventArgs : EventArgs
        {
            public ValueAnimator Animation { get; }

            public ValueAnimatorUpdateEventArgs(ValueAnimator animation)
            {
                Animation = animation;
            }
        }

        public event EventHandler ValueChanged;
        public event EventHandler<ValueAnimatorUpdateEventArgs> Update;

        public void RemoveAllUpdateListeners()
        {
            Update = null;
        }

        public void RemoveAllListeners()
        {
            ValueChanged = null;
        }

        private IInterpolator _interpolator;
        private Timer _timer;

        public abstract float FrameRate { get; set; }

        protected virtual void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public int RepeatCount { get; set; }
        public virtual RepeatMode RepeatMode { get; set; }

        public override bool IsRunning => _timer != null;

        public virtual IInterpolator Interpolator
        {
            get => _interpolator;
            set
            {
                if (value == null)
                    value = new LinearInterpolator();
                _interpolator = value;
            }
        }

        public abstract float AnimatedFraction { get; }

        protected void OnAnimationUpdate()
        {
            Update?.Invoke(this, new ValueAnimatorUpdateEventArgs(this));
        }

        protected void PrivateStart()
        {
            if (_timer == null)
            {
                _timer = new Timer(TimerCallback, null, TimeSpan.Zero, GetTimerInterval());
            }
        }

        protected void UpdateTimerInterval()
        {
            _timer?.Change(TimeSpan.Zero, GetTimerInterval());
        }

        private TimeSpan GetTimerInterval()
        {
            return TimeSpan.FromTicks((long)Math.Floor(TimeSpan.TicksPerSecond / (decimal)FrameRate));
        }

        protected virtual void RemoveFrameCallback()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void TimerCallback(object state)
        {
            DoFrame();
        }

        public virtual void DoFrame()
        {
            OnValueChanged();
        }

        protected long SystemnanoTime()
        {
            var nano = 10000L * DateTime.Now.Ticks;
            nano /= TimeSpan.TicksPerMillisecond;
            nano *= 100L;
            return nano;
        }

        protected virtual void Disposing(bool disposing)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }


            if (Update != null)
                foreach (EventHandler<ValueAnimatorUpdateEventArgs> handler in Update.GetInvocationList())
                    Update -= handler;

            if (ValueChanged != null)
                foreach (EventHandler handler in ValueChanged.GetInvocationList())
                    ValueChanged -= handler;

        }

        public void Dispose()
        {
            Disposing(true);
            GC.SuppressFinalize(this);
        }
    }
}