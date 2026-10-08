using System;
using System.Threading;
using UnityEngine;

namespace MUI.UGUI
{
    /// <summary>
    /// 拥有截图纹理的本地快照。纹理借用、编码和释放须在创建线程执行。
    /// 不持有 View；元数据与像素相邻采集，不保证业务状态和渲染提交的原子一致性。
    /// </summary>
    public sealed class UIAutomationSnapshot : IDisposable
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private Texture2D texture;

        internal UIAutomationSnapshot(Texture2D texture, ViewInputSnapshot input, int frame, DateTimeOffset capturedAt)
        {
            this.texture = texture;
            Input = input;
            Frame = frame;
            CapturedAt = capturedAt;
            Width = texture.width;
            Height = texture.height;
        }

        public ViewInputSnapshot Input
        {
            get;
        }

        public int Frame
        {
            get;
        }

        public DateTimeOffset CapturedAt
        {
            get;
        }

        public int Width
        {
            get;
        }

        public int Height
        {
            get;
        }

        /// <summary>借用只读用途的纹理；不得自行销毁或转移所有权，快照释放后不得继续使用。</summary>
        public Texture2D Texture
        {
            get
            {
                RequireThread();
                if (texture == null)
                {
                    throw new ObjectDisposedException(nameof(UIAutomationSnapshot));
                }
                return texture;
            }
        }

        /// <summary>
        /// 显式同步编码，返回调用方拥有的字节数组；不缓存、不写文件、不上传。
        /// 编码有额外 CPU 与内存成本，不应每帧调用。
        /// </summary>
        public byte[] EncodePng()
        {
            var bytes = ImageConversion.EncodeToPNG(Texture);
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException("Unity 未能将截图编码为 PNG。");
            }
            return bytes;
        }

        /// <summary>幂等释放纹理引用并请求原生销毁；Unity 原生销毁仍遵循帧末时序。</summary>
        public void Dispose()
        {
            RequireThread();
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
            texture = null;
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
            {
                throw new InvalidOperationException("截图纹理只能在创建它的 Unity 主线程操作。");
            }
        }
    }
}
