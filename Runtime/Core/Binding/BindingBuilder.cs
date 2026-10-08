using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MUI
{
    /// <summary>生成绑定使用的强类型构建单元，运行时不使用反射。</summary>
    public sealed partial class BindingBuilder<TViewModel>
        where TViewModel : ViewModel
    {
        private readonly IView view;
        private readonly TViewModel model;
        private readonly BindingSession session;
        private readonly BindingPreview preview;
        private readonly int bindingThreadId = Thread.CurrentThread.ManagedThreadId;
        private int wrongThreadReported;

        internal BindingBuilder(IView view, TViewModel model, BindingSession session, BindingPreview preview = null)
        {
            this.view = view;
            this.model = model;
            this.session = session;
            this.preview = preview;
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
                    BindingMode mode = BindingMode.OneWay,
                    Func<TTarget, BindingConversionResult<TSource>> tryConvertBack = null,
                    Action<TViewModel, BindingValidationState> writeValidation = null,
                    BindingSourcePath<TViewModel> sourcePath = null,
                    TTarget nullValue = default)
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

            var reverseBinding = mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource;
            if (reverseBinding && (readTarget == null || writeSource == null || (convertBack == null && tryConvertBack == null)))
            {
                throw new ArgumentException("Reverse binding requires readable target, writable source and reverse converter.");
            }

            if (!reverseBinding && (tryConvertBack != null || writeValidation != null))
            {
                throw new ArgumentException("Input validation requires a reverse binding.");
            }

            BindingConversionResult<TSource> ConvertReverse(TTarget value)
            {
                try
                {
                    return tryConvertBack == null
                        ? BindingConversionResult<TSource>.Success(convertBack(value)) : tryConvertBack(value);
                }
                catch (Exception error) when (!(error is OperationCanceledException))
                {
                    // 只拦截转换调用；目标读取、模型 setter 和校验发布失败仍是实际绑定故障。
                    return BindingConversionResult<TSource>.Failure(error.Message);
                }
            }

            void PublishValidation(BindingValidationState state)
            {
                if (session.IsActive && session.SourcesCommitted && writeValidation != null)
                {
                    writeValidation(model, state);
                }
            }

            var element = view.GetElement<TElement>(elementName);
            session.RegisterPropertyWriter(element, sourceProperty, targetProperty, mode);
            if (preview != null)
            {
                var owners = sourcePath == null ? null : sourcePath.CaptureOwners(model);
                if (owners != null)
                {
                    preview.AddCheck(sourceProperty, () => sourcePath.HasOwners(model, owners));
                }

                if (mode != BindingMode.OneWayToSource)
                {
                    var available = owners == null || owners[owners.Length - 1] != null;
                    var value = available ? convert(readSource(model)) : nullValue;
                    preview.Add(element, targetProperty, value,
                        () => (owners == null || sourcePath.HasOwners(model, owners)) &&
                            EqualityComparer<TTarget>.Default.Equals(value, available ? convert(readSource(model)) : nullValue));
                }

                if (mode == BindingMode.OneWayToSource && (owners == null || owners[owners.Length - 1] != null))
                {
                    ConvertReverse(readTarget(element));
                }

                return;
            }

            var updating = false;
            var forwardPending = false;
            SourcePathSubscription pathSubscription = null;
            INotifyPropertyChanged SourceOwner() => sourcePath == null ? model : sourcePath.GetOwner(model);
            long SourceRevision() => pathSubscription == null ? 0 : pathSubscription.Revision;
            bool SourceIsCurrent(INotifyPropertyChanged owner, long revision) => session.IsActive &&
                revision == SourceRevision() && ReferenceEquals(owner, SourceOwner());
            void Forward()
            {
                if (!IsBindingThread())
                {
                    return;
                }

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
                    INotifyPropertyChanged previousOwner = null;
                    var passes = 0;
                    while (forwardPending && session.IsActive)
                    {
                        forwardPending = false;
                        if (!element.IsAlive)
                        {
                            throw new InvalidOperationException($"Element '{elementName}' was destroyed.");
                        }

                        if (++passes > 32)
                        {
                            throw new InvalidOperationException($"Binding '{sourceProperty}' did not stabilize after 32 synchronous updates.");
                        }

                        var revision = SourceRevision();
                        var owner = SourceOwner();
                        var current = owner == null ? default : readSource(model);
                        if (!session.IsActive)
                        {
                            return;
                        }

                        // 来源值回传无害，但新值仍必须到达目标。
                        if (!SourceIsCurrent(owner, revision))
                        {
                            forwardPending = true;
                            continue;
                        }

                        if (hasApplied && ReferenceEquals(owner, previousOwner) && EqualityComparer<TSource>.Default.Equals(previous, current))
                        {
                            continue;
                        }

                        previous = current;
                        previousOwner = owner;
                        hasApplied = true;
                        var converted = owner == null ? nullValue : convert(current);
                        if (!session.IsActive)
                        {
                            return;
                        }

                        if (!SourceIsCurrent(owner, revision))
                        {
                            hasApplied = false;
                            forwardPending = true;
                            continue;
                        }

                        if (!element.IsAlive)
                        {
                            throw new InvalidOperationException($"Element '{elementName}' was destroyed during conversion.");
                        }

                        writeTarget(element, converted);
                        PublishValidation(default);
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
                if (!IsBindingThread())
                {
                    return;
                }

                // 由自身写入触发的目标事件属于回传，不是新的用户输入。
                if (!session.IsActive || !session.SourcesCommitted || updating)
                {
                    return;
                }

                var revision = SourceRevision();
                var owner = SourceOwner();
                if (owner == null)
                {
                    return;
                }

                if (!element.IsAlive)
                {
                    throw new InvalidOperationException($"Element '{elementName}' was destroyed.");
                }

                updating = true;
                var succeeded = false;
                var rejected = false;
                try
                {
                    var targetValue = readTarget(element);
                    if (!SourceIsCurrent(owner, revision))
                    {
                        return;
                    }

                    var converted = ConvertReverse(targetValue);
                    if (!SourceIsCurrent(owner, revision))
                    {
                        return;
                    }

                    if (!converted.Succeeded)
                    {
                        rejected = true;
                        PublishValidation(converted.Validation);
                        return;
                    }

                    writeSource(model, converted.Value);
                    PublishValidation(default);
                    succeeded = true;
                }
                finally
                {
                    updating = false;
                    if (!succeeded)
                    {
                        var sourceChangedDuringValidation = (rejected || sourcePath != null) && forwardPending;
                        forwardPending = false;
                        if (sourceChangedDuringValidation && mode == BindingMode.TwoWay)
                        {
                            // 校验观察者可以同步更新模型；该新值优先于被拒绝的旧输入草稿。
                            Forward();
                        }
                    }
                }

                // 即使设置器钳制或拒绝输入后值未变化、没有发出
                // PropertyChanged，也要重新读取规范化后的来源值。
                if (mode == BindingMode.TwoWay)
                {
                    Forward();
                }
            }

            if (sourcePath != null && mode != BindingMode.OneTime)
            {
                pathSubscription = new SourcePathSubscription(this, sourcePath,
                    () => { if (mode != BindingMode.OneWayToSource) { Forward(); } });
                session.AddDetach(pathSubscription.Dispose);
                pathSubscription.Connect();
            }

            if (mode != BindingMode.OneWayToSource)
            {
                if (mode != BindingMode.OneTime && sourcePath == null)
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
                    var owner = SourceOwner();
                    var revision = SourceRevision();
                    if (owner == null)
                    {
                        return;
                    }

                    var staged = ConvertReverse(readTarget(element));
                    session.StageSourceWrite(() =>
                    {
                        if (!SourceIsCurrent(owner, revision))
                        {
                            return;
                        }

                        if (staged.Succeeded)
                        {
                            writeSource(model, staged.Value);
                            PublishValidation(default);
                        }
                        else
                        {
                            PublishValidation(staged.Validation);
                        }
                    });
                }
                else
                {
                    session.OnReady(() => PublishValidation(default));
                }
            }
        }

        private bool IsBindingThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == bindingThreadId)
            {
                return true;
            }

            if (Interlocked.Exchange(ref wrongThreadReported, 1) == 0)
            {
                UIErrors.Report(new InvalidOperationException("Binding notifications must run on the owning UI thread."));
            }

            return false;
        }
    }

    /// <summary>候选绑定只读快照。异步目标准备失败时不订阅或写入当前 View。</summary>
    internal sealed class BindingPreview : IPreparedBindingRebind
    {
        private readonly List<ReadCheck> checks = new List<ReadCheck>();
        private readonly List<TargetValues> targets = new List<TargetValues>();
        private readonly List<IPreparedBindingTarget> prepared = new List<IPreparedBindingTarget>();
        private bool committed;

        internal void Add(IElement element, string property, object value, Func<bool> stillCurrent)
        {
            AddCheck(property, stillCurrent);
            if (!(element is IBindingRebindTarget))
            {
                return;
            }

            TargetValues target = null;
            foreach (var entry in targets)
            {
                if (ReferenceEquals(entry.Element, element))
                {
                    target = entry;
                    break;
                }
            }

            if (target == null)
            {
                target = new TargetValues(element);
                targets.Add(target);
            }

            target.Values.Add(property, value);
        }

        internal void AddCheck(string property, Func<bool> stillCurrent) => checks.Add(new ReadCheck(property, stillCurrent));

        internal async ValueTask PrepareAsync(CancellationToken token)
        {
            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                var candidate = await ((IBindingRebindTarget)target.Element).PrepareRebindAsync(target.Values, token);
                if (candidate == null)
                {
                    throw new InvalidOperationException("Binding rebind target returned no preparation.");
                }

                prepared.Add(candidate);
            }
        }

        public void Validate()
        {
            foreach (var check in checks)
            {
                if (!check.StillCurrent())
                {
                    throw new OperationCanceledException($"Binding source for '{check.Property}' changed during rebind preparation.");
                }
            }

            foreach (var target in prepared)
            {
                target.Validate();
            }
        }

        public void BeginCommit()
        {
            foreach (var target in prepared)
            {
                target.BeginCommit();
            }
        }

        public void Commit()
        {
            foreach (var target in prepared)
            {
                target.Commit();
            }

            committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            List<Exception> errors = null;
            for (var i = prepared.Count - 1; i >= 0; --i)
            {
                try
                {
                    await prepared[i].DisposeAsync();
                }
                catch (Exception error)
                {
                    if (errors == null)
                    {
                        errors = new List<Exception>();
                    }

                    errors.Add(error);
                }
            }

            prepared.Clear();
            if (errors != null)
            {
                throw new AggregateException(committed ? "Committed binding cleanup failed." : "Binding preparation cleanup failed.", errors);
            }
        }

        private readonly struct ReadCheck
        {
            internal readonly string Property;
            internal readonly Func<bool> StillCurrent;

            internal ReadCheck(string property, Func<bool> stillCurrent)
            {
                Property = property;
                StillCurrent = stillCurrent;
            }
        }

        private sealed class TargetValues
        {
            internal readonly IElement Element;
            internal readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.Ordinal);

            internal TargetValues(IElement element)
            {
                Element = element;
            }
        }
    }
}
