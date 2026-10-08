using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MUI
{
    public sealed partial class BindingBuilder<TViewModel>
        where TViewModel : ViewModel
    {
        public void Command<TElement>(string elementName, Func<TViewModel, IUICommand> readCommand, Action<TElement, Action> subscribe, Action<TElement, Action> unsubscribe, Action<TElement, bool> setInteractable, string interactableProperty = "Interactable")
            where TElement : class, IElement
        {
            if (readCommand == null)
            {
                throw new ArgumentNullException(nameof(readCommand));
            }

            if (subscribe == null)
            {
                throw new ArgumentNullException(nameof(subscribe));
            }

            if (unsubscribe == null)
            {
                throw new ArgumentNullException(nameof(unsubscribe));
            }

            if (setInteractable == null)
            {
                throw new ArgumentNullException(nameof(setInteractable));
            }

            if (string.IsNullOrEmpty(interactableProperty))
            {
                throw new ArgumentException("Command input property is required.", nameof(interactableProperty));
            }

            var element = view.GetElement<TElement>(elementName);
            var command = readCommand(model) ?? throw new InvalidOperationException("Command property returned null.");

            session.HasAsynchronousCommands = true;

            session.RegisterPropertyWriter(element, "Command(" + elementName + ")", interactableProperty, BindingMode.OneWay);
            if (preview != null)
            {
                return;
            }

            var refreshing = false;
            var refreshPending = false;
            Action refreshAction = RefreshCore;
            void Refresh() => RunBinding(refreshAction, "CommandRefresh");
            void RefreshCore()
            {
                if (!IsBindingThread())
                {
                    return;
                }

                if (!session.IsActive || !element.IsAlive)
                {
                    return;
                }

                refreshPending = true;
                if (refreshing)
                {
                    return;
                }

                refreshing = true;
                try
                {
                    var passes = 0;
                    bool? applied = null;
                    while (refreshPending && session.IsActive && element.IsAlive)
                    {
                        refreshPending = false;
                        if (++passes > 32)
                        {
                            throw new InvalidOperationException($"Command binding '{elementName}' did not stabilize after 32 updates.");
                        }

                        var enabled = session.IsReady && !session.Commands.IsEnded && command.CanExecute;
                        if (!session.IsActive || !element.IsAlive)
                        {
                            return;
                        }

                        enabled = enabled && session.IsReady && !session.Commands.IsEnded;
                        if (applied == enabled)
                        {
                            continue;
                        }

                        applied = enabled;
                        setInteractable(element, enabled);
                    }
                }
                finally
                {
                    refreshing = false;
                    refreshPending = false;
                }
            }

            Action invoke = () =>
            {
                if (!session.IsActive || session.Commands.IsEnded || !session.IsReady || !element.IsAlive)
                {
                    return;
                }

                if (!view.IsAlive || (view is IInputView input && !input.IsInputEnabled))
                {
                    return;
                }

                _ = ExecuteObservedAsync(command);
            };
            PropertyChangedEventHandler onCommand = (sender, args) => Refresh();
            PropertyChangedEventHandler onModel = (sender, args) =>
            {
                if (session.IsActive && !session.Commands.IsEnded)
                {
                    // 各绑定已经独立订阅模型，不能再次广播命令状态，造成共享命令的重复求值。
                    // 保留原命令通知路径的异常隔离，控件刷新失败不能中断模型的其他订阅者。
                    try
                    {
                        Refresh();
                    }
                    catch (Exception error)
                    {
                        UIErrors.Report(error);
                    }
                }
            };
            session.AddFinalizer(() =>
            {
                if (element.IsAlive)
                {
                    setInteractable(element, false);
                }
            });
            session.AddDetach(() => unsubscribe(element, invoke));
            session.AddDetach(() => command.PropertyChanged -= onCommand);
            session.AddDetach(() => model.PropertyChanged -= onModel);
            command.PropertyChanged += onCommand;
            model.PropertyChanged += onModel;
            subscribe(element, invoke);
            session.OnReady(Refresh);
            Refresh();
        }

        private async Task ExecuteObservedAsync(IUICommand command)
        {
            using (BeginBindingPhase("Execute", "Command"))
            {
                try
                {
                    CommandOutcome outcome;
                    using (session.EnterCommandExecution())
                    {
                        outcome = await session.Commands.RunAsync(token => command.ExecuteAsync(session.CommandTarget, token));
                    }

                    if (outcome.Status == CommandStatus.Failed)
                    {
                        UIErrors.Report(outcome.Error);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException) when (!session.IsActive)
                {
                }
                catch (Exception error)
                {
                    UIErrors.Report(error);
                }
            }
        }
    }
}
