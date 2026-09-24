using System;

namespace MUI.Notifications
{
    /// <summary>不可变通知请求；有效期包含排队等待时间，不保证每条已接受通知都能展示。</summary>
    public sealed class Notification
    {
        /// <summary>创建通知请求；两个时长均为有限正数，单位为秒。</summary>
        /// <param name="message">非空展示文案。</param>
        /// <param name="key">可选去重键；null 不去重，非 null 时不能为空白。</param>
        /// <param name="priority">数值越大越优先；不抢占正在展示的通知。</param>
        /// <param name="displayDuration">成为当前通知后最多展示的时间。</param>
        /// <param name="expiresAfter">从入队起计算的总有效期，包含等待和展示。</param>
        public Notification(string message, string key = null, int priority = 0,
            double displayDuration = 3, double expiresAfter = 30)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Notification message is required.", nameof(message));
            }

            if (key != null && string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Deduplication key cannot be blank.", nameof(key));
            }

            RequireDuration(displayDuration, nameof(displayDuration));
            RequireDuration(expiresAfter, nameof(expiresAfter));
            Message = message;
            Key = key;
            Priority = priority;
            DisplayDuration = displayDuration;
            ExpiresAfter = expiresAfter;
        }

        /// <summary>展示文案。</summary>
        public string Message
        {
            get;
        }

        /// <summary>按 Ordinal 比较的去重键；null 表示允许重复。</summary>
        public string Key
        {
            get;
        }

        /// <summary>等待队列中的优先级，相同优先级按入队顺序处理。</summary>
        public int Priority
        {
            get;
        }

        /// <summary>开始展示后的最长持续秒数。</summary>
        public double DisplayDuration
        {
            get;
        }

        /// <summary>自入队起的总有效秒数，可能使通知在展示前过期。</summary>
        public double ExpiresAfter
        {
            get;
        }

        private static void RequireDuration(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
