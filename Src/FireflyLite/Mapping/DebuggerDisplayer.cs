using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaProgramming;

namespace Firefly.Mapping.MetaSchema
{
    public sealed class DebuggerDisplayer
    {
        private DebuggerDisplayer()
        {
        }

        private static Displayer d = new Displayer();
        public static string ConvertToString<T>(T v)
        {
            var Type = typeof(T);
            var m = (Func<T, string>)d.ResolveProjector(CollectionOperations.CreatePair(Type, typeof(string)));
            return m(v);
        }

        private class Displayer : IMapperResolver
        {
            private IMapperResolver Resolver;

            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                return Resolver.TryResolveProjector(TypePair);
            }
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                return Resolver.TryResolveAggregator(TypePair);
            }

            public Displayer()
            {
                var Root = new ReferenceMapperResolver();
                Resolver = Root;
                var ProjectorResolverList = new List<IProjectorResolver>(new IProjectorResolver[] {
                    new PrimitiveStringResolver(),
                    new NullableStringResolver(Root.AsRuntimeDomainNoncircular()),
                    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new StringAggregatorToProjectorRangeTranslator())
                });
                var AggregatorResolverList = new List<IAggregatorResolver>(new IAggregatorResolver[] {
                    new CollectionPackerTemplate<PackerState>(new CollectionPacker(Root.AsRuntimeDomainNoncircular())),
                    new RecordPackerTemplate<PackerState>(
                        new FieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                        new AliasFieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                        new TagAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                        new TaggedUnionAlternativeAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                        new TupleElementAggregatorResolver(Root.AsRuntimeDomainNoncircular())
                    ),
                    TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new StringProjectorToAggregatorRangeTranslator())
                });
                Root.Inner = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), AggregatorResolverList.Concatenated());
            }
        }

        public class CollectionPacker : IGenericCollectionAggregatorResolver<PackerState>
        {
            public Action<DCollection, PackerState> ResolveAggregator<D, DCollection>() where DCollection : ICollection<D>
            {
                var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
                Action<DCollection, PackerState> F = (c, Value) =>
                {
                    foreach (var v in c)
                    {
                        Value.List.Add(Mapper(v));
                    }
                    Value.NoName = true;
                };
                return F;
            }

            private IProjectorResolver InnerResolver;
            public CollectionPacker(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class PackerState
        {
            public List<string> List;
            public bool NoBraces;
            public bool NoName;
        }

        public class StringAggregatorToProjectorRangeTranslator : IAggregatorToProjectorRangeTranslator<string, PackerState>
        {
            private int CallStackDepth = 0;
            public Func<D, string> TranslateAggregatorToProjectorRange<D>(Action<D, PackerState> Aggregator)
            {
                var Name = typeof(D).Name;
                return v =>
                {
                    CallStackDepth += 1;
                    try
                    {
                        if (v == null) return "$Empty";
                        if (CallStackDepth >= 5) return "..";
                        var s = new PackerState { List = new List<string>(), NoBraces = false, NoName = false };
                        Aggregator(v, s);
                        if (s.NoBraces)
                        {
                            if (s.NoName)
                            {
                                return s.List.Single();
                            }
                            else
                            {
                                throw new InvalidOperationException();
                            }
                        }
                        else
                        {
                            if (s.NoName)
                            {
                                return "{" + string.Join(", ", s.List.ToArray()) + "}";
                            }
                            else
                            {
                                return Name + "{" + string.Join(", ", s.List.ToArray()) + "}";
                            }
                        }
                    }
                    finally
                    {
                        CallStackDepth -= 1;
                    }
                };
            }
        }

        public class StringProjectorToAggregatorRangeTranslator : IProjectorToAggregatorRangeTranslator<PackerState, string>
        {
            public Action<D, PackerState> TranslateProjectorToAggregatorRange<D>(Func<D, string> Projector)
            {
                return (v, s) => s.List.Add(Projector(v));
            }
        }

        /// <remarks>基元解析器</remarks>
        public class PrimitiveStringResolver : IMapperResolver
        {
            private static string ConvertStringToString(string v)
            {
                if (v == null) return "$Empty";
                return "\"" + v + "\"";
            }

            private static string ConvertToString<D>(D v)
            {
                if (v == null)
                {
                    return "$Empty";
                }
                else
                {
                    return v.ToString();
                }
            }

            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (TypePair.Value == typeof(string))
                {
                    if (TypePair.Key == typeof(string)) return (Func<string, string>)ConvertStringToString;
                    if (TypePair.Key.IsPrimitive)
                    {
                        return ((Func<DummyType, string>)ConvertToString<DummyType>).MakeDelegateMethodFromDummy(TypePair.Key);
                    }
                }
                return null;
            }
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                return null;
            }
        }

        /// <remarks>可空解析器</remarks>
        public class NullableStringResolver : IMapperResolver
        {
            private class Converter
            {
                public Delegate CallDelegate;

                public string ConvertToString<D>(D? v) where D : struct
                {
                    if (v == null)
                    {
                        return "$Empty";
                    }
                    else
                    {
                        return ((Func<D, string>)CallDelegate)((D)v);
                    }
                }
            }
            private static Delegate GetConvertToStringFunc<D>(Delegate m) where D : struct
            {
                var c = new Converter { CallDelegate = m };
                return (Func<D?, string>)c.ConvertToString<D>;
            }
            public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
            {
                if (TypePair.Value == typeof(string))
                {
                    var Domain = TypePair.Key;
                    if (Domain.IsGenericType && !Domain.IsGenericTypeDefinition)
                    {
                        if (Domain.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            var UnderlyingDomain = Domain.GetGenericArguments()[0];
                            var m = InnerResolver.TryResolveProjector(CollectionOperations.CreatePair(UnderlyingDomain, TypePair.Value));
                            if (m == null) return null;
                            var md = (Func<Delegate, Delegate>)GetConvertToStringFunc<int>;
                            var d = (Func<Delegate, Delegate>)md.MakeDelegateMethod(new[] { UnderlyingDomain }, typeof(Func<Delegate, Delegate>));
                            return d(m);
                        }
                    }
                }
                return null;
            }
            public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
            {
                return null;
            }

            private IProjectorResolver InnerResolver;
            public NullableStringResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class FieldAggregatorResolver : IFieldAggregatorResolver<PackerState>
        {
            private Action<D, PackerState> Resolve<D>(string Name)
            {
                Action<D, PackerState> F = (k, s) =>
                {
                    var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
                    if (k == null)
                    {
                        var e = "$Empty";
                        s.List.Add(Name + " = " + e);
                    }
                    else
                    {
                        var e = Mapper(k);
                        s.List.Add(Name + " = " + e);
                    }
                };
                return F;
            }

            private Dictionary<Type, Func<string, Delegate>> Dict = new Dictionary<Type, Func<string, Delegate>>();
            public Delegate ResolveAggregator(MemberInfo Member, Type Type)
            {
                var Name = Member.Name;
                if (Dict.ContainsKey(Type))
                {
                    var m = Dict[Type];
                    return m(Name);
                }
                else
                {
                    var GenericMapper = (Func<string, Action<DummyType, PackerState>>)Resolve<DummyType>;
                    var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<string, Delegate>();
                    Dict.Add(Type, m);
                    return m(Name);
                }
            }

            private IProjectorResolver InnerResolver;
            public FieldAggregatorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class AliasFieldAggregatorResolver : IAliasFieldAggregatorResolver<PackerState>
        {
            private Action<D, PackerState> Resolve<D>()
            {
                Action<D, PackerState> F = (k, s) =>
                {
                    var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
                    if (k == null)
                    {
                        s.List.Add("$Empty");
                    }
                    else
                    {
                        s.List.Add(Mapper(k));
                    }
                    s.NoBraces = true;
                    s.NoName = true;
                };
                return F;
            }

            public Delegate ResolveAggregator(MemberInfo Member, Type Type)
            {
                var GenericMapper = (Func<Action<DummyType, PackerState>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<Delegate>();
                return m();
            }

            private IProjectorResolver InnerResolver;
            public AliasFieldAggregatorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class TagAggregatorResolver : ITagAggregatorResolver<PackerState>
        {
            private Action<D, PackerState> Resolve<D>()
            {
                Action<D, PackerState> F = (k, s) => { };
                return F;
            }

            public Delegate ResolveAggregator(MemberInfo Member, Type TagType)
            {
                var GenericMapper = (Func<Action<DummyType, PackerState>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(TagType).AdaptFunction<Delegate>();
                return m();
            }

            private IProjectorResolver InnerResolver;
            public TagAggregatorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class TaggedUnionAlternativeAggregatorResolver : ITaggedUnionAlternativeAggregatorResolver<PackerState>
        {
            private Action<D, PackerState> Resolve<D>(string Name)
            {
                Action<D, PackerState> F = (k, s) =>
                {
                    var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
                    if (k == null)
                    {
                        var e = "$Empty";
                        s.List.Add(Name + "{" + e + "}");
                    }
                    else
                    {
                        var e = Mapper(k);
                        s.List.Add(Name + "{" + e + "}");
                    }
                    s.NoBraces = true;
                    s.NoName = true;
                };
                return F;
            }

            private Dictionary<Type, Func<string, Delegate>> Dict = new Dictionary<Type, Func<string, Delegate>>();
            public Delegate ResolveAggregator(MemberInfo Member, Type Type)
            {
                var Name = Member.Name;
                if (Dict.ContainsKey(Type))
                {
                    var m = Dict[Type];
                    return m(Name);
                }
                else
                {
                    var GenericMapper = (Func<string, Action<DummyType, PackerState>>)Resolve<DummyType>;
                    var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<string, Delegate>();
                    Dict.Add(Type, m);
                    return m(Name);
                }
            }

            private IProjectorResolver InnerResolver;
            public TaggedUnionAlternativeAggregatorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class TupleElementAggregatorResolver : ITupleElementAggregatorResolver<PackerState>
        {
            private Action<D, PackerState> Resolve<D>(int Index)
            {
                Action<D, PackerState> F = (k, s) =>
                {
                    var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
                    if (k == null)
                    {
                        s.List.Add("$Empty");
                    }
                    else
                    {
                        s.List.Add(Mapper(k));
                    }
                    s.NoName = true;
                };
                return F;
            }

            private Dictionary<Type, Func<int, Delegate>> Dict = new Dictionary<Type, Func<int, Delegate>>();
            public Delegate ResolveAggregator(MemberInfo Member, int Index, Type Type)
            {
                if (Dict.ContainsKey(Type))
                {
                    var m = Dict[Type];
                    return m(Index);
                }
                else
                {
                    var GenericMapper = (Func<int, Action<DummyType, PackerState>>)Resolve<DummyType>;
                    var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<int, Delegate>();
                    Dict.Add(Type, m);
                    return m(Index);
                }
            }

            private IProjectorResolver InnerResolver;
            public TupleElementAggregatorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }
    }
}
