using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using Firefly;
using Firefly.Mapping.MetaProgramming;
using Firefly.Texting;

namespace Firefly.Mapping.XmlText
{
    public interface IXmlReader
    {
        T Read<T>(XElement s);
    }
    public interface IXmlWriter
    {
        XElement Write<T>(T Value);
    }
    public interface IXmlSerializer : IXmlReader, IXmlWriter
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
    public class XmlSerializer : IXmlSerializer
    {
        private XmlReaderResolver ReaderResolver;
        private XmlWriterResolver WriterResolver;

        private IMapperResolver ReaderCache;
        private IMapperResolver WriterCache;

        public XmlSerializer() : this(true)
        {
        }
        public XmlSerializer(bool UseByteArrayAndListTranslator)
        {
            var ReaderReference = new ReferenceMapperResolver();
            ReaderCache = ReaderReference;
            ReaderResolver = new XmlReaderResolver(ReaderReference);
            ReaderReference.Inner = ReaderResolver.AsCached();

            var WriterReference = new ReferenceMapperResolver();
            WriterCache = WriterReference;
            WriterResolver = new XmlWriterResolver(WriterReference);
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
        public void PutReader<T>(Func<XElement, T> Reader)
        {
            ReaderResolver.PutReader(Reader);
        }
        public void PutWriter<T>(Func<T, XElement> Writer)
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
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<XElement, M> Translator)
        {
            ReaderResolver.PutReaderTranslator(Translator);
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<XElement, M> Translator)
        {
            WriterResolver.PutWriterTranslator(Translator);
        }

        public T Read<T>(XElement s)
        {
            var m = ReaderCache.ResolveProjector<XElement, T>();
            return m(s);
        }
        public XElement Write<T>(T Value)
        {
            var m = WriterCache.ResolveProjector<T, XElement>();
            return m(Value);
        }

        public XElement CurrentReadingXElement
        {
            get { return ReaderResolver.CurrentReadingXElement; }
        }
    }

    public class XmlReaderResolver : IMapperResolver
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

        public XmlReaderResolver(IMapperResolver Root)
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

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                new EnumResolver(),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementToStringDomainTranslator()),
                new CollectionUnpackerTemplate<XElement>(new CollectionUnpacker(Root.AsRuntimeDomainNoncircular())),
                new RecordUnpackerTemplate<ElementUnpackerState>(
                    new FieldProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new AliasFieldProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TagProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TaggedUnionAlternativeProjectorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TupleElementProjectorResolver(Root.AsRuntimeDomainNoncircular())
                ),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementProjectorToProjectorDomainTranslator())
            });
            DebugResolver = new DebugReaderResolver(Mapping.CreateMapper(ProjectorResolverList.Concatenated(), Mapping.EmptyAggregatorResolver));
            Resolver = DebugResolver;
        }

        public void PutReader<T>(Func<string, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReader<T>(Func<XElement, T> Reader)
        {
            PrimitiveResolver.PutProjector(Reader);
        }
        public void PutReaderTranslator<R, M>(IProjectorToProjectorRangeTranslator<R, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutReaderTranslator<M>(IProjectorToProjectorDomainTranslator<XElement, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }

        public XElement CurrentReadingXElement
        {
            get { return DebugResolver.CurrentReadingXElement; }
        }
    }

    public class XmlWriterResolver : IMapperResolver
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

        public XmlWriterResolver(IMapperResolver Root)
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

            ProjectorResolverList = new LinkedList<IProjectorResolver>(new IProjectorResolver[] {
                PrimitiveResolver,
                new EnumResolver(),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementToStringRangeTranslator()),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementAggregatorToProjectorRangeTranslator())
            });
            AggregatorResolverList = new LinkedList<IAggregatorResolver>(new IAggregatorResolver[] {
                new CollectionPackerTemplate<ElementPackerState>(new CollectionPacker(Root.AsRuntimeDomainNoncircular())),
                new RecordPackerTemplate<ElementPackerState>(
                    new FieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new AliasFieldAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TagAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TaggedUnionAlternativeAggregatorResolver(Root.AsRuntimeDomainNoncircular()),
                    new TupleElementAggregatorResolver(Root.AsRuntimeDomainNoncircular())
                ),
                TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), new XElementProjectorToAggregatorRangeTranslator())
            });
            Resolver = Mapping.CreateMapper(ProjectorResolverList.Concatenated(), AggregatorResolverList.Concatenated());
        }

        public void PutWriter<T>(Func<T, string> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriter<T>(Func<T, XElement> Writer)
        {
            PrimitiveResolver.PutProjector(Writer);
        }
        public void PutWriterTranslator<D, M>(IProjectorToProjectorDomainTranslator<D, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
        public void PutWriterTranslator<M>(IProjectorToProjectorRangeTranslator<XElement, M> Translator)
        {
            ProjectorResolverList.AddFirst(TranslatorResolver.Create(Root.AsRuntimeDomainNoncircular(), Translator));
        }
    }

    public static class MappingXml
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

    public class ElementUnpackerState
    {
        public XElement Parent;
        public List<XElement> List;
        public Dictionary<string, XElement> Dict;
        public Dictionary<string, XAttribute> AttributeDict;
    }
    public class ElementPackerState
    {
        public bool UseParent;
        public XElement Parent;
        public List<XNode> List;
        public List<XAttribute> AttributeList;
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

    public class CollectionUnpacker : IGenericCollectionProjectorResolver<XElement>
    {
        public Func<XElement, RCollection> ResolveProjector<R, RCollection>() where RCollection : ICollection<R>, new()
        {
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            Func<XElement, RCollection> F = Key =>
            {
                if (!Key.IsEmpty)
                {
                    var List = new RCollection();
                    foreach (var k in Key.Elements())
                    {
                        List.Add(Mapper(k));
                    }
                    return List;
                }
                else
                {
                    return default(RCollection);
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
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<DCollection, ElementPackerState> F = (c, Value) =>
            {
                var k = 0;
                foreach (var v in c)
                {
                    Value.List.Add(new XComment(k.ToInvariantString()));
                    Value.List.Add(Mapper(v));
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

    public class XElementToStringRangeTranslator : IProjectorToProjectorRangeTranslator<XElement, string>
    {
        public Func<D, XElement> TranslateProjectorToProjectorRange<D>(Func<D, string> Projector)
        {
            var FriendlyName = MappingXml.GetTypeFriendlyName(typeof(D));
            return v =>
            {
                var s = Projector(v);
                return new XElement(FriendlyName, s);
            };
        }
    }

    public class XElementToStringDomainTranslator : IProjectorToProjectorDomainTranslator<XElement, string>
    {
        public Func<XElement, R> TranslateProjectorToProjectorDomain<R>(Func<string, R> Projector)
        {
            return v =>
            {
                if (v.IsEmpty) return default(R);
                return Projector(v.Value);
            };
        }
    }

    public class XElementProjectorToProjectorDomainTranslator : IProjectorToProjectorDomainTranslator<XElement, ElementUnpackerState>
    {
        public Func<XElement, R> TranslateProjectorToProjectorDomain<R>(Func<ElementUnpackerState, R> Projector)
        {
            return Element =>
            {
                if (!Element.IsEmpty)
                {
                    var l = Element.Elements().ToList();
                    var d = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in l)
                    {
                        var LocalName = e.Name.LocalName;
                        if (!d.ContainsKey(LocalName))
                        {
                            d.Add(LocalName, e);
                        }
                    }
                    var ad = new Dictionary<string, XAttribute>(StringComparer.OrdinalIgnoreCase);
                    foreach (var a in Element.Attributes())
                    {
                        var LocalName = a.Name.LocalName;
                        if (!ad.ContainsKey(LocalName))
                        {
                            ad.Add(LocalName, a);
                        }
                    }
                    return Projector(new ElementUnpackerState { Parent = Element, List = l, Dict = d, AttributeDict = ad });
                }
                else
                {
                    return default(R);
                }
            };
        }
    }

    public class XElementAggregatorToProjectorRangeTranslator : IAggregatorToProjectorRangeTranslator<XElement, ElementPackerState>
    {
        public Func<D, XElement> TranslateAggregatorToProjectorRange<D>(Action<D, ElementPackerState> Aggregator)
        {
            var FriendlyName = MappingXml.GetTypeFriendlyName(typeof(D));
            return v =>
            {
                XElement x;
                var l = new List<XNode>();
                var al = new List<XAttribute>();
                if (v != null)
                {
                    var s = new ElementPackerState { UseParent = false, Parent = null, List = l, AttributeList = al };
                    Aggregator(v, s);
                    if (s.UseParent)
                    {
                        x = s.Parent;
                        x.Name = FriendlyName;
                    }
                    else if (l.Count == 0)
                    {
                        x = new XElement(FriendlyName, "");
                    }
                    else
                    {
                        x = new XElement(FriendlyName, l.ToArray());
                    }
                }
                else
                {
                    x = new XElement(FriendlyName, (object)null);
                }
                foreach (var a in al)
                {
                    x.SetAttributeValue(a.Name, a.Value);
                }
                return x;
            };
        }
    }

    public class XElementProjectorToAggregatorRangeTranslator : IProjectorToAggregatorRangeTranslator<ElementPackerState, XElement>
    {
        public Action<D, ElementPackerState> TranslateProjectorToAggregatorRange<D>(Func<D, XElement> Projector)
        {
            var FriendlyName = MappingXml.GetTypeFriendlyName(typeof(D));
            return (v, s) => s.List.Add(Projector(v));
        }
    }

    public class FieldProjectorResolver : IFieldProjectorResolver<ElementUnpackerState>
    {
        private Func<ElementUnpackerState, R> Resolve<R>(string Name)
        {
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var d = s.Dict;
                if (!d.ContainsKey(Name))
                {
                    var i = new FileLocationInformation();
                    var flip = s.Parent as IFileLocationInformationProvider;
                    if (flip != null)
                    {
                        i = flip.FileLocationInformation;
                    }
                    else
                    {
                        var li = (IXmlLineInfo)s.Parent;
                        if (li.HasLineInfo())
                        {
                            i.LineNumber = li.LineNumber;
                            i.ColumnNumber = li.LinePosition;
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
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(k);
                e.Name = Name;
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
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
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
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(k);
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
                var TagValue = s.List.Single().Name.LocalName;
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
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
            Func<ElementUnpackerState, R> F = s =>
            {
                var d = s.Dict;
                if (!d.ContainsKey(Name))
                {
                    var i = new FileLocationInformation();
                    var flip = s.Parent as IFileLocationInformationProvider;
                    if (flip != null)
                    {
                        i = flip.FileLocationInformation;
                    }
                    else
                    {
                        var li = (IXmlLineInfo)s.Parent;
                        if (li.HasLineInfo())
                        {
                            i.LineNumber = li.LineNumber;
                            i.ColumnNumber = li.LinePosition;
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
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                var e = Mapper(k);
                e.Name = Name;
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
            var Mapper = (Func<XElement, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(XElement), typeof(R)));
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
            var Mapper = (Func<D, XElement>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(XElement)));
            Action<D, ElementPackerState> F = (k, s) =>
            {
                s.List.Add(Mapper(k));
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

        private XElement CurrentReadingXElementValue;
        private void SetCurrentXElement(XElement x)
        {
            CurrentReadingXElementValue = x;
        }
        public XElement CurrentReadingXElement
        {
            get { return CurrentReadingXElementValue; }
        }

        public Delegate TryResolveProjector(KeyValuePair<Type, Type> TypePair)
        {
            var m = InnerResolver.TryResolveProjector(TypePair);
            if (TypePair.Key != typeof(XElement)) return m;
            if (m == null) return null;

            var Parameters = m.GetParameters().Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var DebugDelegate = (Delegate)(Action<XElement>)this.SetCurrentXElement;
            var DebugCall = CollectionOperations.CreatePair(DebugDelegate, new Expression[] { Parameters.First() });
            var OriginalCall = CollectionOperations.CreatePair(m, Parameters.Select(p => (Expression)p).ToArray());
            var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(new[] { DebugCall, OriginalCall });
            var FunctionLambda = Expression.Lambda(m.GetType(), Expression.Block(Context.DelegateExpressions), Parameters);

            return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
        }

        public Delegate TryResolveAggregator(KeyValuePair<Type, Type> TypePair)
        {
            var m = InnerResolver.TryResolveAggregator(TypePair);
            if (TypePair.Key != typeof(XElement)) return m;
            if (m == null) return null;

            var Parameters = m.GetParameters().Select(p => Expression.Parameter(p.Type, p.Name)).ToArray();
            var DebugDelegate = (Delegate)(Action<XElement>)this.SetCurrentXElement;
            var DebugCall = CollectionOperations.CreatePair(DebugDelegate, new Expression[] { Parameters.First() });
            var OriginalCall = CollectionOperations.CreatePair(m, Parameters.Select(p => (Expression)p).ToArray());
            var Context = MetaProgramming.MetaProgramming.CreateDelegateExpressionContext(new[] { DebugCall, OriginalCall });
            var FunctionLambda = Expression.Lambda(m.GetType(), Expression.Block(Context.DelegateExpressions), Parameters);

            return MetaProgramming.MetaProgramming.CreateDelegate(Context.ClosureParam, Context.Closure, FunctionLambda);
        }
    }
}
