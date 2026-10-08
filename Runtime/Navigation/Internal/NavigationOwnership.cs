using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    /// <summary>
    /// 导航实例的所有权账本。只维护关系，不执行界面回调、加载或关闭。
    /// 所有操作由 Navigator 在所属 UI 线程调用，节点随实例登记和移除。
    /// </summary>
    internal sealed partial class NavigationOwnership
    {
        private readonly Dictionary<ViewHandle, Node> nodes = new Dictionary<ViewHandle, Node>();
        private readonly int dependenciesPerOwner;
        private int edgeCount;

        internal NavigationOwnership(int dependenciesPerOwner = 64)
        {
            if (dependenciesPerOwner < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dependenciesPerOwner));
            }
            this.dependenciesPerOwner = dependenciesPerOwner;
        }

        internal bool HasDependencyEdges => edgeCount != 0;

        internal void Register(ViewHandle handle, bool explicitOwner, int layer)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("所有权节点必须对应有效的实例句柄。", nameof(handle));
            }
            nodes.Add(handle, new Node { Explicit = explicitOwner, Layer = layer });
        }

        internal ViewHandle[] CaptureDependencies(ViewHandle handle, DependencyPlacement? placement = null)
        {
            var dependencies = Get(handle).Dependencies;
            if (!placement.HasValue)
            {
                return dependencies.Count == 0 ? Array.Empty<ViewHandle>() : dependencies.ToArray();
            }
            return dependencies.FindAll(dependency => Get(handle).Placements[dependency] == placement.Value).ToArray();
        }

        internal bool HasDependencies(ViewHandle handle) => Get(handle).Dependencies.Count != 0;

        internal bool HasDependency(ViewHandle owner, ViewHandle dependency) => Get(owner).Placements.ContainsKey(dependency);

        internal bool IsRequiredDependency(ViewHandle owner, ViewHandle dependency) =>
                    Get(owner).Placements.ContainsKey(dependency) && !Get(owner).OptionalDependencies.Contains(dependency);

        internal int OwnerCount(ViewHandle handle) => Get(handle).Owners.Count;

        internal bool HasOwners(ViewHandle handle) => Get(handle).Owners.Count != 0;

        internal bool HasExplicitOwner(ViewHandle handle) => Get(handle).Explicit;

        internal void AcquireExplicit(ViewHandle handle) => Get(handle).Explicit = true;

        /// <summary>撤销独立打开关系，返回是否已经没有任何拥有者；不冒充界面关闭。</summary>
        internal bool ReleaseExplicit(ViewHandle handle)
        {
            var node = Get(handle);
            node.Explicit = false;
            return node.Owners.Count == 0;
        }

        /// <summary>添加单条依赖；重复取得幂等，成环或超过容量时不改变原有关系。</summary>
        internal bool Acquire(ViewHandle owner, ViewHandle dependency, DependencyPlacement placement, bool isRequired = true)
        {
            if (!ValidateAcquisition(owner, dependency, placement, isRequired))
            {
                return false;
            }
            Get(owner).Dependencies.Add(dependency);
            Get(owner).Placements.Add(dependency, placement);
            Get(dependency).Owners.Add(owner);
            if (!isRequired)
            {
                Get(owner).OptionalDependencies.Add(dependency);
            }
            ++edgeCount;
            return true;
        }

        /// <summary>只校验，不先添加关系，便于在参数比较与候选复用前发现环。</summary>
        internal bool ValidateAcquisition(ViewHandle owner, ViewHandle dependency, DependencyPlacement placement, bool isRequired = true)
        {
            var parent = Get(owner);
            var child = Get(dependency);
            if (child.Owners.Contains(owner))
            {
                if (parent.OptionalDependencies.Contains(dependency) == isRequired)
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.ConflictingData,
                        "同一父页面不能以不同降级策略重复取得同一依赖。");
                }
                if (parent.Placements[dependency] != placement)
                {
                    throw new NavigationPreparationRejectedException(OpenRejection.DependencyOrderConflict,
                        "同一父页面对同一共享依赖声明了不同顺序。");
                }
                return false;
            }
            ValidateNewDependency(owner);
            if (owner == dependency || Reaches(dependency, owner))
            {
                throw new NavigationPreparationRejectedException(OpenRejection.DependencyCycle, "页面依赖形成循环。");
            }
            if (parent.Layer == child.Layer && CreatesPresentationCycle(owner, dependency, placement))
            {
                throw new NavigationPreparationRejectedException(OpenRejection.DependencyOrderConflict,
                    "共享依赖的同层显示顺序形成循环。");
            }
            return true;
        }

        /// <summary>
        /// 新实例尚无入边或出边，不可能在首次取得时形成环；在创建候选前检查父容量，
        /// 避免候选已登记，却因无法取得所有权而脱离父页面的失败回收链。
        /// 已有实例的取得仍须使用完整校验，重复取得也不消耗容量。
        /// </summary>
        internal void ValidateNewDependency(ViewHandle owner)
        {
            if (Get(owner).Dependencies.Count >= dependenciesPerOwner)
            {
                throw new NavigationPreparationRejectedException(OpenRejection.DependencyLimit, "页面依赖数量超过所有权容量。");
            }
        }

        /// <summary>
        /// 只撤销指定父页面取得的一条关系；不存在时返回 false，不改变其他关系。
        /// unowned 仅在成功撤销后表示目标失去全部拥有者；调用方据此决定是否回收实例。
        /// 此方法不执行关闭或项目回调，便于同步与异步事务分别负责实际清理。
        /// </summary>
        internal bool ReleaseDependency(ViewHandle owner, ViewHandle dependency, out bool unowned)
        {
            unowned = false;
            var parent = Get(owner);
            var index = parent.Dependencies.IndexOf(dependency);
            if (index < 0)
            {
                return false;
            }
            var child = Get(dependency);
            // 修改前验证双向关系；损坏的账本不能继续以成功释放掩盖问题。
            if (!child.Owners.Contains(owner) || !parent.Placements.ContainsKey(dependency) || edgeCount <= 0)
            {
                throw new InvalidOperationException("依赖所有权的双向记录不一致，无法安全撤销关系。");
            }
            parent.Dependencies.RemoveAt(index);
            parent.Placements.Remove(dependency);
            parent.OptionalDependencies.Remove(dependency);
            child.Owners.Remove(owner);
            --edgeCount;
            unowned = !child.Explicit && child.Owners.Count == 0;
            return true;
        }

        /// <summary>按取得顺序逆序撤销父关系，返回失去最后一个拥有者的依赖。</summary>
        internal ViewHandle[] ReleaseDependencies(ViewHandle owner)
        {
            var parent = Get(owner);
            if (parent.Dependencies.Count == 0)
            {
                return Array.Empty<ViewHandle>();
            }
            var unowned = new List<ViewHandle>();
            for (var i = parent.Dependencies.Count - 1; i >= 0; --i)
            {
                var handle = parent.Dependencies[i];
                if (ReleaseDependency(owner, handle, out var becameUnowned) && becameUnowned)
                {
                    unowned.Add(handle);
                }
            }
            return unowned.ToArray();
        }

        /// <summary>取得拥有者快照，供强制关闭前终结父页面使用。</summary>
        internal ViewHandle[] CaptureOwners(ViewHandle handle, int maxCount = int.MaxValue)
        {
            var owners = Get(handle).Owners;
            if (maxCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }
            var result = new ViewHandle[Math.Min(owners.Count, maxCount)];
            var index = 0;
            foreach (var owner in owners)
            {
                if (index == result.Length)
                {
                    break;
                }
                result[index++] = owner;
            }
            return result;
        }

        /// <summary>移除终态节点；仍有关系表示调用方遗漏了释放，禁止静默切断。</summary>
        internal void Remove(ViewHandle handle)
        {
            var node = Get(handle);
            if (node.Owners.Count != 0 || node.Dependencies.Count != 0)
            {
                throw new InvalidOperationException("实例终结前必须释放全部依赖与拥有关系。");
            }
            nodes.Remove(handle);
        }

        /// <summary>
        /// 父页面先于批次内依赖；没有依赖约束时沿用输入的层级和显示优先级。
        /// 批次外的父页面不加入本次关闭，依赖是否可关闭仍由调用方检查最新所有权。
        /// </summary>
        internal ViewInstance[] OrderCloseBatch(ViewInstance[] candidates) => OrderCandidates(candidates, false);

        internal ViewInstance[] OrderPresentation(ViewInstance[] candidates) => OrderCandidates(candidates, true);

        private ViewInstance[] OrderCandidates(ViewInstance[] candidates, bool presentation)
        {
            if (edgeCount == 0 || candidates.Length < 2)
            {
                return candidates;
            }
            var indices = new Dictionary<ViewHandle, int>();
            for (var i = 0; i < candidates.Length; ++i)
            {
                indices.Add(candidates[i].Handle, i);
            }
            var incoming = new int[candidates.Length];
            for (var i = 0; i < candidates.Length; ++i)
            {
                foreach (var dependency in GetSuccessors(candidates[i].Handle, presentation))
                {
                    if (indices.TryGetValue(dependency, out var target))
                    {
                        ++incoming[target];
                    }
                }
            }
            var ready = new SortedSet<int>();
            for (var i = 0; i < incoming.Length; ++i)
            {
                if (incoming[i] == 0)
                {
                    ready.Add(i);
                }
            }
            var result = new ViewInstance[candidates.Length];
            var written = 0;
            while (ready.Count > 0)
            {
                var index = ready.Min;
                ready.Remove(index);
                var instance = candidates[index];
                result[written++] = instance;
                foreach (var dependency in GetSuccessors(instance.Handle, presentation))
                {
                    if (indices.TryGetValue(dependency, out var target) && --incoming[target] == 0)
                    {
                        ready.Add(target);
                    }
                }
            }
            if (written != result.Length)
            {
                throw new InvalidOperationException(presentation
                    ? "依赖显示约束存在循环，无法确定显示顺序。"
                    : "所有权账本存在循环，无法确定安全的关闭顺序。");
            }
            return result;
        }

        private bool Reaches(ViewHandle start, ViewHandle target)
        {
            var visited = new HashSet<ViewHandle>();
            var pending = new Stack<ViewHandle>();
            pending.Push(start);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current == target)
                {
                    return true;
                }
                if (!visited.Add(current))
                {
                    continue;
                }
                foreach (var dependency in Get(current).Dependencies)
                {
                    pending.Push(dependency);
                }
            }
            return false;
        }

        private Node Get(ViewHandle handle)
        {
            if (!nodes.TryGetValue(handle, out var node))
            {
                throw new InvalidOperationException("所有权句柄不属于当前活动实例账本。");
            }
            return node;
        }

        private sealed class Node
        {
            internal bool Explicit;
            internal int Layer;
            internal readonly Dictionary<ViewHandle, DependencyPlacement> Placements = new Dictionary<ViewHandle, DependencyPlacement>();
            internal readonly HashSet<ViewHandle> OptionalDependencies = new HashSet<ViewHandle>();
            internal readonly HashSet<ViewHandle> Owners = new HashSet<ViewHandle>();
            internal readonly List<ViewHandle> Dependencies = new List<ViewHandle>();
        }
    }
}
