using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;
using L = MUI.Editor.Localization.MUIEditorLocalization;

namespace MUI.Editor
{
    /// <summary>页面向导的文本模板扩展，由可选编辑器模块显式注册，不扫描运行时程序集。</summary>
    public sealed class PageTextBackend
    {
        private static readonly List<PageTextBackend> registered = new List<PageTextBackend>();
        private readonly Func<string> validate;
        private readonly Func<string, Transform, string, Graphic> create;

        static PageTextBackend()
        {
            Register("ugui", L.Get("editor.PageTextBackend.19baaca2f4"), typeof(TextElement), () => null,
                (name, parent, content) => PrefabAuthoring.CreateText(name, parent, content));
        }

        private PageTextBackend(string id, string label, Type elementType, Func<string> validate,
                    Func<string, Transform, string, Graphic> create)
        {
            Id = id;
            Label = label;
            ElementType = elementType;
            this.validate = validate;
            this.create = create;
        }

        public string Id
        {
            get;
        }

        public string Label
        {
            get;
        }

        public Type ElementType
        {
            get;
        }

        /// <summary>同一标识只允许注册一次；工厂创建非射线目标文本，生命周期由向导拥有。</summary>
        public static void Register(string id, string label, Type elementType, Func<string> validate,
            Func<string, Transform, string, Graphic> create)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(L.Get("editor.PageTextBackend.cff54d9fa2"));
            }
            if (validate == null || create == null)
            {
                throw new ArgumentNullException(validate == null ? nameof(validate) : nameof(create));
            }
            RequireElementType(elementType);
            if (Find(id) != null)
            {
                throw new InvalidOperationException(L.Get("editor.PageTextBackend.ce0434eb31") + id);
            }
            registered.Add(new PageTextBackend(id, label, elementType, validate, create));
        }

        internal static void RequireElementType(Type type)
        {
            var content = type == null ? null : type.GetProperty("Content");
            if (type == null || !type.IsVisible || type.IsAbstract || type.IsGenericType || type.IsNested ||
                !typeof(Element).IsAssignableFrom(type) || content == null || content.PropertyType != typeof(string) ||
                content.GetGetMethod() == null || content.GetSetMethod() == null ||
                content.GetGetMethod().IsStatic || content.GetSetMethod().IsStatic || content.GetIndexParameters().Length != 0)
            {
                throw new ArgumentException(L.Get("editor.PageTextBackend.36e1635db2"));
            }
        }

        internal static PageTextBackend Find(string id) => registered.Find(item => item.Id == id);

        internal static PageTextBackend[] Capture() => registered.ToArray();

        internal string Validate() => validate();

        internal Graphic CreateText(string name, Transform parent, string content)
        {
            var graphic = create(name, parent, content);
            if (graphic == null || graphic.transform.parent != parent || graphic.name != name)
            {
                throw new InvalidOperationException(L.Get("editor.PageTextBackend.865c6e02de"));
            }
            return graphic;
        }
    }
}
