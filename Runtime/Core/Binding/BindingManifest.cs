using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MUI
{
    public enum BindingEntryKind
    {
        Property,
        Command
    }

    public sealed class BindingEntry
    {
        public BindingEntry(string source, string elementName, Type elementType, string targetProperty, BindingMode mode, BindingEntryKind kind = BindingEntryKind.Property, string interactableProperty = "Interactable")
                    : this(source, elementName, elementType, targetProperty, mode, kind, interactableProperty, null, 0)
        {
        }

        /// <summary>附带声明位置的编辑器清单条目；旧清单仍可使用不含位置的构造函数。</summary>
        public BindingEntry(string source, string elementName, Type elementType, string targetProperty, BindingMode mode,
            BindingEntryKind kind, string interactableProperty, string sourcePath, int sourceLine)
        {
            if (sourceLine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceLine));
            }

            Source = source ?? throw new ArgumentNullException(nameof(source));
            ElementName = elementName ?? throw new ArgumentNullException(nameof(elementName));
            ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
            TargetProperty = targetProperty ?? throw new ArgumentNullException(nameof(targetProperty));
            Mode = mode;
            Kind = kind;
            InteractableProperty = interactableProperty ?? throw new ArgumentNullException(nameof(interactableProperty));
            SourcePath = sourcePath;
            SourceLine = sourceLine;
        }

        public string Source
        {
            get;
        }

        public string ElementName
        {
            get;
        }

        public Type ElementType
        {
            get;
        }

        public string TargetProperty
        {
            get;
        }

        public BindingMode Mode
        {
            get;
        }

        public BindingEntryKind Kind
        {
            get;
        }

        public string InteractableProperty
        {
            get;
        }

        /// <summary>绑定声明的源码路径；生成器仅在 UNITY_EDITOR 编译中填入。</summary>
        public string SourcePath
        {
            get;
        }

        /// <summary>源码中的一基行号；零表示没有位置元数据。</summary>
        public int SourceLine
        {
            get;
        }
    }

    /// <summary>代码生成的契约，由运行时诊断和 Prefab 校验共用。</summary>
    public sealed class BindingManifest
    {
        public BindingManifest(Type viewModelType, IEnumerable<BindingEntry> entries)
        {
            ViewModelType = viewModelType ?? throw new ArgumentNullException(nameof(viewModelType));
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var copy = new List<BindingEntry>();
            foreach (var entry in entries)
            {
                copy.Add(entry ?? throw new ArgumentException("Null binding entry.", nameof(entries)));
            }

            Entries = new ReadOnlyCollection<BindingEntry>(copy);
        }

        public Type ViewModelType
        {
            get;
        }

        public IReadOnlyList<BindingEntry> Entries
        {
            get;
        }
    }
}
