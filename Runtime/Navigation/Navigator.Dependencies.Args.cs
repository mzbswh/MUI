using System;
using System.Collections.Generic;

namespace MUI.Navigation
{
    public sealed partial class Navigator
    {
        /// <summary>
        /// 在父参数更新器执行前确认依赖参数不变，并提供准备后的提交复核；不创建候选、不转移所有权。
        /// 参数工厂和比较器仍属于项目回调，必须在导航回调保护内执行。
        /// </summary>
        private ArgsUpdateRejection CheckRetainedDependencies<TViewModel, TArgs, TResult>(
            ViewInstance<TViewModel, TArgs, TResult> parent, Route<TViewModel, TArgs, TResult> route, TArgs args,
            out Action beforeCommit)
            where TViewModel : ViewModel
        {
            beforeCommit = null;
            if (route.Dependencies.Count == 0)
            {
                beforeCommit = () => RequireArgsOwnershipCurrent(parent);
                return ArgsUpdateRejection.None;
            }
            var expected = new List<KeyValuePair<DependencyRequest, ViewInstance>>();
            var dependencies = new Dictionary<Route, ViewInstance>();
            foreach (var handle in ownership.CaptureDependencies(parent.Handle))
            {
                if (!entries.TryGetValue(handle, out var dependency) || !IsRetainedDependencyCurrent(parent, dependency))
                {
                    return ArgsUpdateRejection.SourceUnavailable;
                }
                dependencies.Add(dependency.Route, dependency);
            }

            using (EnterCallback(parent))
            {
                foreach (var declaration in route.Dependencies)
                {
                    // 已降级的可选目标保持缺席，参数更新不隐式启动重试或恢复已撤销的关系。
                    if (!declaration.IsRequired && parent.DependencyFailures != null &&
                        parent.DependencyFailures.ContainsKey(declaration.Target) && !dependencies.ContainsKey(declaration.Target))
                    {
                        continue;
                    }
                    var request = declaration.Resolve(args);
                    if (!dependencies.TryGetValue(request.Route, out var dependency))
                    {
                        return ArgsUpdateRejection.DependencyChangeRequired;
                    }
                    // 参数工厂和比较器都可能触发取消；不可把失效对象继续交给后续项目回调。
                    if (!IsRetainedDependencyCurrent(parent, dependency))
                    {
                        return ArgsUpdateRejection.SourceUnavailable;
                    }
                    var matches = request.Matches(dependency);
                    if (!IsRetainedDependencyCurrent(parent, dependency))
                    {
                        return ArgsUpdateRejection.SourceUnavailable;
                    }
                    if (!matches)
                    {
                        return ArgsUpdateRejection.DependencyChangeRequired;
                    }
                    expected.Add(new KeyValuePair<DependencyRequest, ViewInstance>(request, dependency));
                }
            }

            // 比较器返回后只读直接状态，避免在提交前沿用已失效的实例快照。
            if (!CanMutateInstance(parent) || !entries.TryGetValue(parent.Handle, out var current) ||
                !ReferenceEquals(current, parent) || ownership.HasOwners(parent.Handle))
            {
                return ArgsUpdateRejection.SourceUnavailable;
            }
            foreach (var dependency in dependencies.Values)
            {
                if (!IsRetainedDependencyCurrent(parent, dependency))
                {
                    return ArgsUpdateRejection.SourceUnavailable;
                }
            }
            // 保留本次解析出的强类型请求；准备后复核不重复执行项目参数工厂。
            beforeCommit = () => RequireArgsDependenciesCurrent(parent, expected);
            return ArgsUpdateRejection.None;
        }

        private void RequireArgsOwnershipCurrent(ViewInstance parent)
        {
            AssertThread();
            if (!entries.TryGetValue(parent.Handle, out var current) || !ReferenceEquals(current, parent) ||
                !CanMutateInstance(parent) || ownership.HasOwners(parent.Handle))
            {
                throw new InvalidOperationException("参数更新准备期间，父页面或其共享拥有关系发生变化。");
            }
        }

        /// <summary>在父参数 Commit 前复核保留的依赖，允许已经正式记录的可选依赖降级。</summary>
        private void RequireArgsDependenciesCurrent(ViewInstance parent,
            List<KeyValuePair<DependencyRequest, ViewInstance>> expected)
        {
            RequireArgsOwnershipCurrent(parent);
            foreach (var item in expected)
            {
                if (IsArgsDependencyDegraded(parent, item.Key, item.Value))
                {
                    continue;
                }
                RequireArgsDependencyCurrent(parent, item.Value);
                if (!item.Key.Matches(item.Value))
                {
                    throw new InvalidOperationException("参数更新准备期间，共享依赖参数发生变化。");
                }
            }
            // 比较器属于项目代码；最后再做一遍无外部回调的资格复核。
            RequireArgsOwnershipCurrent(parent);
            foreach (var item in expected)
            {
                if (!IsArgsDependencyDegraded(parent, item.Key, item.Value))
                {
                    RequireArgsDependencyCurrent(parent, item.Value);
                }
            }
        }

        private bool IsArgsDependencyDegraded(ViewInstance parent, DependencyRequest request, ViewInstance dependency) =>
            !request.IsRequired && parent.DependencyFailures != null &&
            parent.DependencyFailures.ContainsKey(request.Route) &&
            !ownership.HasDependency(parent.Handle, dependency.Handle);

        private void RequireArgsDependencyCurrent(ViewInstance parent, ViewInstance dependency)
        {
            if (!IsRetainedDependencyCurrent(parent, dependency))
            {
                throw new InvalidOperationException("参数提交前，共享依赖已失效或不再属于父页面。");
            }
        }
    }
}
