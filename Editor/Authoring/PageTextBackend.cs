using System;
using System.Collections.Generic;
using MUI.UGUI;
using UnityEngine;
using UnityEngine.UI;

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
            Register("ugui", "uGUI Text", typeof(TextElement), () => null,
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
                throw new ArgumentException("文本模板需要稳定标识和显示名称。");
            }
            if (validate == null || create == null)
            {
                throw new ArgumentNullException(validate == null ? nameof(validate) : nameof(create));
            }
            RequireElementType(elementType);
            if (Find(id) != null)
            {
                throw new InvalidOperationException("文本模板标识已注册：" + id);
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
                throw new ArgumentException("文本 Element 必须是公开、非嵌套、非泛型的具体类型，并有可读写的 string Content 属性。");
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
                throw new InvalidOperationException("文本模板没有返回正确挂载和命名的控件。");
            }
            return graphic;
        }
    }
}
