using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.MetaProgramming;

namespace Firefly.Mapping
{
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IGenericCollectionProjectorResolver<D>
    {
        Func<D, RCollection> ResolveProjector<R, RCollection>() where RCollection : ICollection<R>, new();
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IGenericCollectionAggregatorResolver<R>
    {
        Action<DCollection, R> ResolveAggregator<D, DCollection>() where DCollection : ICollection<D>;
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IFieldProjectorResolver<D>
    {
        /// <returns>返回Func(Of ${DomainType}, ${FieldOrPropertyType})</returns>
        Delegate ResolveProjector(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IFieldAggregatorResolver<R>
    {
        /// <returns>返回Action(Of ${FieldOrPropertyType}, ${RangeType})</returns>
        Delegate ResolveAggregator(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IAliasFieldProjectorResolver<D>
    {
        /// <returns>返回Func(Of ${DomainType}, ${FieldOrPropertyType})</returns>
        Delegate ResolveProjector(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface IAliasFieldAggregatorResolver<R>
    {
        /// <returns>返回Action(Of ${FieldOrPropertyType}, ${RangeType})</returns>
        Delegate ResolveAggregator(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITagProjectorResolver<D>
    {
        /// <returns>返回Func(Of ${DomainType}, ${TagType})</returns>
        Delegate ResolveProjector(MemberInfo Member, Type TagType);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITagAggregatorResolver<R>
    {
        /// <returns>返回Action(Of ${TagType}, ${RangeType})</returns>
        Delegate ResolveAggregator(MemberInfo Member, Type TagType);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITaggedUnionAlternativeProjectorResolver<D>
    {
        /// <returns>返回Func(Of ${DomainType}, ${FieldOrPropertyType})</returns>
        Delegate ResolveProjector(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITaggedUnionAlternativeAggregatorResolver<R>
    {
        /// <returns>返回Action(Of ${FieldOrPropertyType}, ${RangeType})</returns>
        Delegate ResolveAggregator(MemberInfo Member, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITupleElementProjectorResolver<D>
    {
        /// <returns>返回Func(Of ${DomainType}, ${Type})</returns>
        Delegate ResolveProjector(MemberInfo Member, int Index, Type Type);
    }
    /// <remarks>实现带泛型约束的接口会导致代码分析无效。</remarks>
    public interface ITupleElementAggregatorResolver<R>
    {
        /// <returns>返回Action(Of ${Type}, ${RangeType})</returns>
        Delegate ResolveAggregator(MemberInfo Member, int Index, Type Type);
    }

    [DebuggerNonUserCode()]
    public class CollectionUnpackerTemplate<D> : IProjectorResolver
    {
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType != typeof(D)) return null;
            if (RangeType.IsArray)
            {
                var ArrayMapperGen = TryGetArrayMapperGenerator(RangeType.GetArrayRank());
                if (ArrayMapperGen != null)
                {
                    return ArrayMapperGen(RangeType.GetElementType());
                }
            }
            if (RangeType.IsProperCollectionType())
            {
                var CollectionMapperGen = TryGetCollectionMapperGenerator(RangeType.GetGenericTypeDefinition());
                if (CollectionMapperGen != null)
                {
                    return CollectionMapperGen(RangeType.GetGenericArguments());
                }
            }
            return null;
        }

        private Dictionary<int, Func<Type, Delegate>> ArrayMapperGeneratorCache = new Dictionary<int, Func<Type, Delegate>>();
        private Dictionary<Type, Func<Type[], Delegate>> CollectionMapperGeneratorCache = new Dictionary<Type, Func<Type[], Delegate>>();

        public void PutGenericArrayMapper(Delegate GenericMapper)
        {
            var FuncType = GenericMapper.GetType();
            if (!FuncType.IsGenericType) throw new ArgumentException();
            if (FuncType.GetGenericTypeDefinition() != typeof(Func<,>)) throw new ArgumentException();
            var DummyArrayType = FuncType.GetGenericArguments()[1];
            if (!DummyArrayType.IsArray) throw new ArgumentException();
            if (DummyArrayType.GetElementType() != typeof(DummyType)) throw new ArgumentException();
            var Dimension = DummyArrayType.GetArrayRank();

            Func<Type, Delegate> Gen = ElementType => GenericMapper.MakeDelegateMethodFromDummy(ElementType);
            if (ArrayMapperGeneratorCache.ContainsKey(Dimension))
            {
                ArrayMapperGeneratorCache[Dimension] = Gen;
            }
            else
            {
                ArrayMapperGeneratorCache.Add(Dimension, Gen);
            }
        }
        public void PutGenericArrayMapper(Func<D, DummyType[]> GenericMapper)
        {
            PutGenericArrayMapper((Delegate)GenericMapper);
        }
        public void PutGenericArrayMapper(Func<D, DummyType[,]> GenericMapper)
        {
            PutGenericArrayMapper((Delegate)GenericMapper);
        }

        public void PutGenericCollectionMapper(Delegate GenericMapper)
        {
            var FuncType = GenericMapper.GetType();
            if (!FuncType.IsGenericType) throw new ArgumentException();
            if (FuncType.GetGenericTypeDefinition() != typeof(Func<,>)) throw new ArgumentException();
            var DummyCollectionType = FuncType.GetGenericArguments()[1];
            if (!DummyCollectionType.IsProperCollectionType()) throw new ArgumentException();
            var CollectionType = DummyCollectionType.GetGenericTypeDefinition();

            Func<Type[], Delegate> Gen = TypeArguments =>
            {
                var ConcreteCollectionType = CollectionType.MakeGenericType(TypeArguments);
                var ElementType = ConcreteCollectionType.GetCollectionElementType();
                return GenericMapper.MakeDelegateMethodFromDummy(ElementType);
            };
            if (CollectionMapperGeneratorCache.ContainsKey(CollectionType))
            {
                CollectionMapperGeneratorCache[CollectionType] = Gen;
            }
            else
            {
                CollectionMapperGeneratorCache.Add(CollectionType, Gen);
            }
        }
        public void PutGenericCollectionMapper<RCollection>(Func<D, RCollection> GenericMapper) where RCollection : ICollection<DummyType>, new()
        {
            PutGenericCollectionMapper((Delegate)GenericMapper);
        }

        private static Func<D, T[]> ArrayToListGenericMapperAdapter<T>(Func<D, List<T>> DummyMethod)
        {
            return Key => DummyMethod(Key).ToArray();
        }
        public Func<Type, Delegate> TryGetArrayMapperGenerator(int Dimension)
        {
            if (!ArrayMapperGeneratorCache.ContainsKey(Dimension))
            {
                if (Dimension != 1) return null;
                if (Inner == null) return null;
                var m = (Func<Func<D, DummyType[]>>)ArrayToList<DummyType>;
                Func<Type, Delegate> Gen = ElementType => m.MakeDelegateMethodFromDummy(ElementType).StaticDynamicInvoke<Delegate>();
                ArrayMapperGeneratorCache.Add(Dimension, Gen);
            }
            return ArrayMapperGeneratorCache[Dimension];
        }
        public Func<Type[], Delegate> TryGetCollectionMapperGenerator(Type CollectionType)
        {
            if (!CollectionMapperGeneratorCache.ContainsKey(CollectionType))
            {
                if (!CollectionType.IsProperCollectionType()) throw new ArgumentException();
                if (!CollectionType.IsGenericTypeDefinition) throw new ArgumentException();
                Func<Type[], Delegate> Gen = TypeArguments =>
                {
                    var ConcreteCollectionType = CollectionType.MakeGenericType(TypeArguments);
                    var ElementType = ConcreteCollectionType.GetCollectionElementType();
                    var DummyMethod = (Func<Func<D, List<DummyType>>>)Inner.ResolveProjector<DummyType, List<DummyType>>;
                    var m = DummyMethod.MakeDelegateMethodFromDummy(Type =>
                    {
                        if (Type == typeof(DummyType)) return ElementType;
                        if (Type == typeof(List<DummyType>)) return ConcreteCollectionType;
                        return Type;
                    });
                    return m.MakeDelegateMethodFromDummy(ElementType).StaticDynamicInvoke<Delegate>();
                };
                CollectionMapperGeneratorCache.Add(CollectionType, Gen);
            }
            return CollectionMapperGeneratorCache[CollectionType];
        }

        private Func<D, R[]> ArrayToList<R>()
        {
            var Mapper = Inner.ResolveProjector<R, List<R>>();
            return Key =>
            {
                var l = Mapper(Key);
                if (l == null) return null;
                return l.ToArray();
            };
        }

        private IGenericCollectionProjectorResolver<D> Inner;
        public CollectionUnpackerTemplate(IGenericCollectionProjectorResolver<D> Inner)
        {
            this.Inner = Inner;
        }
    }
    [DebuggerNonUserCode()]
    public class CollectionPackerTemplate<R> : IAggregatorResolver
    {
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (RangeType != typeof(R)) return null;
            if (DomainType.IsArray)
            {
                var ArrayMapperGen = TryGetArrayMapperGenerator(DomainType.GetArrayRank());
                if (ArrayMapperGen != null)
                {
                    return ArrayMapperGen(DomainType.GetElementType());
                }
            }
            if (DomainType.IsProperCollectionType())
            {
                var CollectionMapperGen = TryGetCollectionMapperGenerator(DomainType.GetGenericTypeDefinition());
                if (CollectionMapperGen != null)
                {
                    return CollectionMapperGen(DomainType.GetGenericArguments());
                }
            }
            return null;
        }

        private Dictionary<int, Func<Type, Delegate>> ArrayMapperGeneratorCache = new Dictionary<int, Func<Type, Delegate>>();
        private Dictionary<Type, Func<Type[], Delegate>> CollectionMapperGeneratorCache = new Dictionary<Type, Func<Type[], Delegate>>();

        public void PutGenericArrayMapper(Delegate GenericMapper)
        {
            var FuncType = GenericMapper.GetType();
            if (!FuncType.IsGenericType) throw new ArgumentException();
            if (FuncType.GetGenericTypeDefinition() != typeof(Action<,>)) throw new ArgumentException();
            var DummyArrayType = FuncType.GetGenericArguments()[0];
            if (!DummyArrayType.IsArray) throw new ArgumentException();
            if (DummyArrayType.GetElementType() != typeof(DummyType)) throw new ArgumentException();
            var Dimension = DummyArrayType.GetArrayRank();

            Func<Type, Delegate> Gen = ElementType => GenericMapper.MakeDelegateMethodFromDummy(ElementType);
            if (ArrayMapperGeneratorCache.ContainsKey(Dimension))
            {
                ArrayMapperGeneratorCache[Dimension] = Gen;
            }
            else
            {
                ArrayMapperGeneratorCache.Add(Dimension, Gen);
            }
        }
        public void PutGenericArrayMapper(Action<DummyType[], R> GenericMapper)
        {
            PutGenericArrayMapper((Delegate)GenericMapper);
        }
        public void PutGenericArrayMapper(Action<DummyType[,], R> GenericMapper)
        {
            PutGenericArrayMapper((Delegate)GenericMapper);
        }

        public void PutGenericCollectionMapper(Delegate GenericMapper)
        {
            var FuncType = GenericMapper.GetType();
            if (!FuncType.IsGenericType) throw new ArgumentException();
            if (FuncType.GetGenericTypeDefinition() != typeof(Action<,>)) throw new ArgumentException();
            var DummyCollectionType = FuncType.GetGenericArguments()[0];
            if (!DummyCollectionType.IsProperCollectionType()) throw new ArgumentException();
            var CollectionType = DummyCollectionType.GetGenericTypeDefinition();

            Func<Type[], Delegate> Gen = TypeArguments =>
            {
                var ConcreteCollectionType = CollectionType.MakeGenericType(TypeArguments);
                var ElementType = ConcreteCollectionType.GetCollectionElementType();
                return GenericMapper.MakeDelegateMethodFromDummy(ElementType);
            };

            if (CollectionMapperGeneratorCache.ContainsKey(CollectionType))
            {
                CollectionMapperGeneratorCache[CollectionType] = Gen;
            }
            else
            {
                CollectionMapperGeneratorCache.Add(CollectionType, Gen);
            }
        }
        public void PutGenericCollectionMapper<DCollection>(Action<DCollection, R> GenericMapper) where DCollection : ICollection<DummyType>
        {
            PutGenericCollectionMapper((Delegate)GenericMapper);
        }

        public Func<Type, Delegate> TryGetArrayMapperGenerator(int Dimension)
        {
            if (!ArrayMapperGeneratorCache.ContainsKey(Dimension))
            {
                if (Dimension != 1) return null;
                var m = (Func<Action<DummyType[], R>>)ArrayToList<DummyType>;
                Func<Type, Delegate> Gen = ElementType => m.MakeDelegateMethodFromDummy(ElementType).StaticDynamicInvoke<Delegate>();
                ArrayMapperGeneratorCache.Add(Dimension, Gen);
            }
            return ArrayMapperGeneratorCache[Dimension];
        }
        public Func<Type[], Delegate> TryGetCollectionMapperGenerator(Type CollectionType)
        {
            if (!CollectionMapperGeneratorCache.ContainsKey(CollectionType))
            {
                if (!CollectionType.IsProperCollectionType()) throw new ArgumentException();
                if (!CollectionType.IsGenericTypeDefinition) throw new ArgumentException();

                Func<Type[], Delegate> Gen = TypeArguments =>
                {
                    var ConcreteCollectionType = CollectionType.MakeGenericType(TypeArguments);
                    var ElementType = ConcreteCollectionType.GetCollectionElementType();
                    var DummyMethod = (Func<Action<List<DummyType>, R>>)Inner.ResolveAggregator<DummyType, List<DummyType>>;
                    var m = DummyMethod.MakeDelegateMethodFromDummy(Type =>
                    {
                        if (Type == typeof(DummyType)) return ElementType;
                        if (Type == typeof(List<DummyType>)) return ConcreteCollectionType;
                        return Type;
                    });
                    return m.MakeDelegateMethodFromDummy(ElementType).StaticDynamicInvoke<Delegate>();
                };
                CollectionMapperGeneratorCache.Add(CollectionType, Gen);
            }
            return CollectionMapperGeneratorCache[CollectionType];
        }

        private Action<D[], R> ArrayToList<D>()
        {
            var Mapper = Inner.ResolveAggregator<D, List<D>>();
            return (Arr, Value) =>
            {
                if (Arr == null)
                {
                    Mapper(null, Value);
                }
                else
                {
                    Mapper(Arr.ToList(), Value);
                }
            };
        }

        private IGenericCollectionAggregatorResolver<R> Inner;
        public CollectionPackerTemplate(IGenericCollectionAggregatorResolver<R> Inner)
        {
            this.Inner = Inner;
        }
    }

    [DebuggerNonUserCode()]
    public class RecordUnpackerTemplate<D> : IProjectorResolver
    {
        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType != typeof(D)) return null;
            {
                var d = TryResolveAlias(RangeType);
                if (d != null) return d;
            }
            {
                var d = TryResolveTaggedUnion(RangeType);
                if (d != null) return d;
            }
            {
                var d = TryResolveTuple(RangeType);
                if (d != null) return d;
            }
            {
                var d = TryResolveRecord(RangeType);
                if (d != null) return d;
            }
            return null;
        }

        private Delegate TryResolveAlias(Type RangeType)
        {
            var DomainType = typeof(D);
            if (RangeType.IsValueType || RangeType.IsClass)
            {
                FieldOrPropertyInfo[] FieldsAndProperties = null;
                if (FieldsAndProperties == null)
                {
                    var mri = RangeType.TryGetMutableAliasInfo();
                    if (mri != null) FieldsAndProperties = new[] { mri.Member };
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                foreach (var Pair in FieldsAndProperties)
                {
                    DelegateCalls.Add(CollectionOperations.CreatePair(AliasFieldResolver.ResolveProjector(Pair.Member, Pair.Type), new Expression[] { dParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                var CreateThis = Expression.New(RangeType);
                var MemberBindings = new List<MemberBinding>();
                foreach (var Pair in FieldsAndProperties.ZipStrict(Context.DelegateExpressions, (m, e) => new { Member = m.Member, MapperCall = e }))
                {
                    MemberBindings.Add(Expression.Bind(Pair.Member, Pair.MapperCall));
                }
                var FunctionLambda = Expression.Lambda(Expression.MemberInit(CreateThis, MemberBindings.ToArray()), new ParameterExpression[] { dParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private Delegate TryResolveTaggedUnion(Type RangeType)
        {
            var DomainType = typeof(D);
            if (RangeType.IsValueType || RangeType.IsClass)
            {
                FieldOrPropertyInfo[] FieldsAndProperties = null;
                FieldOrPropertyInfo TagMember = null;
                if (FieldsAndProperties == null)
                {
                    var mri = RangeType.TryGetMutableTaggedUnionInfo();
                    if (mri != null)
                    {
                        FieldsAndProperties = mri.Members;
                        TagMember = mri.TagMember;
                    }
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                DelegateCalls.Add(CollectionOperations.CreatePair(TagResolver.ResolveProjector(TagMember.Member, TagMember.Type), new Expression[] { dParam }));
                foreach (var Pair in FieldsAndProperties)
                {
                    DelegateCalls.Add(CollectionOperations.CreatePair(TaggedUnionAlternativeResolver.ResolveProjector(Pair.Member, Pair.Type), new Expression[] { dParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                var CreateThis = Expression.New(RangeType);
                var Cases = new List<SwitchCase>();
                var n = 0;
                var EnumValues = TagMember.Type.GetEnumValues();
                foreach (var Pair in FieldsAndProperties.ZipStrict(Context.DelegateExpressions.Skip(1), (m, e) => new { Member = m.Member, MapperCall = e }))
                {
                    var EnumValue = Expression.Constant(EnumValues.GetValue(n), TagMember.Type);
                    var Init = Expression.MemberInit(CreateThis, new MemberBinding[] { Expression.Bind(TagMember.Member, EnumValue), Expression.Bind(Pair.Member, Pair.MapperCall) });
                    Cases.Add(Expression.SwitchCase(Init, EnumValue));
                    n += 1;
                }
                var DefaultCase = Expression.Block(Expression.Throw(Expression.New(typeof(InvalidOperationException))), CreateThis);
                var SelectCase = Expression.Switch(Context.DelegateExpressions[0], DefaultCase, Cases.ToArray());
                var FunctionLambda = Expression.Lambda(SelectCase, new ParameterExpression[] { dParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private Delegate TryResolveTuple(Type RangeType)
        {
            var DomainType = typeof(D);
            if (RangeType.IsValueType || RangeType.IsClass)
            {
                FieldOrPropertyInfo[] FieldsAndProperties = null;
                ConstructorInfo Constructor = null;
                if (FieldsAndProperties == null)
                {
                    var iri = RangeType.TryGetImmutableTupleInfo();
                    if (iri != null)
                    {
                        FieldsAndProperties = iri.Members;
                        Constructor = iri.Constructor;
                    }
                }
                if (FieldsAndProperties == null)
                {
                    var mri = RangeType.TryGetMutableTupleInfo();
                    if (mri != null) FieldsAndProperties = mri.Members;
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                var n = 0;
                foreach (var Pair in FieldsAndProperties)
                {
                    DelegateCalls.Add(CollectionOperations.CreatePair(TupleElementResolver.ResolveProjector(Pair.Member, n, Pair.Type), new Expression[] { dParam }));
                    n += 1;
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                if (Constructor != null)
                {
                    var CreateThis = Expression.New(Constructor, Context.DelegateExpressions);
                    var FunctionLambda = Expression.Lambda(CreateThis, new ParameterExpression[] { dParam });

                    return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
                }
                else
                {
                    var CreateThis = Expression.New(RangeType);
                    var MemberBindings = new List<MemberBinding>();
                    foreach (var Pair in FieldsAndProperties.ZipStrict(Context.DelegateExpressions, (m, e) => new { Member = m.Member, MapperCall = e }))
                    {
                        MemberBindings.Add(Expression.Bind(Pair.Member, Pair.MapperCall));
                    }
                    var FunctionLambda = Expression.Lambda(Expression.MemberInit(CreateThis, MemberBindings.ToArray()), new ParameterExpression[] { dParam });

                    return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
                }
            }
            return null;
        }

        private Delegate TryResolveRecord(Type RangeType)
        {
            var DomainType = typeof(D);
            if (RangeType.IsValueType || RangeType.IsClass)
            {
                FieldOrPropertyInfo[] FieldsAndProperties = null;
                ConstructorInfo Constructor = null;
                if (FieldsAndProperties == null)
                {
                    var iri = RangeType.TryGetImmutableRecordInfo();
                    if (iri != null)
                    {
                        FieldsAndProperties = iri.Members;
                        Constructor = iri.Constructor;
                    }
                }
                if (FieldsAndProperties == null)
                {
                    var mri = RangeType.TryGetMutableRecordInfo();
                    if (mri != null) FieldsAndProperties = mri.Members;
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                foreach (var Pair in FieldsAndProperties)
                {
                    DelegateCalls.Add(CollectionOperations.CreatePair(FieldResolver.ResolveProjector(Pair.Member, Pair.Type), new Expression[] { dParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);

                if (Constructor != null)
                {
                    var CreateThis = Expression.New(Constructor, Context.DelegateExpressions);
                    var FunctionLambda = Expression.Lambda(CreateThis, new ParameterExpression[] { dParam });

                    return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
                }
                else
                {
                    var CreateThis = Expression.New(RangeType);
                    var MemberBindings = new List<MemberBinding>();
                    foreach (var Pair in FieldsAndProperties.ZipStrict(Context.DelegateExpressions, (m, e) => new { Member = m.Member, MapperCall = e }))
                    {
                        MemberBindings.Add(Expression.Bind(Pair.Member, Pair.MapperCall));
                    }
                    var FunctionLambda = Expression.Lambda(Expression.MemberInit(CreateThis, MemberBindings.ToArray()), new ParameterExpression[] { dParam });

                    return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
                }
            }
            return null;
        }

        private IFieldProjectorResolver<D> FieldResolver;
        private IAliasFieldProjectorResolver<D> AliasFieldResolver;
        private ITagProjectorResolver<D> TagResolver;
        private ITaggedUnionAlternativeProjectorResolver<D> TaggedUnionAlternativeResolver;
        private ITupleElementProjectorResolver<D> TupleElementResolver;
        public RecordUnpackerTemplate(IFieldProjectorResolver<D> FieldResolver, IAliasFieldProjectorResolver<D> AliasFieldResolver, ITagProjectorResolver<D> TagResolver, ITaggedUnionAlternativeProjectorResolver<D> TaggedUnionAlternativeResolver, ITupleElementProjectorResolver<D> TupleElementResolver)
        {
            this.FieldResolver = FieldResolver;
            this.AliasFieldResolver = AliasFieldResolver;
            this.TagResolver = TagResolver;
            this.TaggedUnionAlternativeResolver = TaggedUnionAlternativeResolver;
            this.TupleElementResolver = TupleElementResolver;
        }
        public RecordUnpackerTemplate(IFieldProjectorResolver<D> FieldResolver)
        {
            this.FieldResolver = FieldResolver;
            this.AliasFieldResolver = new AliasFieldProjectorTranslatorResolver<D>(FieldResolver);
            this.TagResolver = new TagProjectorTranslatorResolver<D>(FieldResolver);
            this.TaggedUnionAlternativeResolver = new TaggedUnionAlternativeProjectorTranslatorResolver<D>(FieldResolver);
            this.TupleElementResolver = new TupleElementProjectorTranslatorResolver<D>(FieldResolver);
        }
        public RecordUnpackerTemplate(IProjectorResolver InnerResolver)
        {
            this.FieldResolver = new FieldProjectorResolver<D>(InnerResolver);
            this.AliasFieldResolver = new AliasFieldProjectorResolver<D>(InnerResolver);
            this.TagResolver = new TagProjectorResolver<D>(InnerResolver);
            this.TaggedUnionAlternativeResolver = new TaggedUnionAlternativeProjectorResolver<D>(InnerResolver);
            this.TupleElementResolver = new TupleElementProjectorResolver<D>(InnerResolver);
        }
    }
    [DebuggerNonUserCode()]
    public class RecordPackerTemplate<R> : IAggregatorResolver
    {
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (RangeType != typeof(R)) return null;
            {
                var d = TryResolveAlias(DomainType);
                if (d != null) return d;
            }
            {
                var d = TryResolveTaggedUnion(DomainType);
                if (d != null) return d;
            }
            {
                var d = TryResolveTuple(DomainType);
                if (d != null) return d;
            }
            {
                var d = TryResolveRecord(DomainType);
                if (d != null) return d;
            }
            return null;
        }

        private Delegate TryResolveAlias(Type DomainType)
        {
            var RangeType = typeof(R);
            if (DomainType.IsValueType || DomainType.IsClass)
            {
                if (!(DomainType.IsValueType || DomainType.IsClass)) return null;

                FieldOrPropertyInfo[] FieldsAndProperties = null;
                if (FieldsAndProperties == null)
                {
                    var mri = DomainType.TryGetMutableAliasInfo();
                    if (mri != null) FieldsAndProperties = new[] { mri.Member };
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var rParam = Expression.Parameter(RangeType, "Value");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                foreach (var Pair in FieldsAndProperties)
                {
                    var FieldOrPropertyExpr = MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, Pair.Member);
                    DelegateCalls.Add(CollectionOperations.CreatePair(AliasFieldResolver.ResolveAggregator(Pair.Member, Pair.Type), new Expression[] { FieldOrPropertyExpr, rParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);
                Expression Body;
                if (DelegateCalls.Count > 0)
                {
                    Body = Expression.Block(Context.DelegateExpressions);
                }
                else
                {
                    Body = Expression.Empty();
                }
                var FunctionLambda = Expression.Lambda(typeof(Action<,>).MakeGenericType(DomainType, RangeType), Body, new ParameterExpression[] { dParam, rParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private Delegate TryResolveTaggedUnion(Type DomainType)
        {
            var RangeType = typeof(R);
            if (DomainType.IsValueType || DomainType.IsClass)
            {
                if (!(DomainType.IsValueType || DomainType.IsClass)) return null;

                FieldOrPropertyInfo[] FieldsAndProperties = null;
                FieldOrPropertyInfo TagMember = null;
                if (FieldsAndProperties == null)
                {
                    var mri = DomainType.TryGetMutableTaggedUnionInfo();
                    if (mri != null)
                    {
                        FieldsAndProperties = mri.Members;
                        TagMember = mri.TagMember;
                    }
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var rParam = Expression.Parameter(RangeType, "Value");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                var TagMemberExpr = MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, TagMember.Member);
                DelegateCalls.Add(CollectionOperations.CreatePair(TagResolver.ResolveAggregator(TagMember.Member, TagMember.Type), new Expression[] { TagMemberExpr, rParam }));
                foreach (var Pair in FieldsAndProperties)
                {
                    var FieldOrPropertyExpr = MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, Pair.Member);
                    DelegateCalls.Add(CollectionOperations.CreatePair(TaggedUnionAlternativeResolver.ResolveAggregator(Pair.Member, Pair.Type), new Expression[] { FieldOrPropertyExpr, rParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);
                var Cases = new List<SwitchCase>();
                var n = 0;
                var EnumValues = TagMember.Type.GetEnumValues();
                foreach (var Pair in FieldsAndProperties.ZipStrict(Context.DelegateExpressions.Skip(1), (m, e) => new { Member = m.Member, MapperCall = e }))
                {
                    var EnumValue = Expression.Constant(EnumValues.GetValue(n), TagMember.Type);
                    Cases.Add(Expression.SwitchCase(Pair.MapperCall, EnumValue));
                    n += 1;
                }
                var Body = Expression.Block(Context.DelegateExpressions[0], Expression.Switch(MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, TagMember.Member), Cases.ToArray()));
                var FunctionLambda = Expression.Lambda(typeof(Action<,>).MakeGenericType(DomainType, RangeType), Body, new ParameterExpression[] { dParam, rParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private Delegate TryResolveTuple(Type DomainType)
        {
            var RangeType = typeof(R);
            if (DomainType.IsValueType || DomainType.IsClass)
            {
                if (!(DomainType.IsValueType || DomainType.IsClass)) return null;

                FieldOrPropertyInfo[] FieldsAndProperties = null;
                if (FieldsAndProperties == null)
                {
                    var iri = DomainType.TryGetImmutableTupleInfo();
                    if (iri != null) FieldsAndProperties = iri.Members;
                }
                if (FieldsAndProperties == null)
                {
                    var mri = DomainType.TryGetMutableTupleInfo();
                    if (mri != null) FieldsAndProperties = mri.Members;
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var rParam = Expression.Parameter(RangeType, "Value");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                var n = 0;
                foreach (var Pair in FieldsAndProperties)
                {
                    var FieldOrPropertyExpr = MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, Pair.Member);
                    DelegateCalls.Add(CollectionOperations.CreatePair(TupleElementResolver.ResolveAggregator(Pair.Member, n, Pair.Type), new Expression[] { FieldOrPropertyExpr, rParam }));
                    n += 1;
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);
                Expression Body;
                if (DelegateCalls.Count > 0)
                {
                    Body = Expression.Block(Context.DelegateExpressions);
                }
                else
                {
                    Body = Expression.Empty();
                }
                var FunctionLambda = Expression.Lambda(typeof(Action<,>).MakeGenericType(DomainType, RangeType), Body, new ParameterExpression[] { dParam, rParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private Delegate TryResolveRecord(Type DomainType)
        {
            var RangeType = typeof(R);
            if (DomainType.IsValueType || DomainType.IsClass)
            {
                if (!(DomainType.IsValueType || DomainType.IsClass)) return null;

                FieldOrPropertyInfo[] FieldsAndProperties = null;
                if (FieldsAndProperties == null)
                {
                    var iri = DomainType.TryGetImmutableRecordInfo();
                    if (iri != null) FieldsAndProperties = iri.Members;
                }
                if (FieldsAndProperties == null)
                {
                    var mri = DomainType.TryGetMutableRecordInfo();
                    if (mri != null) FieldsAndProperties = mri.Members;
                }
                if (FieldsAndProperties == null) return null;

                var dParam = Expression.Parameter(DomainType, "Key");
                var rParam = Expression.Parameter(RangeType, "Value");
                var DelegateCalls = new List<KeyValuePair<Delegate, Expression[]>>();
                foreach (var Pair in FieldsAndProperties)
                {
                    var FieldOrPropertyExpr = MetaProgramming.MetaProgramming.CreateFieldOrPropertyExpression(dParam, Pair.Member);
                    DelegateCalls.Add(CollectionOperations.CreatePair(FieldResolver.ResolveAggregator(Pair.Member, Pair.Type), new Expression[] { FieldOrPropertyExpr, rParam }));
                }
                var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(DelegateCalls);
                Expression Body;
                if (DelegateCalls.Count > 0)
                {
                    Body = Expression.Block(Context.DelegateExpressions);
                }
                else
                {
                    Body = Expression.Empty();
                }
                var FunctionLambda = Expression.Lambda(typeof(Action<,>).MakeGenericType(DomainType, RangeType), Body, new ParameterExpression[] { dParam, rParam });

                return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
            }
            return null;
        }

        private IFieldAggregatorResolver<R> FieldResolver;
        private IAliasFieldAggregatorResolver<R> AliasFieldResolver;
        private ITagAggregatorResolver<R> TagResolver;
        private ITaggedUnionAlternativeAggregatorResolver<R> TaggedUnionAlternativeResolver;
        private ITupleElementAggregatorResolver<R> TupleElementResolver;
        public RecordPackerTemplate(IFieldAggregatorResolver<R> FieldResolver, IAliasFieldAggregatorResolver<R> AliasFieldResolver, ITagAggregatorResolver<R> TagResolver, ITaggedUnionAlternativeAggregatorResolver<R> TaggedUnionAlternativeResolver, ITupleElementAggregatorResolver<R> TupleElementResolver)
        {
            this.FieldResolver = FieldResolver;
            this.AliasFieldResolver = AliasFieldResolver;
            this.TagResolver = TagResolver;
            this.TaggedUnionAlternativeResolver = TaggedUnionAlternativeResolver;
            this.TupleElementResolver = TupleElementResolver;
        }
        public RecordPackerTemplate(IFieldAggregatorResolver<R> FieldResolver)
        {
            this.FieldResolver = FieldResolver;
            this.AliasFieldResolver = new AliasFieldAggregatorTranslatorResolver<R>(FieldResolver);
            this.TagResolver = new TagAggregatorTranslatorResolver<R>(FieldResolver);
            this.TaggedUnionAlternativeResolver = new TaggedUnionAlternativeAggregatorTranslatorResolver<R>(FieldResolver);
            this.TupleElementResolver = new TupleElementAggregatorTranslatorResolver<R>(FieldResolver);
        }
        public RecordPackerTemplate(IAggregatorResolver InnerResolver)
        {
            this.FieldResolver = new FieldAggregatorResolver<R>(InnerResolver);
            this.AliasFieldResolver = new AliasFieldAggregatorResolver<R>(InnerResolver);
            this.TagResolver = new TagAggregatorResolver<R>(InnerResolver);
            this.TaggedUnionAlternativeResolver = new TaggedUnionAlternativeAggregatorResolver<R>(InnerResolver);
            this.TupleElementResolver = new TupleElementAggregatorResolver<R>(InnerResolver);
        }
    }

    [DebuggerNonUserCode()]
    public class FieldProjectorResolver<D> : IFieldProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), Type));
        }

        private IProjectorResolver InnerResolver;
        public FieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class FieldAggregatorResolver<R> : IFieldAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(Type, typeof(R)));
        }

        private IAggregatorResolver InnerResolver;
        public FieldAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class AliasFieldProjectorResolver<D> : IAliasFieldProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), Type));
        }

        private IProjectorResolver InnerResolver;
        public AliasFieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class AliasFieldAggregatorResolver<R> : IAliasFieldAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(Type, typeof(R)));
        }

        private IAggregatorResolver InnerResolver;
        public AliasFieldAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TagProjectorResolver<D> : ITagProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type TagType)
        {
            return InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), TagType));
        }

        private IProjectorResolver InnerResolver;
        public TagProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TagAggregatorResolver<R> : ITagAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type TagType)
        {
            return InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(TagType, typeof(R)));
        }

        private IAggregatorResolver InnerResolver;
        public TagAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TaggedUnionAlternativeProjectorResolver<D> : ITaggedUnionAlternativeProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), Type));
        }

        private IProjectorResolver InnerResolver;
        public TaggedUnionAlternativeProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TaggedUnionAlternativeAggregatorResolver<R> : ITaggedUnionAlternativeAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(Type, typeof(R)));
        }

        private IAggregatorResolver InnerResolver;
        public TaggedUnionAlternativeAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TupleElementProjectorResolver<D> : ITupleElementProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, int Index, Type Type)
        {
            return InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), Type));
        }

        private IProjectorResolver InnerResolver;
        public TupleElementProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TupleElementAggregatorResolver<R> : ITupleElementAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, int Index, Type Type)
        {
            return InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(Type, typeof(R)));
        }

        private IAggregatorResolver InnerResolver;
        public TupleElementAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class AliasFieldProjectorTranslatorResolver<D> : IAliasFieldProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveProjector(Member, Type);
        }

        private IFieldProjectorResolver<D> InnerResolver;
        public AliasFieldProjectorTranslatorResolver(IFieldProjectorResolver<D> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class AliasFieldAggregatorTranslatorResolver<R> : IAliasFieldAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveAggregator(Member, Type);
        }

        private IFieldAggregatorResolver<R> InnerResolver;
        public AliasFieldAggregatorTranslatorResolver(IFieldAggregatorResolver<R> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TagProjectorTranslatorResolver<D> : ITagProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type TagType)
        {
            return InnerResolver.ResolveProjector(Member, TagType);
        }

        private IFieldProjectorResolver<D> InnerResolver;
        public TagProjectorTranslatorResolver(IFieldProjectorResolver<D> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TagAggregatorTranslatorResolver<R> : ITagAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type TagType)
        {
            return InnerResolver.ResolveAggregator(Member, TagType);
        }

        private IFieldAggregatorResolver<R> InnerResolver;
        public TagAggregatorTranslatorResolver(IFieldAggregatorResolver<R> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TaggedUnionAlternativeProjectorTranslatorResolver<D> : ITaggedUnionAlternativeProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveProjector(Member, Type);
        }

        private IFieldProjectorResolver<D> InnerResolver;
        public TaggedUnionAlternativeProjectorTranslatorResolver(IFieldProjectorResolver<D> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TaggedUnionAlternativeAggregatorTranslatorResolver<R> : ITaggedUnionAlternativeAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            return InnerResolver.ResolveAggregator(Member, Type);
        }

        private IFieldAggregatorResolver<R> InnerResolver;
        public TaggedUnionAlternativeAggregatorTranslatorResolver(IFieldAggregatorResolver<R> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    [DebuggerNonUserCode()]
    public class TupleElementProjectorTranslatorResolver<D> : ITupleElementProjectorResolver<D>
    {
        public Delegate ResolveProjector(MemberInfo Member, int Index, Type Type)
        {
            return InnerResolver.ResolveProjector(Member, Type);
        }

        private IFieldProjectorResolver<D> InnerResolver;
        public TupleElementProjectorTranslatorResolver(IFieldProjectorResolver<D> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    [DebuggerNonUserCode()]
    public class TupleElementAggregatorTranslatorResolver<R> : ITupleElementAggregatorResolver<R>
    {
        public Delegate ResolveAggregator(MemberInfo Member, int Index, Type Type)
        {
            return InnerResolver.ResolveAggregator(Member, Type);
        }

        private IFieldAggregatorResolver<R> InnerResolver;
        public TupleElementAggregatorTranslatorResolver(IFieldAggregatorResolver<R> Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
}
