using System.Collections.Generic;

namespace MUI.Navigation
{
    internal sealed partial class NavigationOwnership
    {
        /// <summary>Layer 为硬边界，依赖只约束同一层内的相对显示顺序。</summary>
        private IEnumerable<ViewHandle> GetSuccessors(ViewHandle handle, bool presentation)
        {
            var node = Get(handle);
            foreach (var dependency in node.Dependencies)
            {
                if (!presentation || (node.Layer == Get(dependency).Layer &&
                    node.Placements[dependency] == DependencyPlacement.AttachedAfter))
                {
                    yield return dependency;
                }
            }
            if (presentation)
            {
                foreach (var owner in node.Owners)
                {
                    var parent = Get(owner);
                    if (parent.Layer == node.Layer && parent.Placements[handle] == DependencyPlacement.RequiredBefore)
                    {
                        yield return owner;
                    }
                }
            }
        }

        private bool CreatesPresentationCycle(ViewHandle owner, ViewHandle dependency, DependencyPlacement placement)
        {
            var start = placement == DependencyPlacement.RequiredBefore ? owner : dependency;
            var target = placement == DependencyPlacement.RequiredBefore ? dependency : owner;
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
                foreach (var successor in GetSuccessors(current, true))
                {
                    pending.Push(successor);
                }
            }
            return false;
        }
    }
}
