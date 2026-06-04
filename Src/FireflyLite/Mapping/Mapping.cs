using System;
using System.Collections.Generic;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.MetaProgramming;

namespace Firefly.Mapping
{
    public interface IProjectorResolver
    {
        /// <param name="TypePair">(DomainType, RangeType)</param>
        /// <returns>返回Func(Of ${DomainType}, ${RangeType})</returns>
        Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair);
    }

    public interface IAggregatorResolver
    {
        /// <returns>返回Action(Of ${DomainType}, ${RangeType})</returns>
        Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair);
    }

    public interface IMapperResolver : IProjectorResolver, IAggregatorResolver
    {
    }

    [DebuggerNonUserCode()]
    public static class Mapping
    {
        public static Delegate ResolveProjector(this IProjectorResolver This, KeyValuePair<Type, Type> TypePair)
        {
            var Resolved = This.TryResolveProjector(TypePair);
            if (Resolved == null) throw new NotSupportedException("NotResolved: Projector({0}, {1})".Formats(TypePair.Key.FullName, TypePair.Value.FullName));
            return Resolved;
        }
        public static Delegate ResolveAggregator(this IAggregatorResolver This, KeyValuePair<Type, Type> TypePair)
        {
            var Resolved = This.TryResolveAggregator(TypePair);
            if (Resolved == null) throw new NotSupportedException("NotResolved: Aggregator({0}, {1})".Formats(TypePair.Key.FullName, TypePair.Value.FullName));
            return Resolved;
        }

        public static Func<D, R> ResolveProjector<D, R>(this IProjectorResolver This)
        {
            return (Func<D, R>)This.ResolveProjector(new KeyValuePair<Type, Type>(typeof(D), typeof(R)));
        }
        public static Action<D, R> ResolveAggregator<D, R>(this IAggregatorResolver This)
        {
            return (Action<D, R>)This.ResolveAggregator(new KeyValuePair<Type, Type>(typeof(D), typeof(R)));
        }

        public static R Project<D, R>(this IProjectorResolver This, D Key)
        {
            return This.ResolveProjector<D, R>()(Key);
        }
        public static void Aggregate<D, R>(this IAggregatorResolver This, D Key, R Value)
        {
            This.ResolveAggregator<D, R>()(Key, Value);
        }

        public static IMapperResolver CreateMapper(IProjectorResolver ProjectorResolver, IAggregatorResolver AggregatorResolver)
        {
            return new MapperResolver(ProjectorResolver, AggregatorResolver);
        }
        private static EmptyProjectorResolverClass EmptyProjectorResolverInstance = new EmptyProjectorResolverClass();
        public static IProjectorResolver EmptyProjectorResolver
        {
            get { return EmptyProjectorResolverInstance; }
        }
        private static EmptyAggregatorResolverClass EmptyAggregatorResolverInstance = new EmptyAggregatorResolverClass();
        public static IAggregatorResolver EmptyAggregatorResolver
        {
            get { return EmptyAggregatorResolverInstance; }
        }

        /// <remarks>获取不循环解析器，用于在出现循环引用时抛出异常。</remarks>
        public static IProjectorResolver AsNoncircular(this IProjectorResolver This)
        {
            return new NoncircularProjectorResolver(This);
        }
        /// <remarks>获取不循环解析器，用于在出现循环引用时抛出异常。</remarks>
        public static IAggregatorResolver AsNoncircular(this IAggregatorResolver This)
        {
            return new NoncircularAggregatorResolver(This);
        }
        /// <remarks>获取不循环解析器，用于在出现循环引用时抛出异常。</remarks>
        public static IMapperResolver AsNoncircular(this IMapperResolver This)
        {
            return new MapperResolver(new NoncircularProjectorResolver(This), new NoncircularAggregatorResolver(This));
        }

        /// <remarks>获取运行时解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IProjectorResolver AsRuntimeProjectorResolver(this IProjectorResolver This)
        {
            return new RuntimeProjectorResolver(This);
        }
        /// <remarks>获取运行时解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IAggregatorResolver AsRuntimeAggregatorResolver(this IAggregatorResolver This)
        {
            return new RuntimeAggregatorResolver(This);
        }
        /// <remarks>获取运行时解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IMapperResolver AsRuntime(this IMapperResolver This)
        {
            return CreateMapper(This.AsRuntimeProjectorResolver(), This.AsRuntimeAggregatorResolver());
        }
        /// <remarks>获取运行时定义域非循环解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IProjectorResolver AsRuntimeDomainNoncircularProjectorResolver(this IProjectorResolver This)
        {
            return new RuntimeDomainNoncircularProjectorResolver(This);
        }
        /// <remarks>获取运行时定义域非循环解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IAggregatorResolver AsRuntimeDomainNoncircularAggregatorResolver(this IAggregatorResolver This)
        {
            return new RuntimeDomainNoncircularAggregatorResolver(This);
        }
        /// <remarks>获取运行时定义域非循环解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IMapperResolver AsRuntimeDomainNoncircular(this IMapperResolver This)
        {
            return CreateMapper(This.AsRuntimeDomainNoncircularProjectorResolver(), This.AsRuntimeDomainNoncircularAggregatorResolver());
        }
        /// <remarks>获取运行时值域非循环解析器，用于在出现循环类型引用时延迟到运行时解析。</remarks>
        public static IAggregatorResolver AsRuntimeRangeNoncircularAggregatorResolver(this IAggregatorResolver This)
        {
            return new RuntimeRangeNoncircularAggregatorResolver(This);
        }

        /// <remarks>获取缓存解析器。</remarks>
        public static IProjectorResolver AsCached(this IProjectorResolver This)
        {
            return new CachedProjectorResolver(This);
        }
        /// <remarks>获取缓存解析器。</remarks>
        public static IAggregatorResolver AsCached(this IAggregatorResolver This)
        {
            return new CachedAggregatorResolver(This);
        }
        /// <remarks>获取缓存解析器。</remarks>
        public static IMapperResolver AsCached(this IMapperResolver This)
        {
            return new MapperResolver(new CachedProjectorResolver(This), new CachedAggregatorResolver(This));
        }

        /// <remarks>获取连接解析器。</remarks>
        public static IProjectorResolver Concatenated(this IEnumerable<IProjectorResolver> This)
        {
            return new ConcatenatedProjectorResolver(This);
        }
        /// <remarks>获取连接解析器。</remarks>
        public static IAggregatorResolver Concatenated(this IEnumerable<IAggregatorResolver> This)
        {
            return new ConcatenatedAggregatorResolver(This);
        }
        /// <remarks>获取连接解析器。</remarks>
        public static IMapperResolver Concatenated(this IEnumerable<IMapperResolver> This)
        {
            return new MapperResolver(new ConcatenatedProjectorResolver(This), new ConcatenatedAggregatorResolver(This));
        }

        [DebuggerNonUserCode()]
        private class EmptyProjectorResolverClass : IProjectorResolver
        {
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                return null;
            }
        }
        [DebuggerNonUserCode()]
        private class EmptyAggregatorResolverClass : IAggregatorResolver
        {
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                return null;
            }
        }

        [DebuggerNonUserCode()]
        private class MapperResolver : IMapperResolver
        {
            private IProjectorResolver ProjectorResolver;
            private IAggregatorResolver AggregatorResolver;
            public MapperResolver(IProjectorResolver ProjectorResolver, IAggregatorResolver AggregatorResolver)
            {
                this.ProjectorResolver = ProjectorResolver;
                this.AggregatorResolver = AggregatorResolver;
            }

            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                return ProjectorResolver.TryResolveProjector(TypePair);
            }
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                return AggregatorResolver.TryResolveAggregator(TypePair);
            }
        }

        [DebuggerNonUserCode()]
        private class NoncircularProjectorResolver : IProjectorResolver
        {
            private IProjectorResolver InnerResolver;
            public NoncircularProjectorResolver(IProjectorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private HashSet<KeyValuePair<Type, Type>> ResolvingProjectorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingProjectorTypePairs.Contains(TypePair)) throw new InvalidOperationException("CircularReference: Projector({0}, {1})".Formats(TypePair.Key.FullName, TypePair.Value.FullName));
                ResolvingProjectorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveProjector(TypePair);
                }
                finally
                {
                    ResolvingProjectorTypePairs.Remove(TypePair);
                }
            }
        }
        [DebuggerNonUserCode()]
        private class NoncircularAggregatorResolver : IAggregatorResolver
        {
            private IAggregatorResolver InnerResolver;
            public NoncircularAggregatorResolver(IAggregatorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private HashSet<KeyValuePair<Type, Type>> ResolvingAggregatorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingAggregatorTypePairs.Contains(TypePair)) throw new InvalidOperationException("CircularReference: Aggregator({0}, {1})".Formats(TypePair.Key.FullName, TypePair.Value.FullName));
                ResolvingAggregatorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveAggregator(TypePair);
                }
                finally
                {
                    ResolvingAggregatorTypePairs.Remove(TypePair);
                }
            }
        }

        private class DelayFunc<D, R>
        {
            public Func<Delegate> CallDelegate;
            public R Invoke(D Key)
            {
                return ((Func<D, R>)CallDelegate())(Key);
            }
        }
        private class DelayAction<D, R>
        {
            public Func<Delegate> CallDelegate;
            public void Invoke(D Key, R Value)
            {
                ((Action<D, R>)CallDelegate())(Key, Value);
            }
        }

        [DebuggerNonUserCode()]
        private class RuntimeProjectorResolver : IProjectorResolver
        {
            private IProjectorResolver InnerResolver;
            public RuntimeProjectorResolver(IProjectorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private static Delegate GetDelayFunc<D, R>(Func<Delegate> f)
            {
                var c = new DelayFunc<D, R> { CallDelegate = f };
                return (Func<D, R>)c.Invoke;
            }
            private HashSet<KeyValuePair<Type, Type>> ResolvingProjectorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingProjectorTypePairs.Contains(TypePair))
                {
                    var f = (Func<Delegate>)(() => InnerResolver.TryResolveProjector(TypePair));
                    var df = ((Func<Func<Delegate>, Delegate>)GetDelayFunc<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    return ((Func<Func<Delegate>, Delegate>)df)(f);
                }
                ResolvingProjectorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveProjector(TypePair);
                }
                finally
                {
                    ResolvingProjectorTypePairs.Remove(TypePair);
                }
            }
        }
        [DebuggerNonUserCode()]
        private class RuntimeAggregatorResolver : IAggregatorResolver
        {
            private IAggregatorResolver InnerResolver;
            public RuntimeAggregatorResolver(IAggregatorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private static Delegate GetDelayAction<D, R>(Func<Delegate> f)
            {
                var c = new DelayAction<D, R> { CallDelegate = f };
                return (Action<D, R>)c.Invoke;
            }
            private HashSet<KeyValuePair<Type, Type>> ResolvingAggregatorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingAggregatorTypePairs.Contains(TypePair))
                {
                    var f = (Func<Delegate>)(() => InnerResolver.TryResolveAggregator(TypePair));
                    var df = ((Func<Func<Delegate>, Delegate>)GetDelayAction<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    return ((Func<Func<Delegate>, Delegate>)df)(f);
                }
                ResolvingAggregatorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveAggregator(TypePair);
                }
                finally
                {
                    ResolvingAggregatorTypePairs.Remove(TypePair);
                }
            }
        }

        [DebuggerNonUserCode()]
        private class RuntimeDomainNoncircularProjectorResolver : IProjectorResolver
        {
            private IProjectorResolver InnerResolver;
            public RuntimeDomainNoncircularProjectorResolver(IProjectorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private class DelayFuncNoncircular<D, R>
            {
                public Func<Delegate> CallDelegate;
                private HashSet<D> Dict = new HashSet<D>();
                public R Invoke(D Key)
                {
                    if (Key == null)
                    {
                        return ((Func<D, R>)CallDelegate())(Key);
                    }
                    if (Dict.Contains(Key)) throw new InvalidOperationException("CircularReference: Projector({0}, {1})".Formats(typeof(D).FullName, typeof(R).FullName));
                    Dict.Add(Key);
                    try
                    {
                        return ((Func<D, R>)CallDelegate())(Key);
                    }
                    finally
                    {
                        Dict.Remove(Key);
                    }
                }
            }
            private static Delegate GetDelayFunc<D, R>(Func<Delegate> f)
            {
                var c = new DelayFunc<D, R> { CallDelegate = f };
                return (Func<D, R>)c.Invoke;
            }
            private static Delegate GetDelayFuncNoncircular<D, R>(Func<Delegate> f)
            {
                var c = new DelayFuncNoncircular<D, R> { CallDelegate = f };
                return (Func<D, R>)c.Invoke;
            }
            private HashSet<KeyValuePair<Type, Type>> ResolvingProjectorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingProjectorTypePairs.Contains(TypePair))
                {
                    var f = (Func<Delegate>)(() => InnerResolver.TryResolveProjector(TypePair));
                    Delegate df;
                    if (TypePair.Key.IsValueType)
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayFunc<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    else
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayFuncNoncircular<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    return ((Func<Func<Delegate>, Delegate>)df)(f);
                }
                ResolvingProjectorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveProjector(TypePair);
                }
                finally
                {
                    ResolvingProjectorTypePairs.Remove(TypePair);
                }
            }
        }
        [DebuggerNonUserCode()]
        private class RuntimeDomainNoncircularAggregatorResolver : IAggregatorResolver
        {
            private IAggregatorResolver InnerResolver;
            public RuntimeDomainNoncircularAggregatorResolver(IAggregatorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private class DelayActionNoncircular<D, R>
            {
                public Func<Delegate> CallDelegate;
                private HashSet<D> Dict = new HashSet<D>();
                public void Invoke(D Key, R Value)
                {
                    if (Dict.Contains(Key)) throw new InvalidOperationException("CircularReference: Aggregator({0}, {1})".Formats(typeof(D).FullName, typeof(R).FullName));
                    Dict.Add(Key);
                    try
                    {
                        ((Action<D, R>)CallDelegate())(Key, Value);
                    }
                    finally
                    {
                        Dict.Remove(Key);
                    }
                }
            }
            private static Delegate GetDelayAction<D, R>(Func<Delegate> f)
            {
                var c = new DelayAction<D, R> { CallDelegate = f };
                return (Action<D, R>)c.Invoke;
            }
            private static Delegate GetDelayActionNoncircular<D, R>(Func<Delegate> f)
            {
                var c = new DelayActionNoncircular<D, R> { CallDelegate = f };
                return (Action<D, R>)c.Invoke;
            }
            private HashSet<KeyValuePair<Type, Type>> ResolvingAggregatorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingAggregatorTypePairs.Contains(TypePair))
                {
                    var f = (Func<Delegate>)(() => InnerResolver.TryResolveAggregator(TypePair));
                    Delegate df;
                    if (TypePair.Key.IsValueType)
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayAction<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    else
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayActionNoncircular<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    return ((Func<Func<Delegate>, Delegate>)df)(f);
                }
                ResolvingAggregatorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveAggregator(TypePair);
                }
                finally
                {
                    ResolvingAggregatorTypePairs.Remove(TypePair);
                }
            }
        }

        [DebuggerNonUserCode()]
        private class RuntimeRangeNoncircularAggregatorResolver : IAggregatorResolver
        {
            private IAggregatorResolver InnerResolver;
            public RuntimeRangeNoncircularAggregatorResolver(IAggregatorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private class DelayActionNoncircular<D, R>
            {
                public Func<Delegate> CallDelegate;
                private HashSet<R> Dict = new HashSet<R>();
                public void Invoke(D Key, R Value)
                {
                    if (Dict.Contains(Value)) throw new InvalidOperationException("CircularReference: Aggregator({0}, {1})".Formats(typeof(D).FullName, typeof(R).FullName));
                    Dict.Add(Value);
                    try
                    {
                        ((Action<D, R>)CallDelegate())(Key, Value);
                    }
                    finally
                    {
                        Dict.Remove(Value);
                    }
                }
            }
            private static Delegate GetDelayAction<D, R>(Func<Delegate> f)
            {
                var c = new DelayAction<D, R> { CallDelegate = f };
                return (Action<D, R>)c.Invoke;
            }
            private static Delegate GetDelayActionNoncircular<D, R>(Func<Delegate> f)
            {
                var c = new DelayActionNoncircular<D, R> { CallDelegate = f };
                return (Action<D, R>)c.Invoke;
            }
            private HashSet<KeyValuePair<Type, Type>> ResolvingAggregatorTypePairs = new HashSet<KeyValuePair<Type, Type>>();
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                if (ResolvingAggregatorTypePairs.Contains(TypePair))
                {
                    var f = (Func<Delegate>)(() => InnerResolver.TryResolveAggregator(TypePair));
                    Delegate df;
                    if (TypePair.Key.IsValueType)
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayAction<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    else
                    {
                        df = ((Func<Func<Delegate>, Delegate>)GetDelayActionNoncircular<DummyType, DummyType>).MakeDelegateMethod(new[] { TypePair.Key, TypePair.Value }, typeof(Func<Func<Delegate>, Delegate>));
                    }
                    return ((Func<Func<Delegate>, Delegate>)df)(f);
                }
                ResolvingAggregatorTypePairs.Add(TypePair);
                try
                {
                    return InnerResolver.TryResolveAggregator(TypePair);
                }
                finally
                {
                    ResolvingAggregatorTypePairs.Remove(TypePair);
                }
            }
        }

        /// <remarks>缓存解析器</remarks>
        [DebuggerNonUserCode()]
        private class CachedProjectorResolver : IProjectorResolver
        {
            private IProjectorResolver InnerResolver;
            public CachedProjectorResolver(IProjectorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private Dictionary<KeyValuePair<Type, Type>, Delegate> ProjectorCache = new Dictionary<KeyValuePair<Type, Type>, Delegate>();

            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (ProjectorCache.ContainsKey(TypePair)) return ProjectorCache[TypePair];
                var Resolved = InnerResolver.TryResolveProjector(TypePair);
                if (Resolved != null)
                {
                    //如果一个解析依赖于相同类型对的子解析，可能导致子解析已被加入缓存
                    if (ProjectorCache.ContainsKey(TypePair))
                    {
                        ProjectorCache[TypePair] = Resolved;
                    }
                    else
                    {
                        ProjectorCache.Add(TypePair, Resolved);
                    }
                    return Resolved;
                }
                return null;
            }
        }
        /// <remarks>缓存解析器</remarks>
        [DebuggerNonUserCode()]
        private class CachedAggregatorResolver : IAggregatorResolver
        {
            private IAggregatorResolver InnerResolver;
            public CachedAggregatorResolver(IAggregatorResolver InnerResolver)
            {
                this.InnerResolver = InnerResolver;
            }

            private Dictionary<KeyValuePair<Type, Type>, Delegate> AggregatorCache = new Dictionary<KeyValuePair<Type, Type>, Delegate>();

            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                if (AggregatorCache.ContainsKey(TypePair)) return AggregatorCache[TypePair];
                var Resolved = InnerResolver.TryResolveAggregator(TypePair);
                if (Resolved != null)
                {
                    //如果一个解析依赖于相同类型对的子解析，可能导致子解析已被加入缓存
                    if (AggregatorCache.ContainsKey(TypePair))
                    {
                        AggregatorCache[TypePair] = Resolved;
                    }
                    else
                    {
                        AggregatorCache.Add(TypePair, Resolved);
                    }
                    return Resolved;
                }
                return null;
            }
        }

        /// <remarks>选择解析器</remarks>
        [DebuggerNonUserCode()]
        private class ConcatenatedProjectorResolver : IProjectorResolver
        {
            private IEnumerable<IProjectorResolver> InnerResolvers;
            public ConcatenatedProjectorResolver(IEnumerable<IProjectorResolver> InnerResolvers)
            {
                this.InnerResolvers = InnerResolvers;
            }

            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                foreach (var r in InnerResolvers)
                {
                    var Resolved = r.TryResolveProjector(TypePair);
                    if (Resolved != null)
                    {
                        return Resolved;
                    }
                }
                return null;
            }
        }
        /// <remarks>选择解析器</remarks>
        [DebuggerNonUserCode()]
        private class ConcatenatedAggregatorResolver : IAggregatorResolver
        {
            private IEnumerable<IAggregatorResolver> InnerResolvers;
            public ConcatenatedAggregatorResolver(IEnumerable<IAggregatorResolver> InnerResolvers)
            {
                this.InnerResolvers = InnerResolvers;
            }

            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                foreach (var r in InnerResolvers)
                {
                    var Resolved = r.TryResolveAggregator(TypePair);
                    if (Resolved != null)
                    {
                        return Resolved;
                    }
                }
                return null;
            }
        }
    }

    /// <remarks>基元解析器</remarks>
    [DebuggerNonUserCode()]
    public class PrimitiveResolver : IMapperResolver
    {
        private Dictionary<KeyValuePair<Type, Type>, Delegate> ProjectorCache = new Dictionary<KeyValuePair<Type, Type>, Delegate>();
        private Dictionary<KeyValuePair<Type, Type>, Delegate> AggregatorCache = new Dictionary<KeyValuePair<Type, Type>, Delegate>();

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            if (ProjectorCache.ContainsKey(TypePair)) return ProjectorCache[TypePair];
            return null;
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            if (AggregatorCache.ContainsKey(TypePair)) return AggregatorCache[TypePair];
            return null;
        }

        public void PutProjector<D, R>(Func<D, R> Projector)
        {
            var TypePair = CollectionOperations.CreatePair(typeof(D), typeof(R));
            if (ProjectorCache.ContainsKey(TypePair))
            {
                ProjectorCache[TypePair] = Projector;
            }
            else
            {
                ProjectorCache.Add(TypePair, Projector);
            }
        }
        public void PutAggregator<D, R>(Action<D, R> Aggregator)
        {
            var TypePair = CollectionOperations.CreatePair(typeof(D), typeof(R));
            if (AggregatorCache.ContainsKey(TypePair))
            {
                AggregatorCache[TypePair] = Aggregator;
            }
            else
            {
                AggregatorCache.Add(TypePair, Aggregator);
            }
        }
    }

    /// <remarks>引用解析器</remarks>
    [DebuggerNonUserCode()]
    public class ReferenceProjectorResolver : IProjectorResolver
    {
        public IProjectorResolver Inner { get; set; }
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Inner.TryResolveProjector(TypePair);
        }
    }

    /// <remarks>引用解析器</remarks>
    [DebuggerNonUserCode()]
    public class ReferenceAggregatorResolver : IAggregatorResolver
    {
        public IAggregatorResolver Inner { get; set; }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Inner.TryResolveAggregator(TypePair);
        }
    }

    /// <remarks>引用解析器</remarks>
    [DebuggerNonUserCode()]
    public class ReferenceMapperResolver : IMapperResolver
    {
        public IMapperResolver Inner { get; set; }
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Inner.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Inner.TryResolveAggregator(TypePair);
        }
    }
}
