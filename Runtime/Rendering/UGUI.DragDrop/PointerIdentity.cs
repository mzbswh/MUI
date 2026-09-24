using System;
using System.Runtime.CompilerServices;
using UnityEngine.EventSystems;

namespace MUI.UGUI
{
    /// <summary>一次指针按钮流的身份，不保存会被输入模块复用的可变事件字段。</summary>
    internal readonly struct PointerIdentity : IEquatable<PointerIdentity>
    {
        internal PointerIdentity(BaseInputModule module, int id, PointerEventData.InputButton button)
        {
            Module = module;
            Id = id;
            Button = button;
        }

        internal BaseInputModule Module
        {
            get;
        }

        internal int Id
        {
            get;
        }

        internal PointerEventData.InputButton Button
        {
            get;
        }

        internal bool IsCurrentModule
        {
            get
            {
                if (Module == null || !Module.isActiveAndEnabled)
                {
                    return false;
                }

                var system = Module.GetComponent<EventSystem>();
                return system != null && system.currentInputModule == Module;
            }
        }

        internal PointerIdentity ForButton(PointerEventData.InputButton button)
        {
            // 旧输入模块为鼠标三个键分配 -1/-2/-3；新模块同一鼠标共用指针 ID。
            var id = Module is PointerInputModule && Id >= -3 && Id <= -1
                ? -1 - (int)button
                : Id;
            return new PointerIdentity(Module, id, button);
        }

        internal static PointerIdentity From(PointerEventData data)
        {
            return new PointerIdentity(data.currentInputModule, data.pointerId, data.button);
        }

        public bool Equals(PointerIdentity other)
        {
            // 身份比较不使用 Unity 的销毁后等于 null 语义。
            return ReferenceEquals(Module, other.Module) && Id == other.Id && Button == other.Button;
        }

        public override bool Equals(object obj) => obj is PointerIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var moduleHash = ReferenceEquals(Module, null) ? 0 : RuntimeHelpers.GetHashCode(Module);
                return ((moduleHash * 397) ^ Id) * 397 ^ (int)Button;
            }
        }
    }
}
