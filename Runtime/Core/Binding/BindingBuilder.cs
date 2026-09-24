using System;
using System.ComponentModel;

namespace MUI
{
    /// <summary>生成绑定使用的强类型构建单元，运行时不使用反射。</summary>
    public sealed partial class BindingBuilder<TViewModel>
        where TViewModel : ViewModel
    {
        private readonly IView view;
        private readonly TViewModel model;
        private readonly BindingSession session;

        internal BindingBuilder(IView view, TViewModel model, BindingSession session)
        {
            this.view = view;
            this.model = model;
            this.session = session;
        }

        public void Property<TElement, TValue>(string elementName,
                    string sourceProperty,
                    Func<TViewModel, TValue> readSource,
                    Action<TViewModel, TValue> writeSource,
                    string targetProperty,
                    Func<TElement, TValue> readTarget,
                    Action<TElement, TValue> writeTarget,
                    BindingMode mode = BindingMode.OneWay)
                    where TElement : class, IElement
        {
            Property<TElement, TValue, TValue>(elementName, sourceProperty, readSource, writeSource, targetProperty, readTarget, writeTarget, value => value, value => value, mode);
        }

        public void Property<TElement, TSource, TTarget>(string elementName,
                    string sourceProperty,
                    Func<TViewModel, TSource> readSource,
                    Action<TViewModel, TSource> writeSource,
                    string targetProperty,
                    Func<TElement, TTarget> readTarget,
                    Action<TElement, TTarget> writeTarget,
                    Func<TSource, TTarget> convert,
                    Func<TTarget, TSource> convertBack,
                    BindingMode mode = BindingMode.OneWay)
                    where TElement : class, IElement
        {
            if (string.IsNullOrEmpty(sourceProperty))
            {
                throw new ArgumentException("Source property is required.", nameof(sourceProperty));
            }

            if (string.IsNullOrEmpty(targetProperty))
            {
                throw new ArgumentException("Target property is required.", nameof(targetProperty));
            }

            if (!Enum.IsDefined(typeof(BindingMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (mode != BindingMode.OneWayToSource && (readSource == null || writeTarget == null || convert == null))
            {
                throw new ArgumentException("Forward binding requires readable source, writable target and converter.");
            }

            if ((mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource) && (readTarget == null || writeSource == null || convertBack == null))
            {
                throw new ArgumentException("Reverse binding requires readable target, writable source and reverse converter.");
            }

            var element = view.GetElement<TElement>(elementName);
            session.RegisterPropertyWriter(element, sourceProperty, targetProperty, mode);
            var updating = false;
            var forwardPending = false;
            void Forward()
            {
                if (!session.IsActive)
                {
                    return;
                }

                forwardPending = true;
                if (updating)
                {
                    return;
                }

                updating = true;
                try
                {
                    var hasApplied = false;
                    var previous = default(TSource);
                    var passes = 0;
                    while (forwardPending && session.IsActive)
                    {
                        forwardPending = false;
                        if (!element.IsAlive)
                        {
                            throw new InvalidOperationException($"Element '{elementName}' was destroyed.");
                        }

                        var current = readSource(model);
                        if (!session.IsActive)
                        {
                            return;
                        }

                        // 来源值回传无害，但新值仍必须到达目标。
                        if (hasApplied && System.Collections.Generic.EqualityComparer<TSource>.Default.Equals(previous, current))
                        {
                            continue;
                        }

                        if (++passes > 32)
                        {
                            throw new InvalidOperationException($"Binding '{sourceProperty}' did not stabilize after 32 synchronous updates.");
                        }

                        previous = current;
                        hasApplied = true;
                        var converted = convert(current);
                        if (!session.IsActive)
                        {
                            return;
                        }

                        if (!element.IsAlive)
                        {
                            throw new InvalidOperationException($"Element '{elementName}' was destroyed during conversion.");
                        }

                        writeTarget(element, converted);
                    }
                }
                finally
                {
                    updating = false;
                    forwardPending = false;
                }
            }

            void Reverse()
            {
                // 由自身写入触发的目标事件属于回传，不是新的用户输入。
                if (!session.IsActive || !session.SourcesCommitted || updating)
                {
                    return;
                }

                if (!element.IsAlive)
                {
                    throw new InvalidOperationException($"Element '{elementName}' was destroyed.");
                }

                updating = true;
                var succeeded = false;
                try
                {
                    var targetValue = readTarget(element);
                    if (!session.IsActive)
                    {
                        return;
                    }

                    var converted = convertBack(targetValue);
                    if (!session.IsActive)
                    {
                        return;
                    }

                    writeSource(model, converted);
                    succeeded = true;
                }
                finally
                {
                    updating = false;
                    if (!succeeded)
                    {
                        forwardPending = false;
                    }
                }

                // 即使设置器钳制或拒绝输入后值未变化、没有发出
                // PropertyChanged，也要重新读取规范化后的来源值。
                if (mode == BindingMode.TwoWay)
                {
                    Forward();
                }
            }

            if (mode != BindingMode.OneWayToSource)
            {
                if (mode != BindingMode.OneTime)
                {
                    PropertyChangedEventHandler onSource = (sender, args) =>
                    {
                        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == sourceProperty)
                        {
                            Forward();
                        }
                    };
                    session.AddDetach(() => model.PropertyChanged -= onSource);
                    model.PropertyChanged += onSource;
                }

                Forward();
            }

            if (mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource)
            {
                PropertyChangedEventHandler onTarget = (sender, args) =>
                {
                    if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == targetProperty)
                    {
                        Reverse();
                    }
                };
                session.AddDetach(() => element.PropertyChanged -= onTarget);
                element.PropertyChanged += onTarget;
                if (mode == BindingMode.OneWayToSource)
                {
                    var staged = convertBack(readTarget(element));
                    session.StageSourceWrite(() => writeSource(model, staged));
                }
            }
        }
    }
}
