using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Reflection;
using Firefly;
using Firefly.Mapping.MetaProgramming;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Syntax = Firefly.Texting.TreeFormat.Syntax;

namespace Firefly.Mapping.TreeText
{
    public interface ITreeReader
    {
        T Read<T>(Forest s);
        KeyValuePair<T, Dictionary<object, Syntax.FileTextRange>> Read<T>(KeyValuePair<Forest, Dictionary<object, Syntax.FileTextRange>> s);
    }
    public interface ITreeWriter
    {
        Forest Write<T>(T Value);
        KeyValuePair<Forest, Dictionary<object, object>> Write<T>(KeyValuePair<T, Dictionary<object, object>> Value);
    }
    public interface ITreeSerializer : ITreeReader, ITreeWriter
    {
    }

    /// <remarks>
    /// 对于非简单类型，应提供自定义序列化器
    /// 简单类型 ::= 简单类型
    ///           | Byte(UInt8) | UInt16 | UInt32 | UInt64 | Int8(SByte) | Int16 | Int32 | Int64 | Float32(Single) | Float64(Double)
    ///           | Boolean
    ///           | String | Decimal
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
    /// 对于类对象，允许出现null。
    /// </remarks>
    public class TreeSerializer : ITreeSerializer
    {
        private TreeReaderResolver ReaderResolver;
        private TreeWriterResolver WriterResolver;

        private IMapperResolver ReaderCache;
        private IMapperResolver WriterCache;

        public TreeSerializer() : this(true)
        {
        }
        public TreeSerializer(bool UseByteArrayAndListTranslator)
        {
            var ReaderReference = new ReferenceMapperResolver();
            ReaderCache = ReaderReference;
            ReaderResolver = new TreeReaderResolver(ReaderReference);
            ReaderReference.Inner = ReaderResolver.AsCached();

            var WriterReference = new ReferenceMapperResolver();
            WriterCache = WriterReference;
            WriterResolver = new TreeWriterResolver(WriterReference);
            WriterReference.Inner = WriterResolver.AsCached();

            if (UseByteArrayAndListTranslator)
            {
                var bat = new ByteArrayTranslator();
                PutReaderTranslator((IProjectorToProjectorRangeTranslator<byte[], string>)bat);
                PutWriterTranslator((IProjectorToProjectorDomainTranslator<byte[], string>)bat);
                var blt = new ByteListTranslator();
                PutReaderTranslator((IProjectorToProjectorRangeTranslator<List<byte>, string>)blt);
                PutWriterTranslator((IProjectorToProjectorDomainTranslator<List<byte>, string>)blt);
            }
        }

        public void PutReader<T>(Func<string, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Func<T, string> Writer)
        {
            WriterResolver.PutWriter(Writer);
        }
        public void PutReader<T>(Func<NodeContext, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Func<T, Node> Writer)
        {
            WriterResolver.PutWriter(Writer);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<NodeContext, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<Node, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }

        public T Read<T>(Forest s)
        {
            return Read<T>(new KeyValuePair<Forest, Dictionary<object, Syntax.FileTextRange>>(s, new Dictionary<object, Syntax.FileTextRange>())).Key;
        }
        public KeyValuePair<T, Dictionary<object, Syntax.FileTextRange>> Read<T>(KeyValuePair<Forest, Dictionary<object, Syntax.FileTextRange>> s)
        {
            var TargetPositions = new Dictionary<object, Syntax.FileTextRange>();
            var m = ReaderCache.ResolveProjector<NodeContext, T>();
            var Result = m(new NodeContext { Value = s.Key.Nodes.Single(), SourcePositions = s.Value, TargetPositions = TargetPositions });
            return new KeyValuePair<T, Dictionary<object, Syntax.FileTextRange>>(Result, TargetPositions);
        }
        public Forest Write<T>(T Value)
        {
            return Write<T>(new KeyValuePair<T, Dictionary<object, object>>(Value, new Dictionary<object, object>())).Key;
        }
        public KeyValuePair<Forest, Dictionary<object, object>> Write<T>(KeyValuePair<T, Dictionary<object, object>> Value)
        {
            var TargetPositions = new Dictionary<object, object>();
            var m = WriterCache.ResolveProjector<Context<T>, Node>();
            var Result = m(new Context<T> { Value = Value.Key, SourceMappings = Value.Value, TargetMappings = TargetPositions });
            return new KeyValuePair<Forest, Dictionary<object, object>>(new Forest { Nodes = new List<Node> { Result } }, TargetPositions);
        }

        public Node CurrentReadingNode
        {
            get { return ReaderResolver.CurrentReadingNode; }
        }
    }

    public class TreeReaderResolver : IMapperResolver
    {
        private IMapperResolver Root;
        private PrimitiveResolver PrimitiveResolver;
        private IMapperResolver Resolver;
        private LinkedList<IProjectorResolver> ProjectorResolverList;
        private DebugReaderResolver DebugResolver;

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveProjector(TypePair);
        }
        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            return Resolver.TryResolveAggregator(TypePair);
        }

        public TreeReaderResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutReader((string s) => NumericStrings.InvariantParseUInt8(s));
            PutReader((string s) => NumericStrings.InvariantParseUInt16(s));
            PutReader((string s) => NumericStrings.InvariantParseUInt32(s));
            PutReader((string s) => NumericStrings.InvariantParseUInt64(s));
            PutReader((string s) => NumericStrings.InvariantParseInt8(s));
            PutReader((string s) => NumericStrings.InvariantParseInt16(s));
            PutReader((string s) => NumericStrings.InvariantParseInt32(s));
            PutReader((string s) => NumericStrings.InvariantParseInt64(s));
            PutReader((string s) => NumericStrings.InvariantParseFloat32(s));
            PutReader((string s) => NumericStrings.InvariantParseFloat64(s));
            PutReader((string s) => NumericStrings.InvariantParseBoolean(s));
            PutReader((string s) => s);
            PutReader((string s) => NumericStrings.InvariantParseDecimal(s));

            //Reader
            //proj <- proj
            //PrimitiveResolver: (String|NodeContext proj Primitive) <- null
            //EnumResolver: (String proj Enum) <- null
            //ContextToStringDomainTranslator: (NodeContext proj R) <- (String proj R)
            //CollectionUnpacker: (Context proj {R}) <- (NodeContext.SubElement proj R)
            //FieldOrPropertyProjectorResolver: (Dictionary(String, Context) proj R) <- (NodeContext.SubElement proj R.Field)
            //ContextProjectorToProjectorDomainTranslator: (NodeContext proj R) <- (Dictionary(String, Context) proj R)

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                new EnumResolver(),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new ContextToStringDomainTranslator()),
                new CollectionUnpackerTemplate<NodeContext>(new CollectionUnpacker(Root.AsRuntimeDomainNoncircular())),
                new RecordUnpackerTemplate<ElementUnpackerState>(
                    new FieldProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new AliasFieldProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TagProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TaggedUnionAlternativeProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TupleElementProjectorResolver(Root.AsRuntimeDomainNoncircular())
                ),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new ContextProjectorToProjectorDomainTranslator())
            });
            DebugResolver = new DebugReaderResolver(Mapping.CreateMapper(ProjectorResolverList.Concatenated(), Mapping.EmptyAggregatorResolver));
            Resolver = DebugResolver;
        }

        public void PutReader<T>(Func<string, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReader<T>(Func<NodeContext, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<NodeContext, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }

        public Node CurrentReadingNode
        {
            get { return DebugResolver.CurrentReadingNode; }
        }
    }

    public class TreeWriterResolver : IMapperResolver
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

        public TreeWriterResolver(IMapperResolver Root)
        {
            this.Root = Root;

            PrimitiveResolver = new PrimitiveResolver();

            PutWriter((byte b) => b.ToInvariantString());
            PutWriter((ushort i) => i.ToInvariantString());
            PutWriter((uint i) => i.ToInvariantString());
            PutWriter((ulong i) => i.ToInvariantString());
            PutWriter((sbyte i) => i.ToInvariantString());
            PutWriter((short i) => i.ToInvariantString());
            PutWriter((int i) => i.ToInvariantString());
            PutWriter((long i) => i.ToInvariantString());
            PutWriter((float f) => f.ToInvariantString());
            PutWriter((double f) => f.ToInvariantString());
            PutWriter((bool b) => b.ToInvariantString());
            PutWriter((string s) => s);
            PutWriter((decimal d) => d.ToInvariantString());

            //Writer
            //proj <- proj/aggr
            //PrimitiveResolver: (Primitive proj String|Node) <- null
            //EnumResolver: (Enum proj String) <- null
            //ContextDomainTranslatorProjectorResolver: (Context(D) proj String) <- (D proj String)
            //NodeToStringRangeTranslator: (Context(D) proj Node) <- (Context(D) proj String)
            //NodeAggregatorToProjectorRangeTranslator: (Context(D) proj Node) <- (Context(D) aggr List(Node))
            //
            //Writer
            //aggr <- proj/aggr
            //ContextDomainTranslatorAggregatorResolver: (Context(D) aggr List(Node)) <- (D aggr List(Node))
            //CollectionPacker: ({D} aggr Collection(Node)) <- (Context(D) proj Node)
            //FieldOrPropertyAggregatorResolver: (D aggr List(Node)) <- (Context(D.Field) proj Node)
            //NodeProjectorToAggregatorRangeTranslator: (Context(D) aggr List(Node)) <- (Context(D) proj Node)

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                new EnumResolver(),
                new ContextDomainTranslatorProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new NodeToStringRangeTranslator()),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new NodeAggregatorToProjectorRangeTranslator())
            });
            AggregatorResolverList = new LinkedList<IAggregatorResolver>(new IAggregatorResolver[] {
                new ContextDomainTranslatorAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                new CollectionPackerTemplate<ElementPackerState>(new CollectionPacker(Root.AsRuntimeDomainNoncircular())),
                new RecordPackerTemplate<ElementPackerState>(
                    new FieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new AliasFieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TagAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TaggedUnionAlternativeAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TupleElementAggregatorResolver(Root.AsRuntimeDomainNoncircular())
                ),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new NodeProjectorToAggregatorRangeTranslator())
            });
            Resolver = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), AggregatorResolverList.Concatenated());
        }

        public void PutWriter<T>(Func<T, string> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriter<T>(Func<T, Node> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<Node, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
    }

    public static class MappingTree
    {
        public static string GetTypeFriendlyName(Type Type)
        {
            if (Type.IsArray)
            {
                var n = Type.GetArrayRank();
                var ElementTypeName = GetTypeFriendlyName(Type.GetElementType());
                if (n == 1)
                {
                    return "ArrayOf" + ElementTypeName;
                }
                return "Array" + n + "Of" + ElementTypeName;
            }
            if (Type.IsGenericType)
            {
                var Name = Regex.Match(Type.Name, "^(?<Name>.*?)`.*$", RegexOptions.ExplicitCapture).Result("${Name}");
                return Name + "Of" + string.Join("And", (from t in Type.GetGenericArguments() select GetTypeFriendlyName(t)).ToArray());
            }
            return Type.Name;
        }
    }

    public class NodeContext
    {
        public Node Value;
        public Dictionary<object, Syntax.FileTextRange> SourcePositions;
        public Dictionary<object, Syntax.FileTextRange> TargetPositions;
    }
    public interface IContext
    {
        object Value { get; }
        Dictionary<object, object> SourceMappings { get; set; }
        Dictionary<object, object> TargetMappings { get; set; }
    }
    public class Context<T> : IContext
    {
        public T Value;
        object IContext.Value
        {
            get { return Value; }
        }

        public Dictionary<object, object> SourceMappings { get; set; }
        public Dictionary<object, object> TargetMappings { get; set; }
    }
    public class ElementUnpackerState
    {
        public NodeContext Parent;
        public List<NodeContext> List;
        public Dictionary<string, NodeContext> Dict;
    }
    public class ElementPackerState
    {
        public bool UseParent;
        public Node Parent;
        public List<Node> List;
        public Dictionary<object, object> SourceMappings;
        public Dictionary<object, object> TargetMappings;
    }

    public class EnumResolver : IProjectorResolver
    {
        public static R StringToEnum<R>(string s)
        {
            return (R)Enum.Parse(typeof(R), s);
        }
        public static string EnumToString<D>(D v)
        {
            return v.ToString();
        }

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType == typeof(string) && RangeType.IsEnum)
            {
                var DummyMethod = (Func<string, DummyType>)StringToEnum<DummyType>;
                var m = DummyMethod.MakeDelegateMethodFromDummy(RangeType);
                return m;
            }
            if (RangeType == typeof(string) && DomainType.IsEnum)
            {
                var DummyMethod = (Func<DummyType, string>)EnumToString<DummyType>;
                var m = DummyMethod.MakeDelegateMethodFromDummy(DomainType);
                return m;
            }
            return null;
        }
    }

    public class CollectionUnpacker : IGenericCollectionProjectorResolver<NodeContext>
    {
        public Func<NodeContext, RCollection> ResolveProjector<R, RCollection>() where RCollection : ICollection<R>, new()
        {
            var Mapper = (Func<NodeContext, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(NodeContext), typeof(R)));
            Func<NodeContext, RCollection> F = Key =>
            {
                if (Key.Value.OnEmpty) return default(RCollection);
                if (Key.Value.OnLeaf) throw new InvalidOperationException();
                if (Key.Value.OnStem)
                {
                    var List = new RCollection();
                    foreach (var k in Key.Value.Stem.Children)
                    {
                        List.Add(Mapper(new NodeContext { Value = k, SourcePositions = Key.SourcePositions, TargetPositions = Key.TargetPositions }));
                    }
                    return List;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };
            return F;
        }

        private IProjectorResolver InnerResolver;
        public CollectionUnpacker(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class CollectionPacker : IGenericCollectionAggregatorResolver<ElementPackerState>
    {
        public Action<DCollection, ElementPackerState> ResolveAggregator<D, DCollection>() where DCollection : ICollection<D>
        {
            var Mapper = (Func<Context<D>, Node>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(Context<D>), typeof(Node)));
            Action<DCollection, ElementPackerState> F = (c, Value) =>
            {
                var k = 0;
                foreach (var v in c)
                {
                    Value.List.Add(Mapper(new Context<D> { Value = v, SourceMappings = Value.SourceMappings, TargetMappings = Value.TargetMappings }));
                    k += 1;
                }
            };
            return F;
        }

        private IProjectorResolver InnerResolver;
        public CollectionPacker(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class NodeToStringRangeTranslator : IProjectorToProjectorRangeTranslator<Node, string>
    {
        public Func<D, Node> TranslateProjectorToProjectorRange<D>(Func<D, string> Projector)
        {
            var FriendlyName = MappingTree.GetTypeFriendlyName(typeof(D).GetGenericArguments().Single());
            return v =>
            {
                var c = (IContext)v;
                var s = Projector(v);
                var x = Node.CreateStem(new Stem { Name = FriendlyName, Children = new List<Node> { Node.CreateLeaf(s) } });
                if (c.SourceMappings.ContainsKey(c.Value))
                {
                    c.TargetMappings.Add(x, c.SourceMappings[c.Value]);
                }
                return x;
            };
        }
    }

    public class ContextToStringDomainTranslator : IProjectorToProjectorDomainTranslator<NodeContext, string>
    {
        public Func<NodeContext, R> TranslateProjectorToProjectorDomain<R>(Func<string, R> Projector)
        {
            return v =>
            {
                if (v.Value.OnEmpty) return default(R);
                if (v.Value.OnLeaf) throw new InvalidOperationException();
                var Element = v.Value.Stem.Children.Single();
                if (!Element.OnLeaf) throw new InvalidOperationException();
                return Projector(Element.Leaf);
            };
        }
    }

    public class ContextProjectorToProjectorDomainTranslator : IProjectorToProjectorDomainTranslator<NodeContext, ElementUnpackerState>
    {
        public Func<NodeContext, R> TranslateProjectorToProjectorDomain<R>(Func<ElementUnpackerState, R> Projector)
        {
            return Element =>
            {
                if (Element.Value.OnEmpty) return default(R);
                if (Element.Value.OnLeaf) throw new InvalidOperationException();
                if (Element.Value.OnStem)
                {
                    var l = new List<NodeContext>();
                    var d = new Dictionary<string, NodeContext>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in Element.Value.Stem.Children)
                    {
                        if (e.OnEmpty) continue;
                        if (!e.OnStem) throw new InvalidOperationException();
                        var LocalName = e.Stem.Name;
                        var c = new NodeContext { Value = e, SourcePositions = Element.SourcePositions, TargetPositions = Element.TargetPositions };
                        l.Add(c);
                        if (!d.ContainsKey(LocalName))
                        {
                            d.Add(LocalName, c);
                        }
                    }
                    var Value = Projector(new ElementUnpackerState { Parent = Element, List = l, Dict = d });
                    if (typeof(R).IsClass)
                    {
                        if (Element.SourcePositions.ContainsKey(Element.Value))
                        {
                            Element.TargetPositions.Add(Value, Element.SourcePositions[Element.Value]);
                        }
                    }
                    return Value;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };
        }
    }

    public class NodeAggregatorToProjectorRangeTranslator : IAggregatorToProjectorRangeTranslator<Node, ElementPackerState>
    {
        public Func<D, Node> TranslateAggregatorToProjectorRange<D>(Action<D, ElementPackerState> Aggregator)
        {
            var FriendlyName = MappingTree.GetTypeFriendlyName(typeof(D).GetGenericArguments().Single());
            return v =>
            {
                var c = (IContext)v;
                Node x;
                var l = new List<Node>();
                if (v != null)
                {
                    var s = new ElementPackerState { UseParent = false, Parent = null, List = l, SourceMappings = c.SourceMappings, TargetMappings = c.TargetMappings };
                    Aggregator(v, s);
                    if (s.UseParent)
                    {
                        x = s.Parent;
                        x.Stem.Name = FriendlyName;
                    }
                    else if (l.Count == 0)
                    {
                        x = Node.CreateStem(new Stem { Name = FriendlyName, Children = new List<Node>() });
                    }
                    else
                    {
                        x = Node.CreateStem(new Stem { Name = FriendlyName, Children = l });
                    }
                }
                else
                {
                    x = Node.CreateStem(new Stem { Name = FriendlyName, Children = null });
                }
                if (c.SourceMappings.ContainsKey(c.Value))
                {
                    c.TargetMappings.Add(x, c.SourceMappings[c.Value]);
                }
                return x;
            };
        }
    }

    public class NodeProjectorToAggregatorRangeTranslator : IProjectorToAggregatorRangeTranslator<ElementPackerState, Node>
    {
        public Action<D, ElementPackerState> TranslateProjectorToAggregatorRange<D>(Func<D, Node> Projector)
        {
            return (v, s) => s.List.Add(Projector(v));
        }
    }

    public class ContextDomainTranslatorProjectorResolver : IProjectorResolver
    {
        public static Func<Context<D>, string> ContextUnpack<D>(Func<D, string> Inner)
        {
            return c => Inner(c.Value);
        }

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType.IsGenericType && DomainType.GetGenericTypeDefinition() == typeof(Context<>) && RangeType == typeof(string))
            {
                var InnerDomainType = DomainType.GetGenericArguments().Single();
                var Inner = InnerResolver.TryResolveProjector(new KeyValuePair<Type, Type>(InnerDomainType, RangeType));
                if (Inner == null) return null;
                var DummyMethod = (Func<Func<DummyType, string>, Func<Context<DummyType>, string>>)ContextUnpack<DummyType>;
                var m = DummyMethod.MakeDelegateMethodFromDummy(InnerDomainType);
                var d = m.StaticDynamicInvoke<Delegate, Delegate>(Inner);
                return d;
            }
            return null;
        }

        private IProjectorResolver InnerResolver;
        public ContextDomainTranslatorProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class ContextDomainTranslatorAggregatorResolver : IAggregatorResolver
    {
        public static Action<Context<D>, ElementPackerState> ContextUnpack<D>(Action<D, ElementPackerState> Inner)
        {
            return (c, s) => Inner(c.Value, s);
        }

        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var DomainType = TypePair.Key;
            var RangeType = TypePair.Value;
            if (DomainType.IsGenericType && DomainType.GetGenericTypeDefinition() == typeof(Context<>) && RangeType == typeof(ElementPackerState))
            {
                var InnerDomainType = DomainType.GetGenericArguments().Single();
                var Inner = InnerResolver.TryResolveAggregator(new KeyValuePair<Type, Type>(InnerDomainType, RangeType));
                if (Inner == null) return null;
                var DummyMethod = (Func<Action<DummyType, ElementPackerState>, Action<Context<DummyType>, ElementPackerState>>)ContextUnpack<DummyType>;
                var m = DummyMethod.MakeDelegateMethodFromDummy(InnerDomainType);
                var d = m.StaticDynamicInvoke<Delegate, Delegate>(Inner);
                return d;
            }
            return null;
        }

        private IAggregatorResolver InnerResolver;
        public ContextDomainTranslatorAggregatorResolver(IAggregatorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class FieldProjectorResolver : IFieldProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>(string Name)
        {
            var Mapper = (Func<NodeContext, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(NodeContext), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var d = s.Dict;
                if (!d.ContainsKey(Name))
                {
                    var i = new FileLocationInformation();
                    if (s.Parent.SourcePositions.ContainsKey(s.Parent))
                    {
                        var p = s.Parent.SourcePositions[s.Parent];
                        i.Path = p.Text.Path;
                        if (p.Range.OnSome)
                        {
                            var Range = p.Range.Value;
                            i.LineNumber = Range.Start.Row;
                            i.ColumnNumber = Range.Start.Column;
                        }
                    }
                    throw new InvalidTextFormatException("FieldNameNotFound: {0}".Formats(Name), i);
                }
                return Mapper(d[Name]);
            };
            return F;
        }

        private Dictionary<Type, Func<string, Delegate>> Dict = new Dictionary<Type, Func<string, Delegate>>();
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<string, Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<string, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public FieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class FieldAggregatorResolver : IFieldAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>(string Name)
        {
            var Mapper = (Func<Context<D>, Node>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(Context<D>), typeof(Node)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(new Context<D> { Value = k, SourceMappings = s.SourceMappings, TargetMappings = s.TargetMappings });
                e.Stem.Name = Name;
                s.List.Add(e);
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
                var GenericMapper = (Func<string, Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
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

    public class AliasFieldProjectorResolver : IAliasFieldProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>()
        {
            var Mapper = (Func<NodeContext, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(NodeContext), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                return Mapper(s.Parent);
            };
            return F;
        }

        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var GenericMapper = (Func<Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public AliasFieldProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class AliasFieldAggregatorResolver : IAliasFieldAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>()
        {
            var Mapper = (Func<Context<D>, Node>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(Context<D>), typeof(Node)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(new Context<D> { Value = k, SourceMappings = s.SourceMappings, TargetMappings = s.TargetMappings });
                s.UseParent = true;
                s.Parent = e;
            };
            return F;
        }

        public Delegate ResolveAggregator(MemberInfo Member, Type Type)
        {
            var GenericMapper = (Func<Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public AliasFieldAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class TagProjectorResolver : ITagProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>()
        {
            var Mapper = (Func<string, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(string), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var TagValue = s.List.Single().Value.Stem.Name;
                return Mapper(TagValue);
            };
            return F;
        }

        public Delegate ResolveProjector(MemberInfo Member, Type TagType)
        {
            var GenericMapper = (Func<Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(TagType).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public TagProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class TagAggregatorResolver : ITagAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>()
        {
            var Mapper = (Func<D, string>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(string)));
            Action<D, ElementPackerState> F = (k, s) => { };
            return F;
        }

        public Delegate ResolveAggregator(MemberInfo Member, Type TagType)
        {
            var GenericMapper = (Func<Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
            var m = GenericMapper.MakeDelegateMethodFromDummy(TagType).AdaptFunction<Delegate>();
            return m();
        }

        private IProjectorResolver InnerResolver;
        public TagAggregatorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }

    public class TaggedUnionAlternativeProjectorResolver : ITaggedUnionAlternativeProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>(string Name)
        {
            var Mapper = (Func<NodeContext, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(NodeContext), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var d = s.Dict;
                if (!d.ContainsKey(Name))
                {
                    var i = new FileLocationInformation();
                    if (s.Parent.SourcePositions.ContainsKey(s.Parent))
                    {
                        var p = s.Parent.SourcePositions[s.Parent];
                        i.Path = p.Text.Path;
                        if (p.Range.OnSome)
                        {
                            var Range = p.Range.Value;
                            i.LineNumber = Range.Start.Row;
                            i.ColumnNumber = Range.Start.Column;
                        }
                    }
                    throw new InvalidTextFormatException("AlternativeNameNotFound: {0}".Formats(Name), i);
                }
                return Mapper(d[Name]);
            };
            return F;
        }

        private Dictionary<Type, Func<string, Delegate>> Dict = new Dictionary<Type, Func<string, Delegate>>();
        public Delegate ResolveProjector(MemberInfo Member, Type Type)
        {
            var Name = Member.Name;
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Name);
            }
            else
            {
                var GenericMapper = (Func<string, Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<string, Delegate>();
                Dict.Add(Type, m);
                return m(Name);
            }
        }

        private IProjectorResolver InnerResolver;
        public TaggedUnionAlternativeProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class TaggedUnionAlternativeAggregatorResolver : ITaggedUnionAlternativeAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>(string Name)
        {
            var Mapper = (Func<Context<D>, Node>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(Context<D>), typeof(Node)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(new Context<D> { Value = k, SourceMappings = s.SourceMappings, TargetMappings = s.TargetMappings });
                e.Stem.Name = Name;
                s.List.Add(e);
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
                var GenericMapper = (Func<string, Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
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

    public class TupleElementProjectorResolver : ITupleElementProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>(int Index)
        {
            var Mapper = (Func<NodeContext, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(NodeContext), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var l = s.List;
                return Mapper(l[Index]);
            };
            return F;
        }

        private Dictionary<Type, Func<int, Delegate>> Dict = new Dictionary<Type, Func<int, Delegate>>();
        public Delegate ResolveProjector(MemberInfo Member, int Index, Type Type)
        {
            if (Dict.ContainsKey(Type))
            {
                var m = Dict[Type];
                return m(Index);
            }
            else
            {
                var GenericMapper = (Func<int, Func<ElementUnpackerState, DummyType>>)Resolve<DummyType>;
                var m = GenericMapper.MakeDelegateMethodFromDummy(Type).AdaptFunction<int, Delegate>();
                Dict.Add(Type, m);
                return m(Index);
            }
        }

        private IProjectorResolver InnerResolver;
        public TupleElementProjectorResolver(IProjectorResolver Resolver)
        {
            this.InnerResolver = Resolver;
        }
    }
    public class TupleElementAggregatorResolver : ITupleElementAggregatorResolver<ElementPackerState>
    {
        private Action<D, ElementPackerState> Resolve<D>(int Index)
        {
            var Mapper = (Func<Context<D>, Node>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(Context<D>), typeof(Node)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(new Context<D> { Value = k, SourceMappings = s.SourceMappings, TargetMappings = s.TargetMappings });
                s.List.Add(e);
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
                var GenericMapper = (Func<int, Action<DummyType, ElementPackerState>>)Resolve<DummyType>;
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

    public class ByteArrayTranslator : IProjectorToProjectorRangeTranslator<byte[], string>, IProjectorToProjectorDomainTranslator<byte[], string>
    {
        Func<D, byte[]> IProjectorToProjectorRangeTranslator<byte[], string>.TranslateProjectorToProjectorRange<D>(Func<D, string> Projector)
        {
            return k =>
            {
                if (k == null) return null;
                var Trimmed = Projector(k).Trim(" \\t\\r\\n".Descape().ToCharArray());
                if (Trimmed == "") return new byte[] { };
                return Regex.Split(Trimmed, "( |\\t|\\r|\\n)+", RegexOptions.ExplicitCapture).Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToArray();
            };
        }

        Func<byte[], R> IProjectorToProjectorDomainTranslator<byte[], string>.TranslateProjectorToProjectorDomain<R>(Func<string, R> Projector)
        {
            return ba =>
            {
                if (ba == null) return default(R);
                return Projector(string.Join(" ", (ba.Select(b => b.ToString("X2")).ToArray())));
            };
        }
    }

    public class ByteListTranslator : IProjectorToProjectorRangeTranslator<List<byte>, string>, IProjectorToProjectorDomainTranslator<List<byte>, string>
    {
        Func<D, List<byte>> IProjectorToProjectorRangeTranslator<List<byte>, string>.TranslateProjectorToProjectorRange<D>(Func<D, string> Projector)
        {
            return k =>
            {
                var Trimmed = Projector(k).Trim(" \\t\\r\\n".Descape().ToCharArray());
                if (Trimmed == "") return new List<byte>();
                return Regex.Split(Trimmed, "( |\\t|\\r|\\n)+", RegexOptions.ExplicitCapture).Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToList();
            };
        }

        Func<List<byte>, R> IProjectorToProjectorDomainTranslator<List<byte>, string>.TranslateProjectorToProjectorDomain<R>(Func<string, R> Projector)
        {
            return ba => Projector(string.Join(" ", (ba.Select(b => b.ToString("X2")).ToArray())));
        }
    }

    public class DebugReaderResolver : IMapperResolver
    {
        private IMapperResolver InnerResolver;
        public DebugReaderResolver(IMapperResolver InnerResolver)
        {
            this.InnerResolver = InnerResolver;
        }

        private Node CurrentReadingNodeValue;
        private void SetCurrentNode(NodeContext c)
        {
            CurrentReadingNodeValue = c.Value;
        }
        public Node CurrentReadingNode
        {
            get { return CurrentReadingNodeValue; }
        }

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var m = InnerResolver.TryResolveProjector(TypePair);
            if (TypePair.Key != typeof(NodeContext)) return m;
            if (m == null) return null;

            var Parameters = m.GetParameters().Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var DebugDelegate = (Delegate)(Action<NodeContext>)this.SetCurrentNode;
            var DebugCall = CollectionOperations.CreatePair(DebugDelegate, new Expression[] { Parameters.First() });
            var OriginalCall = CollectionOperations.CreatePair(m, Parameters.Select(p => (Expression)p).ToArray());
            var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(new[] { DebugCall, OriginalCall });
            var FunctionLambda = Expression.Lambda(m.GetType(), Expression.Block(Context.DelegateExpressions), Parameters);

            return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
        }

        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var m = InnerResolver.TryResolveAggregator(TypePair);
            if (TypePair.Key != typeof(NodeContext)) return m;
            if (m == null) return null;

            var Parameters = m.GetParameters().Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var DebugDelegate = (Delegate)(Action<NodeContext>)this.SetCurrentNode;
            var DebugCall = CollectionOperations.CreatePair(DebugDelegate, new Expression[] { Parameters.First() });
            var OriginalCall = CollectionOperations.CreatePair(m, Parameters.Select(p => (Expression)p).ToArray());
            var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(new[] { DebugCall, OriginalCall });
            var FunctionLambda = Expression.Lambda(m.GetType(), Expression.Block(Context.DelegateExpressions), Parameters);

            return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
        }
    }
}
