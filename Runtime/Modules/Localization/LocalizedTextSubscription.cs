using System;

namespace MUI.Localization
{
    /// <summary>将订阅托管到目标生命周期；每个订阅独占对应目标的文本写入，所有操作在服务所属 UI 线程执行。</summary>
    public sealed class LocalizedTextSubscription : IDisposable
    {
        private LocalizationService service;
        private LocalizedMessage message;
        private Action<LocalizedTextValue> apply;
        private bool refreshing;
        private bool pending;

        internal LocalizedTextSubscription(LocalizationService service, LocalizedMessage message, Action<LocalizedTextValue> apply)
        {
            this.service = service;
            this.message = message ?? throw new ArgumentNullException(nameof(message));
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
            service.Subscribe(Refresh);
            try
            {
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>当前文本请求；赋新值会立即重新格式化和应用，不要求更换语言。</summary>
        public LocalizedMessage Message
        {
            get => message;
            set
            {
                if (service == null)
                {
                    throw new ObjectDisposedException(nameof(LocalizedTextSubscription));
                }

                service.CheckAccess();
                message = value ?? throw new ArgumentNullException(nameof(value));
                Refresh();
            }
        }

        // 参数格式化和目标赋值都可能回调业务代码；重入只标记下一轮刷新，避免递归覆盖。
        private void Refresh()
        {
            if (service == null)
            {
                return;
            }

            pending = true;
            if (refreshing)
            {
                return;
            }

            refreshing = true;
            try
            {
                var iterations = 0;
                while (pending && service != null)
                {
                    if (++iterations > 32)
                    {
                        throw new InvalidOperationException("Localized text did not stabilize after 32 updates.");
                    }

                    pending = false;
                    var current = message;
                    var version = service.Version;
                    var value = service.Format(current);
                    if (service == null)
                    {
                        return;
                    }

                    if (!ReferenceEquals(current, message) || version != service.Version)
                    {
                        pending = true;
                        continue;
                    }

                    apply(value);
                }
            }
            finally
            {
                refreshing = false;
                pending = false;
            }
        }

        /// <summary>幂等退订并解除消息与回调引用；保留目标最后显示的文本。</summary>
        public void Dispose()
        {
            if (service == null)
            {
                return;
            }

            service.Unsubscribe(Refresh);
            service = null;
            message = null;
            apply = null;
        }
    }
}
