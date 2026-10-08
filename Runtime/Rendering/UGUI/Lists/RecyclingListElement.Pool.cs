using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MUI.UGUI
{
    public partial class RecyclingListElement
    {
        /// <summary>
        /// 同步与异步执行器共用的协调过程。产出条目表示其本轮换绑必须完成后才能显示。
        /// 复用按位置进行，删除后的节点先隐藏再完成解绑后留池，原生布局只计算活动节点。
        /// </summary>
        private IEnumerable<Task> Reconcile(LifetimeScope activation, CancellationToken token)
        {
            var remaining = (long)capacity + 8;
            while (IsActivationCurrent(activation) && dirty)
            {
                token.ThrowIfCancellationRequested();
                if (--remaining < 0)
                {
                    throw new InvalidOperationException("回收列表刷新回调持续修改来源，未能在有界次数内稳定。");
                }

                dirty = false;
                var currentRevision = revision;
                var desired = snapshot;
                // 先回收多余项；必须等待解绑结束，后续刷新才能复用这些节点。
                for (var index = cells.Count - 1; index >= desired.Count; --index)
                {
                    var cell = RequireCell(index);
                    cell.gameObject.SetActive(false);
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    cell.ViewModel = null;
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    yield return ((IChildViewElement)cell).Preparation;
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }
                }

                for (var index = 0; index < desired.Count && IsCurrent(activation, currentRevision); ++index)
                {
                    var cell = index < cells.Count ? RequireCell(index) : CreateCell(activation, currentRevision);
                    if (cell == null || !IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    if (!ReferenceEquals(cell.DisplayedViewModel, desired[index]))
                    {
                        cell.gameObject.SetActive(false);
                        if (!IsCurrent(activation, currentRevision))
                        {
                            break;
                        }
                    }

                    cell.ViewModel = desired[index];
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    yield return ((IChildViewElement)cell).Preparation;
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    if (!UsesFixedSlots)
                    {
                        cell.transform.SetSiblingIndex(index);
                    }
                    if (!IsCurrent(activation, currentRevision))
                    {
                        break;
                    }

                    cell.gameObject.SetActive(true);
                }

                if (IsActivationCurrent(activation) && Error != null)
                {
                    ExceptionDispatchInfo.Capture(Error).Throw();
                }
            }

            token.ThrowIfCancellationRequested();
        }

        private bool IsActivationCurrent(LifetimeScope activation) =>
            IsAlive && ReferenceEquals(lifetime, activation) && !activation.IsEnded && scope != null && scope.IsActive;

        private bool IsCurrent(LifetimeScope activation, long currentRevision) =>
            IsActivationCurrent(activation) && revision == currentRevision && Error == null;

        private NestedViewElement RequireCell(int index)
        {
            var cell = cells[index];
            var expectedParent = UsesFixedSlots && cell != null && fixedCellParents.TryGetValue(cell, out var parent)
                ? parent : content;
            if (cell == null || !cell.IsAlive || expectedParent == null || cell.transform.parent != expectedParent)
            {
                throw new InvalidOperationException("回收列表的池节点已被外部销毁或移动。");
            }

            return cell;
        }

        private NestedViewElement CreateCell(LifetimeScope activation, long currentRevision)
        {
            if (UsesFixedSlots)
            {
                throw new InvalidOperationException("Fixed slots cannot create additional list cells.");
            }
            if (itemTemplate == null || content == null || itemTemplate.gameObject.activeSelf)
            {
                throw new InvalidOperationException("回收列表的内容节点或非激活模板已失效。");
            }

            var cell = Instantiate(itemTemplate, content, false);
            var accepted = false;
            try
            {
                if (!IsCurrent(activation, currentRevision))
                {
                    return null;
                }

                cell.Initialize();
                if (!IsCurrent(activation, currentRevision))
                {
                    return null;
                }

                ((IChildViewElement)cell).BeginParentActivation(scope, activation);
                if (!IsCurrent(activation, currentRevision))
                {
                    return null;
                }

                cells.Add(cell);
                accepted = true;
                return cell;
            }
            finally
            {
                if (!accepted)
                {
                    ReleaseCell(cell);
                }
            }
        }

        private void ReleaseCells()
        {
            ++itemsAssignmentVersion;
            ++revision;
            scope = null;
            lifetime = null;
            snapshot.Clear();
            pending = null;
            Error = null;
            running = false;
            dirty = false;
            var failures = new List<Exception>();
            try
            {
                DetachSource();
            }
            catch (Exception error)
            {
                // 来源退订失败仍必须继续销毁每个池节点。
                failures.Add(error);
            }

            foreach (var cell in cells)
            {
                try
                {
                    ReleaseCell(cell);
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }

            cells.Clear();
            fixedCellParents.Clear();
            ownedFixedCells.Clear();
            if (failures.Count != 0)
            {
                throw new AggregateException("回收列表节点清理失败。", failures);
            }
        }

        private void ReleaseCell(NestedViewElement cell)
        {
            if (cell == null)
            {
                return;
            }

            try
            {
                cell.Dispose();
            }
            finally
            {
                if (cell != null && (!UsesFixedSlots || ownedFixedCells.Remove(cell)))
                {
                    Destroy(cell.gameObject);
                }
            }
        }
    }
}
