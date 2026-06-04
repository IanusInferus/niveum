using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Firefly;
using Firefly.Mapping.MetaProgramming;
using Firefly.Streaming;

namespace Firefly.Mapping.Binary
{
    public interface IBinaryReader<TReadStream> where TReadStream : IReadableStream
    {
        T Read<T>(TReadStream s);
    }
    public interface IBinaryWriter<TWriteStream> where TWriteStream : IWritableStream
    {
        void Write<T>(T Value, TWriteStream s);
    }
    public interface IBinaryCounter
    {
        long Count<T>(T Value);
    }
    public interface IBinarySerializer<TReadStream, TWriteStream> : IBinaryReader<TReadStream>, IBinaryWriter<TWriteStream>, IBinaryCounter
        where TReadStream : IReadableStream
        where TWriteStream : IWritableStream
    {
    }

    public class BinarySerializer : BinarySerializer<IReadableStream, IWritableStream>
    {
    }
    public class BinaryReaderResolver : BinaryReaderResolver<IReadableStream>
    {
        public BinaryReaderResolver(IMapperResolver Root) : base(Root)
        {
        }
    }
    public class BinaryWriterResolver : BinaryWriterResolver<IWritableStream>
    {
        public BinaryWriterResolver(IMapperResolver Root) : base(Root)
        {
        }
    }

    /// <remarks>
    /// 对于非简单类型，应提供自定义序列化器
    /// 简单类型 ::= 简单类型
    ///           | Byte(UInt8) | UInt16 | UInt32 | UInt64 | Int8(SByte) | Int16 | Int32 | Int64 | Float32(Single) | Float64(Double)
    ///           | Boolean
    ///           | 枚举
    ///           | 数组(简单类型)
    ///           | ICollection(简单类型)
    ///           | 简单类或结构
    /// 简单类或结构 ::=
    ///               ( 类或结构(构造函数(参数(简单类型)*), 公共只读字段(简单类型)*, 公共可写属性{0}) AND (参数(简单类型)* = 公共只读字段(简单类型)*)
    ///               | 类或结构(构造函数(参数(简单类型)*), 公共可写字段{0}, 公共只读属性(简单类型)*) AND (参数(简单类型)* = 公共只读属性(简单类型)*)
    ///               | 类或结构(无参构造函数, 公共可读写字段(简单类型)*, 公共可写属性{0})
    ///               | 类或结构(无参构造函数, 公共可写字段{0}, 公共可读写属性(简单类型)*)
    ///               ) AND 类型结构为树状
    /// 此外，对象树中不应有空引用，否则应提供自定义序列化器
    /// </remarks>
    public class BinarySerializer<TReadStream, TWriteStream> : IBinarySerializer<TReadStream, TWriteStream>
        where TReadStream : IReadableStream
        where TWriteStream : IWritableStream
    {
        private BinaryReaderResolver<TReadStream> ReaderResolver;
        private BinaryWriterResolver<TWriteStream> WriterResolver;
        private BinaryCounterResolver CounterResolver;

        private IMapperResolver ReaderCache;
        private IMapperResolver WriterCache;
        private IMapperResolver CounterCache;

        public BinarySerializer()
        {
            var ReaderReference = new ReferenceMapperResolver();
            ReaderCache = ReaderReference;
            ReaderResolver = new BinaryReaderResolver<TReadStream>(ReaderReference);
            ReaderReference.Inner = ReaderResolver.AsCached();

            var WriterReference = new ReferenceMapperResolver();
            WriterCache = WriterReference;
            WriterResolver = new BinaryWriterResolver<TWriteStream>(WriterReference);
            WriterReference.Inner = WriterResolver.AsCached();

            var CounterReference = new ReferenceMapperResolver();
            CounterCache = CounterReference;
            CounterResolver = new BinaryCounterResolver(CounterReference);
            CounterReference.Inner = CounterResolver.AsCached();
        }

        public void PutReader<T>(Func<TReadStream, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Action<T, TWriteStream> Writer)
        {
            WriterResolver.PutWriter(Writer);
        }
        public void PutCounter<T>(Func<T, long> Counter)
        {
            CounterResolver.PutCounter(Counter);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<D, M>(IAggregatorToAggregatorDomainTranslator<D, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }
        public void PutCounterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            CounterResolver.PutCounterTranslator(Translator);
        }

        public T Read<T>(TReadStream s)
        {
            var m = ReaderCache.ResolveProjector<TReadStream, T>();
            return m(s);
        }
        public void Write<T>(T Value, TWriteStream s)
        {
            var m = WriterCache.ResolveAggregator<T, TWriteStream>();
            m(Value, s);
        }
        public void Write<T>(TWriteStream s, T Value)
        {
            Write(Value, s);
        }
        public long Count<T>(T Value)
        {
            var m = CounterCache.ResolveProjector<T, long>();
            return m(Value);
        }
    }

    public class BinaryReaderResolver<TReadStream> : IMapperResolver where TReadStream : IReadableStream
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IProjectorResolver> ProjectorResolverList;

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public BinaryReaderResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutReader((TReadStream s) => s.ReadByte());
            PutReader((TReadStream s) => s.ReadUInt16());
            PutReader((TReadStream s) => s.ReadUInt32());
            PutReader((TReadStream s) => s.ReadUInt64());
            PutReader((TReadStream s) => s.ReadInt8());
            PutReader((TReadStream s) => s.ReadInt16());
            PutReader((TReadStream s) => s.ReadInt32());
            PutReader((TReadStream s) => s.ReadInt64());
            PutReader((TReadStream s) => s.ReadFloat32());
            PutReader((TReadStream s) => s.ReadFloat64());
            PutReader((TReadStream s) => s.ReadByte() != 0);

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                new EnumUnpacker<TReadStream>(Root.AsRuntime()),
                new CollectionUnpackerTemplate<TReadStream>(new GenericCollectionProjectorResolver<TReadStream>(Root.AsRuntime())),
                new RecordUnpackerTemplate<TReadStream>(Root.AsRuntime())
            });
            Resolver = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), Mapping.EmptyAggregatorResolver);
        }

        public void PutReader<T>(Func<TReadStream, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntime(), Translator));
        }
    }

    public class BinaryWriterResolver<TWriteStream> : IMapperResolver where TWriteStream : IWritableStream
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IAggregatorResolver> AggregatorResolverList;

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public BinaryWriterResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutWriter((byte b, TWriteStream s) => s.WriteByte(b));
            PutWriter((ushort i, TWriteStream s) => s.WriteUInt16(i));
            PutWriter((uint i, TWriteStream s) => s.WriteUInt32(i));
            PutWriter((ulong i, TWriteStream s) => s.WriteUInt64(i));
            PutWriter((sbyte i, TWriteStream s) => s.WriteInt8(i));
            PutWriter((short i, TWriteStream s) => s.WriteInt16(i));
            PutWriter((int i, TWriteStream s) => s.WriteInt32(i));
            PutWriter((long i, TWriteStream s) => s.WriteInt64(i));
            PutWriter((float f, TWriteStream s) => s.WriteFloat32(f));
            PutWriter((double f, TWriteStream s) => s.WriteFloat64(f));
            PutWriter((bool b, TWriteStream s) => s.WriteByte((byte)(b ? 1 : 0)));

            AggregatorResolverList = new LinkedList<IAggregatorResolver>(new IAggregatorResolver[] {
                PrimitiveResolver,
                new EnumPacker<TWriteStream>(Root.AsRuntimeDomainNoncircular()),
                new CollectionPackerTemplate<TWriteStream>(new GenericCollectionAggregatorResolver<TWriteStream>(Root.AsRuntimeDomainNoncircular())),
                new RecordPackerTemplate<TWriteStream>(Root.AsRuntimeDomainNoncircular())
            });
            Resolver = Mapping.CreateMapper(Mapping.EmptyProjectorResolver, AggregatorResolverList.Concatenated());
        }

        public void PutWriter<T>(Action<T, TWriteStream> Writer)
        {
            PrimitiveResolver.PutAggregator(Writer);
        }
        public void PutWriterTranslator<D, M>(IAggregatorToAggregatorDomainTranslator<D, M> Translator)
        {
            AggregatorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            var t = new PP2AADomainTranslatorTranslator<D, M>(Translator);
            AggregatorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), t));
        }

        //AA(D, M)(R): (M aggr R) -> (D aggr R) = (D proj M) @ (M aggr R)
        //PP(D, M)(M): (M proj M) -> (D proj M) = (D proj M) @ (M proj M)
        //PP2AA(D, M)(R): (M aggr R) -> (D aggr R) = PP(D, M)(M)(M -> M: m |-> m) @ AA(D, M)(R) = (D proj M) @ (M -> M: m |-> m) @ (M aggr R) = (D proj M) @ (M aggr R)
        private class PP2AADomainTranslatorTranslator<D, M> : IAggregatorToAggregatorDomainTranslator<D, M>
        {
            public Action<D, R> TranslateAggregatorToAggregatorDomain<R>(Action<M, R> Aggregator)
            {
                Func<M, M> Identity = k => k;
                return (D k, R v) => Aggregator(Inner.TranslateProjectorToProjectorDomain<M>(Identity)(k), v);
            }

            private IProjectorToProjectorDomainTranslator<D, M> Inner;
            public PP2AADomainTranslatorTranslator(IProjectorToProjectorDomainTranslator<D, M> Inner)
            {
                this.Inner = Inner;
            }
        }
    }

    public class BinaryCounterResolver : IMapperResolver
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IProjectorResolver> ProjectorResolverList;
        private LinkedList<IAggregatorResolver> AggregatorResolverList;

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public BinaryCounterResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutCounter((byte b) => (long)1);
            PutCounter((ushort i) => (long)2);
            PutCounter((uint i) => (long)4);
            PutCounter((ulong i) => (long)8);
            PutCounter((sbyte i) => (long)1);
            PutCounter((short i) => (long)2);
            PutCounter((int i) => (long)4);
            PutCounter((long i) => (long)8);
            PutCounter((float f) => (long)4);
            PutCounter((double f) => (long)8);
            PutCounter((bool b) => (long)1);

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new CounterStateToIntRangeTranslator())
            });
            AggregatorResolverList = new LinkedList<IAggregatorResolver>(new IAggregatorResolver[] {
                new EnumPacker<CounterState>(Root.AsRuntimeDomainNoncircular()),
                new CollectionPackerTemplate<CounterState>(new GenericCollectionAggregatorResolver<CounterState>(Root.AsRuntimeDomainNoncircular())),
                new RecordPackerTemplate<CounterState>(Root.AsRuntimeDomainNoncircular()),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new IntToCounterStateRangeTranslator())
            });
            Resolver = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), AggregatorResolverList.Concatenated());
        }

        public void PutCounter<T>(Func<T, long> Counter)
        {
            PrimitiveResolver.PutProjector(Counter);
        }
        public void PutCounterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }

        public class CounterState
        {
            public long Number;
        }
        private class CounterStateToIntRangeTranslator : IAggregatorToProjectorRangeTranslator<long, CounterState>
        {
            public Func<D, long> TranslateAggregatorToProjectorRange<D>(Action<D, CounterState> Aggregator)
            {
                return Key =>
                {
                    var c = new CounterState();
                    Aggregator(Key, c);
                    return c.Number;
                };
            }
        }
        private class IntToCounterStateRangeTranslator : IProjectorToAggregatorRangeTranslator<CounterState, long>
        {
            public Action<D, CounterState> TranslateProjectorToAggregatorRange<D>(Func<D, long> Projector)
            {
                return (Key, c) =>
                {
                    c.Number += Projector(Key);
                };
            }
        }
    }

    public class EnumUnpacker<D> : IProjectorResolver
    {
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType != typeof(D)) return null;
            if (RangeType.IsEnum)
            {
                var UnderlyingType = RangeType.GetEnumUnderlyingType();
                var Mapper = InnerResolver.ResolveProjector(CollectionOperations.CreatePair(DomainType, UnderlyingType));
                var dParam = Expression.Parameter(typeof(D), "Key");

                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                DelegateCalls.Add(new KeyValuePair<Delegate, Expression[]>(Mapper, new Expression[] { dParam }));
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                var FunctionBody = Expression.ConvertChecked(Context.DelegateExpressions.Single(), RangeType);
                var FunctionLambda = Expression.Lambda(FunctionBody, new ParameterExpression[] { dParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private IProjectorResolver InnerResolver;
        public EnumUnpacker(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class EnumPacker<R> : IAggregatorResolver
    {
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (RangeType != typeof(R)) return null;
            if (DomainType.IsEnum)
            {
                var UnderlyingType = DomainType.GetEnumUnderlyingType();
                var Mapper = InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(UnderlyingType, RangeType));
                var dParam = Expression.Parameter(DomainType, "Key");
                var rParam = Expression.Parameter(typeof(R), "Value");

                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                DelegateCalls.Add(new KeyValuePair<Delegate, Expression[]>(Mapper, new Expression[] { Expression.ConvertChecked(dParam, UnderlyingType), rParam }));
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                var FunctionLambda = Expression.Lambda(Context.DelegateExpressions.Single(), new ParameterExpression[] { dParam, rParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private IAggregatorResolver InnerResolver;
        public EnumPacker(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class GenericCollectionProjectorResolver<D> : IGenericCollectionProjectorResolver<D>
    {
        public Func<D, RCollection> ResolveProjector<R, RCollection>() where RCollection : ICollection<R>, new()
        {
            var Mapper = (Func<D, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(R)));
            var IntMapper = (Func<D, int>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(int)));
            Func<D, RCollection> F = Key =>
            {
                var NumElement = IntMapper(Key);
                var c = new RCollection();
                for (var n = 0; n < NumElement; n += 1)
                {
                    c.Add(Mapper(Key));
                }
                return c;
            };
            return F;
        }

        private IProjectorResolver InnerResolver;
        public GenericCollectionProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class GenericCollectionAggregatorResolver<R> : IGenericCollectionAggregatorResolver<R>
    {
        public Action<DCollection, R> ResolveAggregator<D, DCollection>() where DCollection : ICollection<D>
        {
            var Mapper = (Action<D, R>)InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(typeof(D), typeof(R)));
            var IntMapper = (Action<int, R>)InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(typeof(int), typeof(R)));
            Action<DCollection, R> F = (c, Value) =>
            {
                var NumElement = c.Count;
                IntMapper(NumElement, Value);
                foreach (var v in c)
                {
                    Mapper(v, Value);
                }
            };
            return F;
        }

        private IAggregatorResolver InnerResolver;
        public GenericCollectionAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
}
