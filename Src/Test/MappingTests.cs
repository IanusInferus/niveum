using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaProgramming;
using Firefly.Mapping.MetaSchema;
using Firefly.Mapping.XmlText;
using Firefly.Streaming;
using Firefly.TextEncoding;

#pragma warning disable 0659, 0660, 0661

namespace Firefly.Test
{
    public static class MappingTests
    {
        public static void TestMetaProgramming()
        {
            Func<int, int> g = i => i;
            Func<int, int> h = i => i;

            var hg = (Func<int, int>)(((Delegate)g).Compose(h));

            TestAssert.IsTrue(hg(1) == 1);
            TestAssert.IsTrue(hg(2) == 2);

            Func<int, int, int> k = (i, j) => i + j;
            var l = ((Delegate)k).Curry(1).AdaptFunction<int, int>();

            TestAssert.IsTrue(l(1) == 2);
            TestAssert.IsTrue(l(2) == 3);
        }

        public enum SerializerTestEnum
        {
            E1,
            E2,
            E3
        }

        public class SerializerTestObject
        {
            public int i;
            public byte s;
            public SerializerTestObject2 o;
            public byte[] a;
            public List<short> l;
            public LinkedList<int> l2;
            public HashSet<ulong> l3;
            public SerializerTestEnum e1;
            public KeyValuePair<byte, int> p;
            public string str;

            public static bool operator ==(SerializerTestObject Left, SerializerTestObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.i == Right.i && Left.s == Right.s && Left.o.h == Right.o.h && Left.a.SequenceEqual(Right.a) && Left.l.ToArray().SequenceEqual(Right.l.ToArray()) && Left.l2.ToArray().SequenceEqual(Right.l2.ToArray()) && Left.l3.ToArray().SequenceEqual(Right.l3.ToArray()) && Left.e1 == Right.e1 && Left.p.Key == Right.p.Key && Left.p.Value == Right.p.Value && Left.str == Right.str;
            }
            public static bool operator !=(SerializerTestObject Left, SerializerTestObject Right)
            {
                return !(Left == Right);
            }
            public override bool Equals(object obj)
            {
                var o = obj as SerializerTestObject;
                if (o == null) return false;
                return this == o;
            }
        }

        public static SerializerTestObject TestObject = new SerializerTestObject
        {
            i = 1,
            s = 2,
            o = new SerializerTestObject2 { h = 3 },
            a = new byte[] { 4, 5, 6 },
            l = new List<short> { 7, 8, 9 },
            l2 = new LinkedList<int>(new int[] { 10, 11, 12 }),
            l3 = new HashSet<ulong> { 13, 14, 15 },
            e1 = (SerializerTestEnum)16,
            p = new KeyValuePair<byte, int>(17, 18),
            str = "19"
        };

        public struct SerializerTestObject2
        {
            public int h;
        }

        public class GenericCollectionProjectorResolver<D> : IGenericCollectionProjectorResolver<D>
        {
            public Func<D, RCollection> ResolveProjector<R, RCollection>() where RCollection : ICollection<R>, new()
            {
                var Mapper = (Func<D, R>)InnerResolver.ResolveProjector(CollectionOperations.CreatePair(typeof(D), typeof(R)));
                Func<D, RCollection> F =
                    Key =>
                    {
                        int Size = 3;
                        var l = new RCollection();
                        for (int n = 0; n < Size; n++)
                        {
                            l.Add(Mapper(Key));
                        }
                        return l;
                    };
                return F;
            }

            private IProjectorResolver InnerResolver;
            public GenericCollectionProjectorResolver(IProjectorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public class GenericListAggregatorResolver<R> : IGenericCollectionAggregatorResolver<R>
        {
            public Action<DList, R> ResolveAggregator<D, DList>() where DList : ICollection<D>
            {
                var Mapper = (Action<D, R>)InnerResolver.ResolveAggregator(CollectionOperations.CreatePair(typeof(D), typeof(R)));
                Action<DList, R> F =
                    (list, Value) =>
                    {
                        int Size = 3;
                        var It = list.GetEnumerator();
                        for (int n = 0; n < Size; n++)
                        {
                            It.MoveNext();
                            Mapper(It.Current, Value);
                        }
                    };
                return F;
            }

            private IAggregatorResolver InnerResolver;
            public GenericListAggregatorResolver(IAggregatorResolver Resolver)
            {
                this.InnerResolver = Resolver;
            }
        }

        public static void TestObjectTreeMapper()
        {
            int Count = 0;

            {
                var mp = new ReferenceProjectorResolver();
                var pr = new PrimitiveResolver();
                var er = new Firefly.Mapping.Binary.EnumUnpacker<int>(mp);
                var cr = new CollectionUnpackerTemplate<int>(new GenericCollectionProjectorResolver<int>(mp));
                var csr = new RecordUnpackerTemplate<int>(mp);
                var mprs = new List<IProjectorResolver> { pr, er, cr, csr };
                pr.PutProjector<int, byte>(
                    i =>
                    {
                        Count += 1;
                        return (byte)Count;
                    }
                );
                pr.PutProjector<int, short>(
                    i =>
                    {
                        Count += 1;
                        return (short)Count;
                    }
                );
                pr.PutProjector<int, int>(
                    i =>
                    {
                        Count += 1;
                        return Count;
                    }
                );
                pr.PutProjector<int, ulong>(
                    i =>
                    {
                        Count += 1;
                        return (ulong)Count;
                    }
                );
                pr.PutProjector<int, string>(
                    i =>
                    {
                        Count += 1;
                        return Count.ToString();
                    }
                );
                mp.Inner = mprs.Concatenated();

                var BuiltObject = mp.Project<int, SerializerTestObject>(0);
                TestAssert.IsTrue(TestObject == BuiltObject);
            }

            int Count2 = 0;
            {
                var mp = new ReferenceAggregatorResolver();
                var pr = new PrimitiveResolver();
                var er = new Firefly.Mapping.Binary.EnumPacker<int>(mp);
                var cr = new CollectionPackerTemplate<int>(new GenericListAggregatorResolver<int>(mp));
                var csr = new RecordPackerTemplate<int>(mp);
                var mprs = new List<IAggregatorResolver> { pr, er, cr, csr };
                pr.PutAggregator<byte, int>(
                    (Key, Value) => { Count2 += 1; }
                );
                pr.PutAggregator<short, int>(
                    (Key, Value) => { Count2 += 1; }
                );
                pr.PutAggregator<int, int>(
                    (Key, Value) => { Count2 += 1; }
                );
                pr.PutAggregator<ulong, int>(
                    (Key, Value) => { Count2 += 1; }
                );
                pr.PutAggregator<string, int>(
                    (Key, Value) => { Count2 += 1; }
                );
                mp.Inner = mprs.Concatenated();

                mp.Aggregate(TestObject, 1);
                TestAssert.IsTrue(Count == Count2);
            }
        }

        public class StringAndBytesTranslator :
            IProjectorToProjectorRangeTranslator<string, byte[]>,
            IProjectorToProjectorDomainTranslator<string, byte[]>
        {
            public Func<D, string> TranslateProjectorToProjectorRange<D>(Func<D, byte[]> Projector)
            {
                return v => Firefly.TextEncoding.TextEncoding.UTF16.GetString(Projector(v));
            }
            public Func<string, R> TranslateProjectorToProjectorDomain<R>(Func<byte[], R> Projector)
            {
                return s => Projector(Firefly.TextEncoding.TextEncoding.UTF16.GetBytes(s));
            }
        }

        public static void TestBinarySerializer()
        {
            SerializerTestObject BinaryRoundTripped;

            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();

                var sbr = new StringAndBytesTranslator();

                bs.PutReaderTranslator((IProjectorToProjectorRangeTranslator<string, byte[]>)sbr);
                bs.PutWriterTranslator((IProjectorToProjectorDomainTranslator<string, byte[]>)sbr);
                bs.PutCounterTranslator((IProjectorToProjectorDomainTranslator<string, byte[]>)sbr);

                long Size = bs.Count(TestObject);
                bs.Write(s, TestObject);
                TestAssert.IsTrue(Size == s.Length);
                s.Position = 0;
                BinaryRoundTripped = bs.Read<SerializerTestObject>(s);
            }
            TestAssert.IsTrue(TestObject == BinaryRoundTripped);
        }

        public class XmlTestObject
        {
            public XmlTestObject2 Test;
            public object o;

            public static bool operator ==(XmlTestObject Left, XmlTestObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.Test == Right.Test && (ReferenceEquals(Left.o, null) == ReferenceEquals(Right.o, null));
            }
            public static bool operator !=(XmlTestObject Left, XmlTestObject Right)
            {
                return !(Left == Right);
            }
            public override bool Equals(object obj)
            {
                var o = obj as XmlTestObject;
                if (o == null) return false;
                return this == o;
            }
        }
        public class XmlTestObject2
        {
            public int i = 1;

            public static bool operator ==(XmlTestObject2 Left, XmlTestObject2 Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.i == Right.i;
            }
            public static bool operator !=(XmlTestObject2 Left, XmlTestObject2 Right)
            {
                return !(Left == Right);
            }
            public override bool Equals(object obj)
            {
                var o = obj as XmlTestObject2;
                if (o == null) return false;
                return this == o;
            }
        }

        public static void XmlRoundTrip<T>(XmlSerializer xs, T v)
        {
            var xe = xs.Write(v);
            var RoundTripped = xs.Read<T>(xe);
            TestAssert.IsTrue(object.Equals(v, RoundTripped));
        }
        public static void XmlRoundTripCollection<E, T>(XmlSerializer xs, T v) where T : IEnumerable<E>
        {
            var xe = xs.Write(v);
            var RoundTripped = xs.Read<T>(xe);
            var va = v.ToArray();
            var ra = RoundTripped.ToArray();
            TestAssert.IsTrue(Enumerable.SequenceEqual(va, ra));
        }
        public static void TestXmlSerializer()
        {
            var xs = new XmlSerializer();

            XmlRoundTrip(xs, 123123);
            XmlRoundTrip(xs, "123123");
            XmlRoundTrip(xs, SerializerTestEnum.E3);
            XmlRoundTrip(xs, 123.123);
            XmlRoundTrip(xs, (decimal)123.123);
            XmlRoundTrip(xs, true);

            XmlRoundTripCollection<byte, byte[]>(xs, new byte[] { 1, 2, 3 });
            XmlRoundTripCollection<byte, LinkedList<byte>>(xs, new LinkedList<byte>(new byte[] { 1, 2, 3 }));

            XmlRoundTrip(xs, TestObject);

            XmlRoundTrip<XmlTestObject>(xs, null);
            XmlRoundTrip(xs, new XmlTestObject { Test = null });
            XmlRoundTrip(xs, new XmlTestObject { o = new object() });
            XmlRoundTrip<byte[]>(xs, null);
            XmlRoundTripCollection<byte, byte[]>(xs, new byte[] { });
        }
        public static void TestXmlSerializerForDict()
        {
            var xs = new XmlSerializer();

            var dict = new Dictionary<string, int>();
            dict.Add("123", 0);
            dict.Add("234", 1);

            var xe = xs.Write(dict);
            var RoundTripped = xs.Read<Dictionary<string, int>>(xe);
            var va = dict.ToArray();
            var ra = RoundTripped.ToArray();
            TestAssert.IsTrue(Enumerable.SequenceEqual(va, ra));
        }

        [Alias]
        public class AliasObject
        {
            public int i = 3;
            public static bool operator ==(AliasObject Left, AliasObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.i == Right.i;
            }
            public static bool operator !=(AliasObject Left, AliasObject Right)
            {
                return !(Left == Right);
            }
            public override bool Equals(object obj) { var o = obj as AliasObject; if (o == null) return false; return this == o; }
        }

        public enum TaggedUnionObjectTag
        {
            Item1,
            Item2,
            Item3,
            Item4
        }
        [TaggedUnion]
        public class TaggedUnionObject
        {
            [Tag] public TaggedUnionObjectTag _Tag;
            public int Item1;
            public short Item2;
            public byte Item3;
            public TaggedUnionObject Item4;
            public static bool Equal(TaggedUnionObject Left, TaggedUnionObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                if (Left._Tag != Right._Tag) return false;
                switch (Left._Tag)
                {
                    case TaggedUnionObjectTag.Item1:
                        return Left.Item1 == Right.Item1;
                    case TaggedUnionObjectTag.Item2:
                        return Left.Item2 == Right.Item2;
                    case TaggedUnionObjectTag.Item3:
                        return Left.Item3 == Right.Item3;
                    case TaggedUnionObjectTag.Item4:
                        return Equal(Left.Item4, Right.Item4);
                    default:
                        throw new InvalidOperationException();
                }
            }
            public static bool operator ==(TaggedUnionObject Left, TaggedUnionObject Right) { return Equal(Left, Right); }
            public static bool operator !=(TaggedUnionObject Left, TaggedUnionObject Right) { return !Equal(Left, Right); }
            public override bool Equals(object obj) { var o = obj as TaggedUnionObject; if (o == null) return false; return this == o; }
        }

        [Firefly.Mapping.MetaSchema.Tuple]
        public class TupleObject
        {
            public int Item1 = 1;
            public short Item2 = 2;
            public byte Item3 = 3;
            public static bool operator ==(TupleObject Left, TupleObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.Item1 == Right.Item1 && Left.Item2 == Right.Item2 && Left.Item3 == Right.Item3;
            }
            public static bool operator !=(TupleObject Left, TupleObject Right) { return !(Left == Right); }
            public override bool Equals(object obj) { var o = obj as TupleObject; if (o == null) return false; return this == o; }
        }

        [Alias]
        public class Alias2Object
        {
            public MixedObject i = new MixedObject { _Tag = MixedObjectTag.Item3, Item3 = new Alias3Object { i = 1 } };
            public static bool operator ==(Alias2Object Left, Alias2Object Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.i == Right.i;
            }
            public static bool operator !=(Alias2Object Left, Alias2Object Right) { return !(Left == Right); }
            public override bool Equals(object obj) { var o = obj as Alias2Object; if (o == null) return false; return this == o; }
        }
        [Alias]
        public class Alias3Object
        {
            public byte i = 123;
            public static bool operator ==(Alias3Object Left, Alias3Object Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.i == Right.i;
            }
            public static bool operator !=(Alias3Object Left, Alias3Object Right) { return !(Left == Right); }
            public override bool Equals(object obj) { var o = obj as Alias3Object; if (o == null) return false; return this == o; }
        }
        [Firefly.Mapping.MetaSchema.Tuple]
        public class Tuple2Object
        {
            public Alias2Object Item1 = new Alias2Object();
            public MixedObject Item2 = new MixedObject { _Tag = MixedObjectTag.Item3, Item3 = new Alias3Object { i = 2 } };
            public Alias3Object Item3 = new Alias3Object { i = 3 };
            public static bool operator ==(Tuple2Object Left, Tuple2Object Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                return Left.Item1 == Right.Item1 && Left.Item2 == Right.Item2 && Left.Item3 == Right.Item3;
            }
            public static bool operator !=(Tuple2Object Left, Tuple2Object Right) { return !(Left == Right); }
            public override bool Equals(object obj) { var o = obj as Tuple2Object; if (o == null) return false; return this == o; }
        }
        public enum MixedObjectTag
        {
            Item1,
            Item2,
            Item3,
            Item4
        }
        [TaggedUnion]
        public class MixedObject
        {
            [Tag] public MixedObjectTag _Tag;
            public Alias2Object Item1;
            public Tuple2Object Item2;
            public Alias3Object Item3;
            public MixedObject Item4;
            public static bool Equal(MixedObject Left, MixedObject Right)
            {
                if (ReferenceEquals(Left, null) && ReferenceEquals(Right, null)) return true;
                if (ReferenceEquals(Left, null) || ReferenceEquals(Right, null)) return false;
                if (Left._Tag != Right._Tag) return false;
                switch (Left._Tag)
                {
                    case MixedObjectTag.Item1:
                        return Left.Item1 == Right.Item1;
                    case MixedObjectTag.Item2:
                        return Left.Item2 == Right.Item2;
                    case MixedObjectTag.Item3:
                        return Left.Item3 == Right.Item3;
                    case MixedObjectTag.Item4:
                        return Equal(Left.Item4, Right.Item4);
                    default:
                        throw new InvalidOperationException();
                }
            }
            public static bool operator ==(MixedObject Left, MixedObject Right) { return Equal(Left, Right); }
            public static bool operator !=(MixedObject Left, MixedObject Right) { return !Equal(Left, Right); }
            public override bool Equals(object obj) { var o = obj as MixedObject; if (o == null) return false; return this == o; }
        }

        public static void TestAlias()
        {
            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();
                var xs = new XmlSerializer();

                var a1 = new AliasObject();
                AliasObject a2;

                bs.Write(s, a1);
                s.Position = 0;
                a2 = bs.Read<AliasObject>(s);
                TestAssert.IsTrue(a1 == a2);

                var x = xs.Write(a1);
                var a3 = xs.Read<AliasObject>(x);
                TestAssert.IsTrue(a1 == a3);
            }
        }

        public static void TestTaggedUnion()
        {
            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();
                var xs = new XmlSerializer();

                var a1 = new TaggedUnionObject { _Tag = TaggedUnionObjectTag.Item4, Item4 = new TaggedUnionObject { _Tag = TaggedUnionObjectTag.Item2, Item2 = 2 } };
                TaggedUnionObject a2;

                bs.Write(s, a1);
                TestAssert.IsTrue(s.Length == 10);

                s.Position = 0;
                a2 = bs.Read<TaggedUnionObject>(s);
                TestAssert.IsTrue(a1 == a2);

                var x = xs.Write(a1);
                var a3 = xs.Read<TaggedUnionObject>(x);
                TestAssert.IsTrue(a1 == a3);
            }
        }

        public static void TestTuple()
        {
            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();
                var xs = new XmlSerializer();

                var a1 = new TupleObject();
                TupleObject a2;

                bs.Write(s, a1);
                s.Position = 0;
                a2 = bs.Read<TupleObject>(s);
                TestAssert.IsTrue(a1 == a2);

                var x = xs.Write(a1);
                var a3 = xs.Read<TupleObject>(x);
                TestAssert.IsTrue(a1 == a3);
            }
        }

        public static void TestMixed()
        {
            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();
                var xs = new XmlSerializer();

                var a1 = new MixedObject { _Tag = MixedObjectTag.Item2, Item2 = new Tuple2Object() };
                MixedObject a2;

                bs.Write(s, a1);

                s.Position = 0;
                a2 = bs.Read<MixedObject>(s);
                TestAssert.IsTrue(a1 == a2);

                var x = xs.Write(a1);
                var a3 = xs.Read<MixedObject>(x);
                TestAssert.IsTrue(a1 == a3);
            }
        }

        public class RecursiveObject
        {
            public RecursiveObject[] Items;
        }

        public static void TestDebuggerDisplayer()
        {
            var o = new RecursiveObject { Items = new[] { new RecursiveObject { Items = new RecursiveObject[] { } }, new RecursiveObject { Items = null } } };
            var s = DebuggerDisplayer.ConvertToString(o);

            double? o2 = 3;
            var s2 = DebuggerDisplayer.ConvertToString(o2);
        }

        public static void TestRecursive()
        {
            using (var s = Streams.CreateMemoryStream())
            {
                var bs = new Firefly.Mapping.Binary.BinarySerializer();
                var xs = new XmlSerializer();

                var o = new RecursiveObject { Items = new[] { new RecursiveObject { Items = new RecursiveObject[] { } } } };

                var o2 = new RecursiveObject();
                o2.Items = new RecursiveObject[] { o2 };

                bs.Write(s, o);
                s.Position = 0;
                var o_2 = bs.Read<RecursiveObject>(s);

                var x = xs.Write(o);
                var o_3 = xs.Read<RecursiveObject>(x);

                try
                {
                    bs.Write(s, o2);
                    TestAssert.IsTrue(false);
                }
                catch (InvalidOperationException)
                {
                    TestAssert.IsTrue(true);
                }

                try
                {
                    var x_2 = xs.Write(o2);
                    TestAssert.IsTrue(false);
                }
                catch (InvalidOperationException)
                {
                    TestAssert.IsTrue(true);
                }
            }
        }
    }
}
