using System;
using System.Collections.Generic;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.MetaProgramming;

namespace Firefly.Mapping
{
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IProjectorToProjectorDomainTranslator<D, M>
    {
        Func<D, R> TranslateProjectorToProjectorDomain<R>(Func<M, R> Projector);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IAggregatorToAggregatorDomainTranslator<D, M>
    {
        Action<D, R> TranslateAggregatorToAggregatorDomain<R>(Action<M, R> Aggregator);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IProjectorToProjectorRangeTranslator<R, M>
    {
        Func<D, R> TranslateProjectorToProjectorRange<D>(Func<D, M> Projector);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IProjectorToAggregatorRangeTranslator<R, M>
    {
        Action<D, R> TranslateProjectorToAggregatorRange<D>(Func<D, M> Projector);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IAggregatorToProjectorRangeTranslator<R, M>
    {
        Func<D, R> TranslateAggregatorToProjectorRange<D>(Action<D, M> Aggregator);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IAggregatorToAggregatorRangeTranslator<R, M>
    {
        Action<D, R> TranslateAggregatorToAggregatorRange<D>(Action<D, M> Aggregator);
    }

    /// <summary>映射分解器</summary>
    /// <remarks>
    /// IProjectorToProjectorDomainTranslator(D, M) = Projector(M, R) -> Projector(D, R)
    /// IAggregatorToAggregatorDomainTranslator(D, M) = Aggregator(M, R) -> Aggregator(D, R)
    /// IAggregatorToProjectorRangeTranslator(R, M) = Aggregator(D, M) -> Projector(D, R)
    /// IProjectorToAggregatorRangeTranslator(R, M) = Projector(D, M) -> Aggregator(D, R)
    /// 这样就能把(D, R)的映射器转换为(M, R)或者(D, M)的映射器，是一种化简。
    /// 不过使用的前提是(D, M)或者(R, M)静态已知。
    /// 本解析器应小心放置，以防止死递归导致无法解析。
    /// </remarks>
    [DebuggerNonUserCode()]
    public class TranslatorResolver
    {
        private TranslatorResolver()
        {
        }

        public static IProjectorResolver Create<D, M>(IProjectorResolver Resolver, IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            return new DPP<D, M> { Inner = Resolver, Translator = Translator };
        }
        public static IAggregatorResolver Create<D, M>(IAggregatorResolver Resolver, IAggregatorToAggregatorDomainTranslator<D, M> Translator)
        {
            return new DAA<D, M> { Inner = Resolver, Translator = Translator };
        }
        public static IProjectorResolver Create<R, M>(IProjectorResolver Resolver, IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            return new RPP<R, M> { Inner = Resolver, Translator = Translator };
        }
        public static IAggregatorResolver Create<R, M>(IProjectorResolver Resolver, IProjectorToAggregatorRangeTranslator<R, M> Translator)
        {
            return new RPA<R, M> { Inner = Resolver, Translator = Translator };
        }
        public static IProjectorResolver Create<R, M>(IAggregatorResolver Resolver, IAggregatorToProjectorRangeTranslator<R, M> Translator)
        {
            return new RAP<R, M> { Inner = Resolver, Translator = Translator };
        }
        public static IAggregatorResolver Create<R, M>(IAggregatorResolver Resolver, IAggregatorToAggregatorRangeTranslator<R, M> Translator)
        {
            return new RAA<R, M> { Inner = Resolver, Translator = Translator };
        }


        //Domain

        [DebuggerNonUserCode()]
        private class DPP<D, M> : IProjectorResolver
        {
            public IProjectorResolver Inner;
            public IProjectorToProjectorDomainTranslator<D, M> Translator;
            private Func<Func<M, DummyType>, Func<D, DummyType>> DummyMethod;
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (DomainType == typeof(D))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateProjectorToProjectorDomain<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(RangeType);
                    var m = Inner.TryResolveProjector(CollectionOperations.CreatePair(typeof(M), RangeType));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }
        [DebuggerNonUserCode()]
        private class DAA<D, M> : IAggregatorResolver
        {
            public IAggregatorResolver Inner;
            public IAggregatorToAggregatorDomainTranslator<D, M> Translator;
            private Func<Action<M, DummyType>, Action<D, DummyType>> DummyMethod;
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (DomainType == typeof(D))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateAggregatorToAggregatorDomain<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(RangeType);
                    var m = Inner.TryResolveAggregator(CollectionOperations.CreatePair(typeof(M), RangeType));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }


        //Range

        [DebuggerNonUserCode()]
        private class RPP<R, M> : IProjectorResolver
        {
            public IProjectorResolver Inner;
            public IProjectorToProjectorRangeTranslator<R, M> Translator;
            private Func<Func<DummyType, M>, Func<DummyType, R>> DummyMethod;
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (RangeType == typeof(R))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateProjectorToProjectorRange<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(DomainType);
                    var m = Inner.TryResolveProjector(CollectionOperations.CreatePair(DomainType, typeof(M)));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }
        [DebuggerNonUserCode()]
        private class RPA<R, M> : IAggregatorResolver
        {
            public IProjectorResolver Inner;
            public IProjectorToAggregatorRangeTranslator<R, M> Translator;
            private Func<Func<DummyType, M>, Action<DummyType, R>> DummyMethod;
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (RangeType == typeof(R))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateProjectorToAggregatorRange<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(DomainType);
                    var m = Inner.TryResolveProjector(CollectionOperations.CreatePair(DomainType, typeof(M)));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }
        [DebuggerNonUserCode()]
        private class RAP<R, M> : IProjectorResolver
        {
            public IAggregatorResolver Inner;
            public IAggregatorToProjectorRangeTranslator<R, M> Translator;
            private Func<Action<DummyType, M>, Func<DummyType, R>> DummyMethod;
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (RangeType == typeof(R))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateAggregatorToProjectorRange<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(DomainType);
                    var m = Inner.TryResolveAggregator(CollectionOperations.CreatePair(DomainType, typeof(M)));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }
        [DebuggerNonUserCode()]
        private class RAA<R, M> : IAggregatorResolver
        {
            public IAggregatorResolver Inner;
            public IAggregatorToAggregatorRangeTranslator<R, M> Translator;
            private Func<Action<DummyType, M>, Action<DummyType, R>> DummyMethod;
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                var DomainType = TypePair.Key;
                var RangeType = TypePair.Value;
                if (RangeType == typeof(R))
                {
                    if (DummyMethod == null) DummyMethod = Translator.TranslateAggregatorToAggregatorRange<DummyType>;
                    var t = DummyMethod.MakeDelegateMethodFromDummy(DomainType);
                    var m = Inner.TryResolveAggregator(CollectionOperations.CreatePair(DomainType, typeof(M)));
                    if (m == null) return null;
                    return t.StaticDynamicInvoke<Delegate, Delegate>(m);
                }
                return null;
            }
        }
    }
}
