using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>观察原生选择变化，防止异步条目准备结束后覆盖更新的用户焦点。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class NativeFocusObserver : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private static readonly ConditionalWeakTable<EventSystem, FocusState> states =
            new ConditionalWeakTable<EventSystem, FocusState>();

        public void OnSelect(BaseEventData eventData) => Observe(gameObject);

        public void OnDeselect(BaseEventData eventData) => Observe(null);

        private void Observe(GameObject selection)
        {
            var system = View.GetInputEventSystem(transform);
            if (system != null && states.TryGetValue(system, out var state) && state.InternalChanges == 0)
            {
                ++state.Revision;
                state.Selection = selection;
            }
        }

        internal static void Attach(GameObject target)
        {
            if (target != null && target.GetComponent<NativeFocusObserver>() == null)
            {
                target.AddComponent<NativeFocusObserver>();
            }
        }

        internal static Request Capture(EventSystem system)
        {
            if (system == null)
            {
                return null;
            }

            var state = states.GetValue(system, _ => new FocusState());
            state.Selection = system.currentSelectedGameObject;
            Attach(state.Selection);
            return new Request(system, state, state.Revision);
        }

        /// <summary>框架回收与默认焦点回退不视为用户的新选择；回调中的重入选择仍会使请求失效。</summary>
        internal static void SetFrameworkSelection(EventSystem system, GameObject selection)
        {
            if (system == null)
            {
                return;
            }

            if (!states.TryGetValue(system, out var state))
            {
                system.SetSelectedGameObject(selection);
                return;
            }

            ++state.InternalChanges;
            try
            {
                system.SetSelectedGameObject(selection);
            }
            finally
            {
                --state.InternalChanges;
                state.Selection = system.currentSelectedGameObject;
                if (state.Selection != selection)
                {
                    ++state.Revision;
                }
            }
        }

        internal sealed class Request
        {
            private readonly EventSystem system;
            private readonly FocusState state;
            private readonly long revision;

            internal Request(EventSystem system, FocusState state, long revision)
            {
                this.system = system;
                this.state = state;
                this.revision = revision;
            }

            internal bool IsCurrent(EventSystem current) =>
                system != null && current == system && revision == state.Revision &&
                system.currentSelectedGameObject == state.Selection;
        }

        internal sealed class FocusState
        {
            internal GameObject Selection;
            internal long Revision;
            internal int InternalChanges;
        }
    }
}
