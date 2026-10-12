using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using UnityEngine.UI;

using MUI.UGUI;
using TMPro;

namespace MUI.TMP
{
    public sealed class NativeTMPInputField : NativeTextInput<TMP_InputField>
    {
        public NativeTMPInputField(string name, TMP_InputField target) : base(name, target, target.onValueChanged, target.onEndEdit) { }
        public override string Value { get => Component.text; set { value = value ?? ""; if (Value == value) return; Component.SetTextWithoutNotify(value); Changed(); } }
        public override bool ReadOnly { get => Component.readOnly; set { Component.readOnly = value; Changed(); } }
    }

    public sealed class NativeTMPDropdown : NativeControl<TMP_Dropdown>, IDropdownElement
    {
        public NativeTMPDropdown(string name, TMP_Dropdown target) : base(name, target)
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
                Component.options = value == null ? new List<TMP_Dropdown.OptionData>() : value.Select(text => new TMP_Dropdown.OptionData(text ?? "")).ToList();
                Component.RefreshShownValue();
                Changed(); Changed(nameof(Value));
            }
        }
    }
}
