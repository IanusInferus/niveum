//==========================================================================
//
//  File:        RelationSchemaExtensions.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构扩展
//  Version:     2013.03.27.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;

namespace Yuki.RelationSchema
{
    public static class RelationSchemaExtensions
    {
        public static IEnumerable<KeyValuePair<String, TypeDef>> GetMap(this Schema s)
        {
            return s.TypeRefs.Concat(s.Types).Where(t => !t.OnQueryList).Select(t => CollectionOperations.CreatePair(t.Name(), t));
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
            public String[] Columns;

            public override bool Equals(object obj)
            {
                var o = obj as ByIndex;
                if (o == null) { return false; }
                if (Columns.Length != o.Columns.Length) { return false; }
                if (Columns.Intersect(o.Columns, StringComparer.OrdinalIgnoreCase).Count() != Columns.Length) { return false; }
                return true;
            }

            public override int GetHashCode()
            {
                if (Columns.Length == 0) { return 0; }
                Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                return Columns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
            }
        }

        private class OrderByIndex
        {
            public KeyColumn[] Columns;

            public override bool Equals(object obj)
            {
                var o = obj as OrderByIndex;
                if (o == null) { return false; }
                if (Columns.Length != o.Columns.Length) { return false; }
                for (int k = 0; k < Columns.Length; k += 1)
                {
                    var c = Columns[k];
                    var oc = o.Columns[k];
                    if (!c.Name.Equals(oc.Name, StringComparison.OrdinalIgnoreCase)) { return false; }
                    if (c.IsDescending != oc.IsDescending) { return false; }
                }
                return true;
            }

            public override int GetHashCode()
            {
                if (Columns.Length == 0) { return 0; }
                Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                return Columns.Select(k => h(k.Name) ^ k.IsDescending.GetHashCode()).Aggregate((a, b) => a ^ b);
            }
        }

        public static void VerifyEntityForeignKeys(this Schema s)
        {
            var Entities = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToArray();
            var IndexDict = new Dictionary<String, HashSet<ByIndex>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Entities)
            {
                var h = new HashSet<ByIndex>();
                foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                {
                    for (int i = 1; i <= k.Columns.Length; i += 1)
                    {
                        var SubIndex = new ByIndex { Columns = k.Columns.Take(i).Select(c => c.Name).ToArray() };
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
        // {Select, Lock} Optional <EntityName> By <Index>
        // {Select, Lock} One <EntityName> By <Index>
        // {Select, Lock} Many <EntityName> By <Index>
        // {Select, Lock} Many <EntityName> By <Index> OrderBy <Index>
        // {Select, Lock} All <EntityName>
        // {Select, Lock} All <EntityName> OrderBy <Index>
        // {Select, Lock} Range <EntityName> OrderBy <Index>
        // {Select, Lock} Range <EntityName> By <Index> OrderBy <Index>
        // {Select, Lock} Count <EntityName>
        // {Select, Lock} Count <EntityName> By <Index>
        // 
        // {Insert, Update, Upsert} {One, Many} <EntityName>
        // 
        // Delete {Optional, One, Many} <EntityName> By <Index>
        // Delete All <EntityName>
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
                                if (q.By.Length != 0 && q.OrderBy.Length == 0) { continue; }
                            }
                            if (q.Numeral.OnOne)
                            {
                                if (q.By.Length != 0 && q.OrderBy.Length == 0) { continue; }
                            }
                            if (q.Numeral.OnMany)
                            {
                                if (q.By.Length != 0) { continue; }
                            }
                            if (q.Numeral.OnAll)
                            {
                                if (q.By.Length == 0) { continue; }
                            }
                            if (q.Numeral.OnRange)
                            {
                                if (q.OrderBy.Length != 0) { continue; }
                            }
                            if (q.Numeral.OnCount)
                            {
                                if (q.OrderBy.Length == 0) { continue; }
                            }
                        }
                        if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                        {
                            if (q.Numeral.OnOne || q.Numeral.OnMany)
                            {
                                if (q.By.Length == 0 && q.OrderBy.Length == 0) { continue; }
                            }
                        }
                        if (q.Verb.OnDelete)
                        {
                            if (q.Numeral.OnOptional || q.Numeral.OnOne || q.Numeral.OnMany)
                            {
                                if (q.By.Length != 0 && q.OrderBy.Length == 0) { continue; }
                            }
                            if (q.Numeral.OnAll)
                            {
                                if (q.By.Length == 0 && q.OrderBy.Length == 0) { continue; }
                            }
                        }

                        var QueryLine = GetQueryLine(q);
                        throw new InvalidOperationException("InvalidQuery: {0}".Formats(QueryLine));
                    }
                }
            }
        }

        // EntityName必须对应于Entity
        // Upsert对应的Entity不得含有Identity列，不得有多个PrimaryKey或UniqueKey
        // By索引必须和Entity已经声明的Key一致但可以不用考虑列的顺序和每个列里数据的排列方法
        // OrderBy索引必须和Entity已经声明的Key一致
        public static void VerifyQuerySemantics(this Schema s)
        {
            var Entities = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToArray();
            var EntityDict = Entities.ToDictionary(e => e.Name);
            var ByIndexDict = new Dictionary<String, HashSet<ByIndex>>(StringComparer.OrdinalIgnoreCase);
            var OrderByIndexDict = new Dictionary<String, HashSet<OrderByIndex>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Entities)
            {
                var h = new HashSet<ByIndex>();
                foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                {
                    for (int i = 1; i <= k.Columns.Length; i += 1)
                    {
                        var SubIndex = new ByIndex { Columns = k.Columns.Take(i).Select(c => c.Name).ToArray() };
                        if (!h.Contains(SubIndex))
                        {
                            h.Add(SubIndex);
                        }
                    }
                }
                ByIndexDict.Add(e.Name, h);
            }
            foreach (var e in Entities)
            {
                var h = new HashSet<OrderByIndex>();
                foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                {
                    for (int i = 1; i <= k.Columns.Length; i += 1)
                    {
                        var SubIndex = new OrderByIndex { Columns = k.Columns.Take(i).ToArray() };
                        if (!h.Contains(SubIndex))
                        {
                            h.Add(SubIndex);
                        }
                    }
                }
                OrderByIndexDict.Add(e.Name, h);
            }
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
                        if (q.Verb.OnUpsert)
                        {
                            if (e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Any())
                            {
                                throw new InvalidOperationException(String.Format("UpsertNotValidOnEntityWithIdentityColumn: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                            }
                            if (e.UniqueKeys.Length != 0)
                            {
                                throw new InvalidOperationException(String.Format("UpsertNotValidOnEntityWithMultiplePrimaryKeyOrUniqueKey: '{0}' in {1}", q.EntityName, GetQueryLine(q)));
                            }
                        }

                        if (q.By.Length != 0)
                        {
                            var bih = ByIndexDict[q.EntityName];
                            var bi = new ByIndex { Columns = q.By };
                            if (!bih.Contains(bi))
                            {
                                throw new InvalidOperationException(String.Format("ByIndexNotExist: '{0}.{1}' in {2}", q.EntityName, GetByKeyString(q.By), GetQueryLine(q)));
                            }
                        }
                        if (q.OrderBy.Length != 0)
                        {
                            var obih = OrderByIndexDict[q.EntityName];
                            var obi = new OrderByIndex { Columns = q.OrderBy };
                            if (!obih.Contains(obi))
                            {
                                throw new InvalidOperationException(String.Format("OrderByIndexNotExist: '{0}.{1}' in {2}", q.EntityName, GetOrderByKeyString(q.OrderBy), GetQueryLine(q)));
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

        private static String GetByKeyString(String[] ByKey)
        {
            if (ByKey.Length == 1)
            {
                return ByKey.Single();
            }
            return "(" + String.Join(" ", ByKey) + ")";
        }
        private static String GetOrderByKeyString(KeyColumn[] OrderByKey)
        {
            var OrderByKeyColumnStrings = OrderByKey.Select(c => c.IsDescending ? c.Name + "-" : c.Name).ToArray();
            if (OrderByKey.Length == 1)
            {
                return OrderByKeyColumnStrings.Single();
            }
            return "(" + String.Join(" ", OrderByKeyColumnStrings) + ")";
        }
        private static String GetQueryLine(QueryDef q)
        {
            var QueryStrings = new List<String>();
            QueryStrings.Add(q.Verb._Tag.ToString());
            QueryStrings.Add(q.Numeral._Tag.ToString());
            QueryStrings.Add(q.EntityName);
            if (q.By.Length != 0)
            {
                QueryStrings.Add("By");
                var ByString = GetByKeyString(q.By);
                QueryStrings.Add(ByString);
            }
            if (q.OrderBy.Length != 0)
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
            QueryStrings.Add(q.Verb._Tag.ToString());
            QueryStrings.Add(q.Numeral._Tag.ToString());
            QueryStrings.Add(q.EntityName);
            if (q.By.Length != 0)
            {
                QueryStrings.Add("By");
                var ByString = String.Join("And", q.By);
                QueryStrings.Add(ByString);
            }
            if (q.OrderBy.Length != 0)
            {
                QueryStrings.Add("OrderBy");
                var OrderByString = q.OrderBy.FriendlyName();
                QueryStrings.Add(OrderByString);
            }
            var QueryLine = String.Join("", QueryStrings.ToArray());
            return QueryLine;
        }
        public static String FriendlyName(this KeyColumn[] Key)
        {
            return String.Join("And", Key.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name).ToArray());
        }
    }
}
