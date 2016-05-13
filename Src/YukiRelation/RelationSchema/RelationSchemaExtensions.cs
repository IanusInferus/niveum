//==========================================================================
//
//  File:        RelationSchemaExtensions.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构扩展
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;

namespace Yuki.RelationSchema
{
    public class KeyColumnComparer : IEqualityComparer<KeyColumn>
    {
        public Boolean Equals(KeyColumn x, KeyColumn y)
        {
            return (x.Name == y.Name) && (x.IsDescending == y.IsDescending);
        }

        public int GetHashCode(KeyColumn obj)
        {
            return obj.Name.GetHashCode() ^ (obj.IsDescending ? 1 : 0);
        }
    }

    public class KeyComparer : IEqualityComparer<Key>
    {
        private KeyColumnComparer c = new KeyColumnComparer();

        public Boolean Equals(Key x, Key y)
        {
            return x.Columns.SequenceEqual(y.Columns, c) && (x.IsClustered == y.IsClustered);
        }

        public int GetHashCode(Key obj)
        {
            return obj.Columns.Select(o => c.GetHashCode(o)).Aggregate((a, b) => a ^ b) ^ (obj.IsClustered ? 1 : 0);
        }
    }

    public class ForeignKey
    {
        public String ThisTableName;
        public List<String> ThisKeyColumns;
        public String OtherTableName;
        public List<String> OtherKeyColumns;

        public override bool Equals(object obj)
        {
            var o = obj as ForeignKey;
            if (o == null) { return false; }
            if (!ThisTableName.Equals(o.ThisTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
            if (!OtherTableName.Equals(o.OtherTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
            if (ThisKeyColumns.Count != o.ThisKeyColumns.Count) { return false; }
            if (OtherKeyColumns.Count != o.OtherKeyColumns.Count) { return false; }
            if (ThisKeyColumns.Intersect(o.ThisKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != ThisKeyColumns.Count) { return false; }
            if (OtherKeyColumns.Intersect(o.OtherKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != OtherKeyColumns.Count) { return false; }
            return true;
        }

        public override int GetHashCode()
        {
            Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
            return h(ThisTableName) ^ h(OtherTableName) ^ ThisKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b) ^ OtherKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
        }

        private String GetIdentifier()
        {
            return String.Format("{0}_{1}_{2}_{3}", ThisTableName, String.Join("And", ThisKeyColumns), OtherTableName, String.Join("And", OtherKeyColumns));
        }

        public String Name
        {
            get
            {
                return "FK_" + GetIdentifier();
            }
        }

        public String GetLimitedName(int MaxLength)
        {
            return RelationSchemaExtensions.GetLimitedKeyName("FK", GetIdentifier(), MaxLength);
        }
    }

    public static class RelationSchemaExtensions
    {
        public static IEnumerable<KeyValuePair<String, TypeDef>> GetMap(this Schema s)
        {
            return s.TypeRefs.Concat(s.Types).Where(t => !t.OnQueryList).Select(t => CollectionOperations.CreatePair(t.Name(), t));
        }

        private static ThreadLocal<BinarySerializer> bs = new ThreadLocal<BinarySerializer>
        (
            () =>
            {
                return Yuki.ObjectSchema.BinarySerializerWithString.Create();
            }
        );

        public static UInt64 Hash(this Schema s)
        {
            var Types = s.GetMap().OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Value).ToList();
            var TypesWithoutDescription = Types.Select(t => MapWithoutDescription(t)).ToList();

            var sha = new SHA1CryptoServiceProvider();
            Byte[] result;

            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Value.Write(TypesWithoutDescription, ms);
                ms.Position = 0;

                result = sha.ComputeHash(ms.ToUnsafeStream());
            }

            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(result.Skip(result.Length - 8).ToArray());
                ms.Position = 0;

                return ms.ReadUInt64B();
            }
        }

        private static TypeDef MapWithoutDescription(TypeDef t)
        {
            if (t.OnPrimitive)
            {
                var p = t.Primitive;
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, Description = "" });
            }
            else if (t.OnEntity)
            {
                var r = t.Entity;
                return TypeDef.CreateEntity(new EntityDef { Name = r.Name, CollectionName = r.CollectionName, Fields = r.Fields.Select(gp => MapWithoutDescription(gp)).ToList(), Description = "", PrimaryKey = r.PrimaryKey, UniqueKeys = r.UniqueKeys, NonUniqueKeys = r.NonUniqueKeys });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = e.Name, UnderlyingType = e.UnderlyingType, Literals = e.Literals.Select(l => MapWithoutDescription(l)).ToList(), Description = "" });
            }
            else if (t.OnQueryList)
            {
                var ql = t.QueryList;
                return t;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static VariableDef MapWithoutDescription(VariableDef v)
        {
            return new VariableDef { Name = v.Name, Type = v.Type, Description = "", Attribute = v.Attribute };
        }
        private static LiteralDef MapWithoutDescription(LiteralDef l)
        {
            return new LiteralDef { Name = l.Name, Value = l.Value, Description = "" };
        }

        private class Marker
        {
            public Dictionary<String, TypeDef> Types;
            public HashSet<TypeDef> Marked = new HashSet<TypeDef>();
            public void Mark(TypeDef t)
            {
                if (Marked.Contains(t)) { return; }
                Marked.Add(t);
                switch (t._Tag)
                {
                    case TypeDefTag.Primitive:
                        break;
                    case TypeDefTag.Entity:
                        foreach (var f in t.Entity.Fields)
                        {
                            Mark(f.Type);
                        }
                        break;
                    case TypeDefTag.Enum:
                        Mark(t.Enum.UnderlyingType);
                        break;
                    case TypeDefTag.QueryList:
                        foreach (var q in t.QueryList.Queries)
                        {
                            Mark(TypeSpec.CreateTypeRef(new TypeRef { Value = q.EntityName }));
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            public void Mark(TypeSpec t)
            {
                switch (t._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        {
                            var Name = t.TypeRef.Value;
                            if (Types.ContainsKey(Name))
                            {
                                Mark(Types[Name]);
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format("TypeNotExist: {0}", Name));
                            }
                        }
                        break;
                    case TypeSpecTag.List:
                        {
                            var Name = t.List.Value;
                            if (Types.ContainsKey(Name))
                            {
                                Mark(Types[Name]);
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format("TypeNotExist: {0}", Name));
                            }
                        }
                        break;
                    case TypeSpecTag.Optional:
                        {
                            var Name = t.Optional.Value;
                            if (Types.ContainsKey(Name))
                            {
                                Mark(Types[Name]);
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format("TypeNotExist: {0}", Name));
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public static void Verify(this Schema s)
        {
            VerifyDuplicatedNames(s);
            VerifyTypes(s);
            VerifyEntityKeys(s);
            VerifyEntityForeignKeys(s);
            VerifyQuerySyntax(s);
            VerifyQuerySemantics(s);
        }

        public static void VerifyDuplicatedNames(this Schema s)
        {
            CheckDuplicatedNames(s.TypePaths, tp => tp.Name, tp => String.Format("DuplicatedName {0}: at {1}", tp.Name, tp.Path));

            var PathDict = s.TypePaths.ToDictionary(tp => tp.Name, tp => tp.Path);

            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                switch (t._Tag)
                {
                    case TypeDefTag.Entity:
                        {
                            var r = t.Entity;
                            CheckDuplicatedNames(r.Fields, rf => rf.Name, rf => String.Format("DuplicatedField {0}: record {1}, at {2}", rf.Name, r.Name, PathDict[r.Name]));
                        }
                        break;
                    case TypeDefTag.Enum:
                        {
                            var e = t.Enum;
                            CheckDuplicatedNames(e.Literals, el => el.Name, el => String.Format("DuplicatedLiteral {0}: enum {1}, at {2}", el.Name, e.Name, PathDict[e.Name]));
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public static void VerifyTypes(this Schema s)
        {
            var Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);

            var m = new Marker { Types = Types };
            foreach (var t in s.Types)
            {
                m.Mark(t);
            }
        }

        public static void VerifyEntityKeys(this Schema s)
        {
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnEntity)
                {
                    var e = t.Entity;
                    foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                    {
                        foreach (var c in k.Columns)
                        {
                            if (e.Fields.Where(f => f.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).Count() == 0)
                            {
                                throw new InvalidOperationException(String.Format("InvalidColumnInKey: {0}.{1}", e.Name, c.Name));
                            }
                        }
                    }
                }
            }
        }

        private class ByIndex
        {
            public List<String> Columns;

            public override bool Equals(object obj)
            {
                var o = obj as ByIndex;
                if (o == null) { return false; }
                if (Columns.Count != o.Columns.Count) { return false; }
                if (Columns.Intersect(o.Columns, StringComparer.OrdinalIgnoreCase).Count() != Columns.Count) { return false; }
                return true;
            }

            public override int GetHashCode()
            {
                if (Columns.Count == 0) { return 0; }
                Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                return Columns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
            }
        }

        public static void VerifyEntityForeignKeys(this Schema s)
        {
            var Entities = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList();
            var IndexDict = new Dictionary<String, HashSet<ByIndex>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Entities)
            {
                var h = new HashSet<ByIndex>();
                foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                {
                    for (int i = 1; i <= k.Columns.Count; i += 1)
                    {
                        var SubIndex = new ByIndex { Columns = k.Columns.Take(i).Select(c => c.Name).ToList() };
                        if (!h.Contains(SubIndex))
                        {
                            h.Add(SubIndex);
                        }
                    }
                }
                IndexDict.Add(e.Name, h);
            }
            foreach (var e in Entities)
            {
                foreach (var a in e.Fields.Where(f => f.Attribute.OnNavigation))
                {
                    String OtherRecordName;
                    if (a.Type.OnTypeRef)
                    {
                        OtherRecordName = a.Type.TypeRef.Value;
                    }
                    else if (a.Type.OnOptional)
                    {
                        OtherRecordName = a.Type.Optional.Value;
                    }
                    else if (a.Type.OnList)
                    {
                        OtherRecordName = a.Type.List.Value;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    //FK和FNK只需要目标表上的键有索引，RFK和RFNK需要当前表和目标表的键都有索引
                    if (a.Attribute.Navigation.IsReverse)
                    {
                        var ThisIndex = new ByIndex { Columns = a.Attribute.Navigation.ThisKey };
                        var h = IndexDict[e.Name];
                        if (!h.Contains(ThisIndex))
                        {
                            throw new InvalidOperationException(String.Format("ThisKeyIsNotIndex: {0}->{1} FK:{2}={3}", e.Name, OtherRecordName, String.Join(", ", a.Attribute.Navigation.ThisKey), String.Join(", ", a.Attribute.Navigation.OtherKey)));
                        }
                    }
                    {
                        var OtherIndex = new ByIndex { Columns = a.Attribute.Navigation.OtherKey };
                        var h = IndexDict[OtherRecordName];
                        if (!h.Contains(OtherIndex))
                        {
                            throw new InvalidOperationException(String.Format("OtherKeyIsNotIndex: {0}->{1} FK:{2}={3}", e.Name, OtherRecordName, String.Join(", ", a.Attribute.Navigation.ThisKey), String.Join(", ", a.Attribute.Navigation.OtherKey)));
                        }
                    }
                }
            }
        }

        // 查询支持的语法如下
        //
        // From <EntityName> {Select, Lock} Optional By <Index>
        // From <EntityName> {Select, Lock} One By <Index>
        // From <EntityName> {Select, Lock} Many By <Index>
        // From <EntityName> {Select, Lock} Many By <Index> OrderBy <Index>
        // From <EntityName> {Select, Lock} All
        // From <EntityName> {Select, Lock} All OrderBy <Index>
        // From <EntityName> {Select, Lock} Range OrderBy <Index>
        // From <EntityName> {Select, Lock} Range By <Index> OrderBy <Index>
        // From <EntityName> {Select, Lock} Count
        // From <EntityName> {Select, Lock} Count By <Index>
        // 
        // From <EntityName> {Insert, Update} {Optional, One, Many}
        // From <EntityName> Upsert {One, Many}
        // 
        // From <EntityName> Delete {Optional, One, Many} By <Index>
        // From <EntityName> Delete All
        // 
        // <Index> ::=
        //     | <ColumnName> "-"?
        //     | <Index> "," <ColumnName> "-"?
        public static void VerifyQuerySyntax(this Schema s)
        {
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnQueryList)
                {
                    foreach (var q in t.QueryList.Queries)
                    {
                        //检查语法
                        if (q.Verb.OnSelect || q.Verb.OnLock)
                        {
                            if (q.Numeral.OnOptional)
                            {
                                if (q.By.Count != 0 && q.OrderBy.Count == 0) { continue; }
                            }
                            if (q.Numeral.OnOne)
                            {
                                if (q.By.Count != 0 && q.OrderBy.Count == 0) { continue; }
                            }
                            if (q.Numeral.OnMany)
                            {
                                if (q.By.Count != 0) { continue; }
                            }
                            if (q.Numeral.OnAll)
                            {
                                if (q.By.Count == 0) { continue; }
                            }
                            if (q.Numeral.OnRange)
                            {
                                if (q.OrderBy.Count != 0) { continue; }
                            }
                            if (q.Numeral.OnCount)
                            {
                                if (q.OrderBy.Count == 0) { continue; }
                            }
                        }
                        if (q.Verb.OnInsert || q.Verb.OnUpdate)
                        {
                            if (q.Numeral.OnOptional || q.Numeral.OnOne || q.Numeral.OnMany)
                            {
                                if (q.By.Count == 0 && q.OrderBy.Count == 0) { continue; }
                            }
                        }
                        if (q.Verb.OnUpsert)
                        {
                            if (q.Numeral.OnOne || q.Numeral.OnMany)
                            {
                                if (q.By.Count == 0 && q.OrderBy.Count == 0) { continue; }
                            }
                        }
                        if (q.Verb.OnDelete)
                        {
                            if (q.Numeral.OnOptional || q.Numeral.OnOne || q.Numeral.OnMany)
                            {
                                if (q.By.Count != 0 && q.OrderBy.Count == 0) { continue; }
                            }
                            if (q.Numeral.OnAll)
                            {
                                if (q.By.Count == 0 && q.OrderBy.Count == 0) { continue; }
                            }
                        }

                        var QueryLine = GetQueryLine(q);
                        throw new InvalidOperationException("InvalidQuery: {0}".Formats(QueryLine));
                    }
                }
            }
        }

        // EntityName必须对应于Entity
        // Insert Optional对应的Entity不得含有Identity列
        // Upsert对应的Entity不得含有Identity列，不得有多个PrimaryKey或UniqueKey
        // By索引和OrderBy索引中的名称必须是Entity中的列
        // By索引必须和Entity已经声明的Key一致但可以不用考虑列的顺序和每个列里数据的排列方法
        // By索引加上OrderBy索引中除去By索引的部分，必须和Entity已经声明的Key一致
        public static void VerifyQuerySemantics(this Schema s)
        {
            var Entities = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList();
            var EntityDict = Entities.ToDictionary(e => e.Name);
            var ColumnDict = Entities.ToDictionary(e => e.Name, e => e.Fields.Where(f => f.Attribute.OnColumn).ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase));
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnQueryList)
                {
                    foreach (var q in t.QueryList.Queries)
                    {
                        if (!EntityDict.ContainsKey(q.EntityName))
                        {
                            throw new InvalidOperationException(String.Format("EntityNameNotExist: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                        }
                        var e = EntityDict[q.EntityName];
                        if (q.Verb.OnInsert)
                        {
                            if (q.Numeral.OnOptional)
                            {
                                if (e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Any())
                                {
                                    throw new InvalidOperationException(String.Format("InsertOptionalNotValidOnEntityWithIdentityColumn: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                                }
                            }
                        }
                        else if (q.Verb.OnUpsert)
                        {
                            if (e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Any())
                            {
                                throw new InvalidOperationException(String.Format("UpsertNotValidOnEntityWithIdentityColumn: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                            }
                            if (e.UniqueKeys.Count != 0)
                            {
                                throw new InvalidOperationException(String.Format("UpsertNotValidOnEntityWithMultiplePrimaryKeyOrUniqueKey: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                            }
                        }

                        if (q.By.Count != 0)
                        {
                            var cd = ColumnDict[q.EntityName];
                            foreach (var Column in q.By)
                            {
                                if (!cd.ContainsKey(Column))
                                {
                                    throw new InvalidOperationException(String.Format("ColumnNotExist: '{0}.{1}' in {2}", q.EntityName, Column, GetQueryLine(q)));
                                }
                            }
                        }
                        if (q.OrderBy.Count != 0)
                        {
                            var cd = ColumnDict[q.EntityName];
                            foreach (var Column in q.OrderBy)
                            {
                                if (!cd.ContainsKey(Column.Name))
                                {
                                    throw new InvalidOperationException(String.Format("ColumnNotExist: '{0}.{1}' in {2}", q.EntityName, Column.Name, GetQueryLine(q)));
                                }
                            }
                        }
                        if ((q.By.Count != 0) || (q.OrderBy.Count != 0))
                        {
                            var ByColumns = new HashSet<String>(q.By, StringComparer.OrdinalIgnoreCase);
                            var ActualOrderBy = q.OrderBy.Where(c => !ByColumns.Contains(c.Name)).ToList();
                            Key SearchKey = null;
                            var IsByIndexExist = false;
                            foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                            {
                                if (k.Columns.Count < q.By.Count + ActualOrderBy.Count) { continue; }
                                if (!k.Columns.Take(q.By.Count).Zip(q.By, (Left, Right) => Left.Name.Equals(Right, StringComparison.OrdinalIgnoreCase)).Any(f => !f))
                                {
                                    IsByIndexExist = true;
                                    if (!k.Columns.Skip(q.By.Count).Take(ActualOrderBy.Count).Zip(ActualOrderBy, (Left, Right) => Left.Name.Equals(Right.Name) && (Left.IsDescending == Right.IsDescending)).Any(f => !f))
                                    {
                                        SearchKey = k;
                                        break;
                                    }
                                }
                            }
                            if (SearchKey == null)
                            {
                                if (!IsByIndexExist)
                                {
                                    throw new InvalidOperationException(String.Format("ByIndexNotExist: '{0}.{1}' in {2}", q.EntityName, GetByKeyString(q.By), GetQueryLine(q)));
                                }
                                else
                                {
                                    throw new InvalidOperationException(String.Format("OrderByIndexNotExist: '{0}.{1}' in {2}", q.EntityName, GetOrderByKeyString(q.OrderBy), GetQueryLine(q)));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void CheckDuplicatedNames<T>(IEnumerable<T> Values, Func<T, String> NameSelector, Func<T, String> ErrorMessageSelector)
        {
            var TypeNames = Values.Select(NameSelector).Distinct(StringComparer.OrdinalIgnoreCase);
            var DuplicatedNames = new HashSet<String>(Values.GroupBy(NameSelector, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);

            if (DuplicatedNames.Count > 0)
            {
                var l = new List<String>();
                foreach (var tp in Values.Where(p => DuplicatedNames.Contains(NameSelector(p))))
                {
                    l.Add(ErrorMessageSelector(tp));
                }
                var Message = String.Concat(l.Select(Line => Line + Environment.NewLine));
                throw new AggregateException(Message);
            }
        }

        public static String Name(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Name;
                case TypeDefTag.Entity:
                    return t.Entity.Name;
                case TypeDefTag.Enum:
                    return t.Enum.Name;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String Description(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Description;
                case TypeDefTag.Entity:
                    return t.Entity.Description;
                case TypeDefTag.Enum:
                    return t.Enum.Description;
                default:
                    throw new InvalidOperationException();
            }
        }

        private static String GetByKeyString(IEnumerable<String> ByKey)
        {
            if (ByKey.Count() == 1)
            {
                return ByKey.Single();
            }
            return "(" + String.Join(" ", ByKey) + ")";
        }
        private static String GetOrderByKeyString(IEnumerable<KeyColumn> OrderByKey)
        {
            var OrderByKeyColumnStrings = OrderByKey.Select(c => c.IsDescending ? c.Name + "-" : c.Name).ToList();
            if (OrderByKey.Count() == 1)
            {
                return OrderByKeyColumnStrings.Single();
            }
            return "(" + String.Join(" ", OrderByKeyColumnStrings) + ")";
        }
        private static String GetQueryLine(QueryDef q)
        {
            var QueryStrings = new List<String>();
            QueryStrings.Add("From");
            QueryStrings.Add(q.EntityName);
            QueryStrings.Add(q.Verb._Tag.ToString());
            QueryStrings.Add(q.Numeral._Tag.ToString());
            if (q.By.Count != 0)
            {
                QueryStrings.Add("By");
                var ByString = GetByKeyString(q.By);
                QueryStrings.Add(ByString);
            }
            if (q.OrderBy.Count != 0)
            {
                QueryStrings.Add("OrderBy");
                var OrderByString = GetOrderByKeyString(q.OrderBy);
                QueryStrings.Add(OrderByString);
            }
            var QueryLine = String.Join(" ", QueryStrings.ToArray());
            return QueryLine;
        }

        public static String FriendlyName(this QueryDef q)
        {
            var QueryStrings = new List<String>();
            QueryStrings.Add("From");
            QueryStrings.Add(q.EntityName);
            QueryStrings.Add(q.Verb._Tag.ToString());
            QueryStrings.Add(q.Numeral._Tag.ToString());
            if (q.By.Count != 0)
            {
                QueryStrings.Add("By");
                var ByString = String.Join("And", q.By);
                QueryStrings.Add(ByString);
            }
            if (q.OrderBy.Count != 0)
            {
                QueryStrings.Add("OrderBy");
                var OrderByString = q.OrderBy.FriendlyName();
                QueryStrings.Add(OrderByString);
            }
            var QueryLine = String.Join("", QueryStrings.ToArray());
            return QueryLine;
        }
        public static String FriendlyName(this IEnumerable<KeyColumn> Key)
        {
            return String.Join("And", Key.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name).ToArray());
        }

        public static String GetLimitedKeyName(String KeyType, String Identifier, int MaxLength)
        {
            var Name = KeyType + "_" + Identifier;
            if (Name.Length > MaxLength)
            {
                var Bytes = TextEncoding.UTF8.GetBytes(Name);
                var c = new CRC32();
                foreach (var b in Bytes)
                {
                    c.PushData(b);
                }
                var Hash = c.GetCRC32();
                Name = KeyType + "_" + Hash.ToString("X8");
                if (Name.Length > MaxLength)
                {
                    throw new InvalidOperationException("MaxLengthTooSmall");
                }
            }
            return Name;
        }
    }
}
