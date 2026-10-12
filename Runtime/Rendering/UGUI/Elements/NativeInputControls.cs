using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MUI.UGUI
{
    public abstract class NativeTextInput<T> : NativeControl<T>, IInputFieldElement where T : Selectable
    {
        private TextInputCommitMode commitMode;
        protected NativeTextInput(string name, T target, UnityEvent<string> changed, UnityEvent<string> ended) : base(name, target)
        {
            Listen(changed, _ => { if (commitMode == TextInputCommitMode.OnChange) Changed(nameof(Value)); });
            Listen(ended, _ => { Changed(nameof(Value)); if (CanReceiveInput()) EditingEnded?.Invoke(); });
            TrackCleanup(() => EditingEnded = null);
        }
        public abstract string Value { get; set; }
        public abstract bool ReadOnly { get; set; }
        public bool Interactable { get => Component.interactable; set { if (Component.interactable == value) return; Component.interactable = value; Changed(); } }
        public event Action EditingEnded;
        public TextInputCommitMode CommitMode
        {
            get => commitMode;
            set
            {
                if (!Enum.IsDefined(typeof(TextInputCommitMode), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (commitMode == value) return;
                commitMode = value;
                Changed();
            }
        }
    }

    public sealed class NativeInputField : NativeTextInput<InputField>
    {
        public NativeInputField(string name, InputField target) : base(name, target, target.onValueChanged, target.onEndEdit) { }
        public override string Value { get => Component.text; set { value = value ?? ""; if (Value == value) return; Component.SetTextWithoutNotify(value); Changed(); } }
        public override bool ReadOnly { get => Component.readOnly; set { Component.readOnly = value; Changed(); } }
    }

    public sealed class NativeDropdown : NativeControl<Dropdown>, IDropdownElement
    {
        public NativeDropdown(string name, Dropdown target) : base(name, target)
        {
            Listen(target.onValueChanged, _ => { Changed(nameof(Value)); if (CanReceiveInput()) SelectionChanged?.Invoke(); });
            TrackCleanup(() => SelectionChanged = null);
        }
        public event Action SelectionChanged;
        public int Value { get => Component.value; set { if (Value == value) return; Component.SetValueWithoutNotify(value); Changed(); } }
        public bool Interactable { get => Component.interactable; set { Component.interactable = value; Changed(); } }
        public IReadOnlyList<string> Options
        {
            get => Component.options.Select(option => option.text).ToArray();
            set
            {
                Component.options = value == null ? new List<Dropdown.OptionData>() : value.Select(text => new Dropdown.OptionData(text ?? "")).ToList();
                Component.RefreshShownValue();
                Changed(); Changed(nameof(Value));
            }
        }
    }
}
