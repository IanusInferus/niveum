﻿//==========================================================================
//
//  File:        RelationSchemaTranslator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构转换器
//  Version:     2012.06.19.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OS = Yuki.ObjectSchema;
using RS = Yuki.RelationSchema;

namespace Yuki.RelationSchema
{
    //在描述中可以在[]中使用如下几个标记
    //在Record上
    //  CN:<Name>                   CollectionName
    //  PK[C]:<Columns>             PrimaryKey
    //      C                       Clustered
    //  UK[C]:<Columns>             UniqueKey
    //      C                       Clustered
    //  NK[C]:<Columns>             NonUniqueKey
    //      C                       Clustered
    //在列上
    //  I                           Identity
    //  N                           Nullable
    //  P:<Params>                  TypeParameters, 例如字符串长度[P:50]
    //在导航属性上
    //  FK:<Columns>=<Columns>      ForeignKey
    //  RFK:<Columns>=<Columns>     ReverseForeignKey
    //  FNK:<Columns>=<Columns>     ForeignNonUniqueKey 即当前键可指向多种物体，没有外键约束
    //  RFNK:<Columns>=<Columns>    ReverseForeignNonUniqueKey 即目标键可指向多种物体，没有外键约束
    //  FK和FNK只需要目标表上的键有索引，RFK和RFNK需要当前表和目标表的键都有索引
    //
    //标记可以叠加，如[CN:Users][PKC:Id1, Id2]
    //外键中可有多个键，例如[FK:Id1, Id2=Id1, Id2]
    //索引列上可以标注减号表示递减，比如[PKC:Id1, Id2-]
    //
    //如果不存在CN，则默认使用<EntityName>
    //如果没有声明PK，则自动寻找名称为Id或者<EntityName>Id(不区分大小写)的列为[PK]，但不会将该字段记为[I]
    //如果没有一个Key有C，则默认PK有C
    //如果一个非简单类型属性(导航属性)没有标明外键或反外键，则
    //    1)如果有<Name>Id的列，且该列为简单类型，类型表的主键列数量为1，则将该字段标明为[FK:<Name>Id=<Type/ElementType>.<PrimaryKey>]
    //    2)如果类型表有一个<TableName>Id的列，且该列为简单类型，当前表的主键列数量为1，则将该字段标明为[RFK:<PrimaryKey>=<Type/ElementType>.<TableName>Id]
    //如果一个String类型的列上没有标记P，则报错

    public static class RelationSchemaTranslator
    {
        public static RS.Schema Translate(OS.Schema Schema)
        {
            var s = (new Translator { Schema = Schema }).Analyze();
            CheckForeignKeyIndex(s);
            return s;
        }

        private class Index
        {
            public String[] Columns;

            public override bool Equals(object obj)
            {
                var o = obj as Index;
                if (o == null) { return false; }
                if (Columns.Length != o.Columns.Length) { return false; }
                if (Columns.Intersect(o.Columns, StringComparer.OrdinalIgnoreCase).Count() != Columns.Length) { return false; }
                return true;
            }

            public override int GetHashCode()
            {
                Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                return Columns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
            }
        }
        private static void CheckForeignKeyIndex(RS.Schema s)
        {
            var Records = s.Types.Where(r => r.OnRecord).Select(r => r.Record).ToArray();
            var IndexDict = new Dictionary<String, HashSet<Index>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Records)
            {
                var h = new HashSet<Index>();
                foreach (var k in (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys).Concat(r.NonUniqueKeys))
                {
                    for (int i = 1; i <= k.Columns.Length; i += 1)
                    {
                        var SubIndex = new Index { Columns = k.Columns.Take(i).Select(c => c.Name).ToArray() };
                        if (!h.Contains(SubIndex))
                        {
                            h.Add(SubIndex);
                        }
                    }
                }
                IndexDict.Add(r.Name, h);
            }
            foreach (var r in Records)
            {
                foreach (var a in r.Fields.Where(f => f.Attribute.OnNavigation))
                {
                    String OtherRecordName;
                    if (a.Type.OnTypeRef)
                    {
                        OtherRecordName = a.Type.TypeRef.Value;
                    }
                    else
                    {
                        OtherRecordName = a.Type.List.ElementType.TypeRef.Value;
                    }

                    //FK和FNK只需要目标表上的键有索引，RFK和RFNK需要当前表和目标表的键都有索引
                    if (a.Attribute.Navigation.IsReverse)
                    {
                        var ThisIndex = new Index { Columns = a.Attribute.Navigation.ThisKey };
                        var h = IndexDict[r.Name];
                        if (!h.Contains(ThisIndex))
                        {
                            throw new InvalidOperationException(String.Format("ThisKeyIsNotIndex: {0}->{1} FK:{2}={3}", r.Name, OtherRecordName, String.Join(", ", a.Attribute.Navigation.ThisKey), String.Join(", ", a.Attribute.Navigation.OtherKey)));
                        }
                    }
                    {
                        var OtherIndex = new Index { Columns = a.Attribute.Navigation.OtherKey };
                        var h = IndexDict[OtherRecordName];
                        if (!h.Contains(OtherIndex))
                        {
                            throw new InvalidOperationException(String.Format("OtherKeyIsNotIndex: {0}->{1} FK:{2}={3}", r.Name, OtherRecordName, String.Join(", ", a.Attribute.Navigation.ThisKey), String.Join(", ", a.Attribute.Navigation.OtherKey)));
                        }
                    }
                }
            }
        }

        private class Translator
        {
            public OS.Schema Schema;
            private HashSet<String> OPrimitives = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            private HashSet<String> OEnums = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            private Dictionary<String, RS.Record> Records = new Dictionary<String, RS.Record>();

            public RS.Schema Analyze()
            {
                var TypeRefs = new List<RS.TypeDef>();
                var Types = new List<RS.TypeDef>();

                foreach (var t in Schema.TypeRefs)
                {
                    if (t.OnPrimitive)
                    {
                        if (!OPrimitives.Contains(t.Primitive.Name))
                        {
                            OPrimitives.Add(t.Primitive.Name);
                        }
                    }
                    else if (t.OnEnum)
                    {
                        if (!OEnums.Contains(t.Enum.Name))
                        {
                            OEnums.Add(t.Enum.Name);
                        }
                    }
                    else
                    {
                        //throw new InvalidOperationException("引用的类型中有Primitive、Enum以外的类型声明。");
                    }
                }

                foreach (var t in Schema.Types)
                {
                    if (t.OnPrimitive)
                    {
                        if (!OPrimitives.Contains(t.Primitive.Name))
                        {
                            OPrimitives.Add(t.Primitive.Name);
                        }
                    }
                    else if (t.OnEnum)
                    {
                        if (!OEnums.Contains(t.Enum.Name))
                        {
                            OEnums.Add(t.Enum.Name);
                        }
                    }
                }

                var ltfRefs = new OS.TupleAndGenericTypeSpecFetcher();
                ltfRefs.PushTypeDefs(Schema.TypeRefs);
                var GenericTypeSpecsRefs = ltfRefs.GetGenericTypeSpecs();
                foreach (var t in GenericTypeSpecsRefs)
                {
                    if (t.OnGenericTypeSpec)
                    {
                        if (t.GenericTypeSpec.TypeSpec.OnTypeRef && t.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && t.GenericTypeSpec.GenericParameterValues.Length == 1)
                        {
                            var Parameter = t.GenericTypeSpec.GenericParameterValues.Single();
                            if (Parameter.OnTypeSpec && Parameter.TypeSpec.OnTypeRef && Parameter.TypeSpec.TypeRef.Name == "Byte")
                            {
                                if (!OPrimitives.Contains("Binary"))
                                {
                                    OPrimitives.Add("Binary");
                                    TypeRefs.Add(RS.TypeDef.CreatePrimitive(new Primitive { Name = "Binary", Description = "二进制数据" }));
                                }
                            }
                        }
                    }
                }

                var ltf = new OS.TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.Types);
                var GenericTypeSpecss = ltf.GetGenericTypeSpecs();
                foreach (var t in GenericTypeSpecss)
                {
                    if (t.OnGenericTypeSpec)
                    {
                        if (t.GenericTypeSpec.TypeSpec.OnTypeRef && t.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && t.GenericTypeSpec.GenericParameterValues.Length == 1)
                        {
                            var Parameter = t.GenericTypeSpec.GenericParameterValues.Single();
                            if (Parameter.OnTypeSpec && Parameter.TypeSpec.OnTypeRef && Parameter.TypeSpec.TypeRef.Name == "Byte")
                            {
                                if (!OPrimitives.Contains("Binary"))
                                {
                                    OPrimitives.Add("Binary");
                                    Types.Add(RS.TypeDef.CreatePrimitive(new Primitive { Name = "Binary", Description = "二进制数据" }));
                                }
                            }
                        }
                    }
                }

                foreach (var t in Schema.TypeRefs)
                {
                    if (t.OnPrimitive)
                    {
                        TypeRefs.Add(RS.TypeDef.CreatePrimitive(TranslatePrimitive(t.Primitive)));
                    }
                    else if (t.OnEnum)
                    {
                        TypeRefs.Add(RS.TypeDef.CreateEnum(TranslateEnum(t.Enum)));
                    }
                }

                foreach (var t in Schema.Types)
                {
                    if (t.OnPrimitive)
                    {
                        Types.Add(RS.TypeDef.CreatePrimitive(TranslatePrimitive(t.Primitive)));
                    }
                    else if (t.OnEnum)
                    {
                        Types.Add(RS.TypeDef.CreateEnum(TranslateEnum(t.Enum)));
                    }
                    else if (t.OnRecord)
                    {
                        var r = TranslateRecord(t.Record);
                        Records.Add(r.Name, r);
                        Types.Add(RS.TypeDef.CreateRecord(r));
                    }
                    else
                    {
                        throw new InvalidOperationException("有Primitive、Enum、Record以外的类型声明。");
                    }
                }

                foreach (var r in Records.Values)
                {
                    FillRecordNavigations(r);
                }

                return new RS.Schema { Types = Types.ToArray(), TypeRefs = TypeRefs.ToArray(), Imports = Schema.Imports.ToArray() };
            }

            private RS.Primitive TranslatePrimitive(OS.PrimitiveDef e)
            {
                return new RS.Primitive { Name = e.Name, Description = e.Description };
            }

            private RS.Literal TranslateLiteral(OS.LiteralDef l)
            {
                return new RS.Literal { Name = l.Name, Value = l.Value, Description = l.Description };
            }
            private RS.Enum TranslateEnum(OS.EnumDef e)
            {
                return new RS.Enum { Name = e.Name, UnderlyingType = TranslateTypeSpec(e.UnderlyingType), Literals = e.Literals.Select(l => TranslateLiteral(l)).ToArray(), Description = e.Description };
            }

            private RS.TypeSpec TranslateTypeSpec(OS.TypeSpec t)
            {
                if (t.OnTypeRef)
                {
                    return RS.TypeSpec.CreateTypeRef(new RS.TypeRef { Value = t.TypeRef.Name });
                }
                else if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && t.GenericTypeSpec.TypeSpec.TypeRef.Name == "List")
                {
                    if (t.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        var Parameter = t.GenericTypeSpec.GenericParameterValues.Single();
                        if (Parameter.OnTypeSpec && Parameter.TypeSpec.OnTypeRef && Parameter.TypeSpec.TypeRef.Name == "Byte")
                        {
                            return RS.TypeSpec.CreateTypeRef(new RS.TypeRef { Value = "Binary" });
                        }
                    }
                    return RS.TypeSpec.CreateList(new RS.List { ElementType = TranslateTypeSpec(t.GenericTypeSpec.GenericParameterValues.Single().TypeSpec) });
                }
                else
                {
                    throw new InvalidOperationException("有TypeRef、List以外的类型规格。");
                }
            }

            private RS.Field TranslateField(OS.VariableDef f)
            {
                var t = TranslateTypeSpec(f.Type);
                var IsColumn = t.OnTypeRef && (OPrimitives.Contains(t.TypeRef.Value) || OEnums.Contains(t.TypeRef.Value));
                var dc = Decompose(f.Description);
                RS.FieldAttribute fa = null;

                if (IsColumn)
                {
                    var IsIdentity = false;
                    var IsNullable = false;
                    String TypeParameters = null;
                    foreach (var a in dc.Attributes)
                    {
                        if (a.Name == "I")
                        {
                            IsIdentity = true;
                        }
                        else if (a.Name == "N")
                        {
                            IsNullable = true;
                        }
                        else if (a.Name == "P")
                        {
                            TypeParameters = a.Parameters;
                        }
                    }

                    //如果一个列上没有标记P，则自动添加[P:]
                    if (TypeParameters == null)
                    {
                        TypeParameters = "";
                    }

                    fa = RS.FieldAttribute.CreateColumn(new RS.ColumnAttribute { IsIdentity = IsIdentity, IsNullable = IsNullable, TypeParameters = TypeParameters });
                }
                else
                {
                    if (dc.Attributes.Length > 1)
                    {
                        throw new InvalidOperationException(String.Format("映射数过多: {0}", f.Description));
                    }
                    foreach (var a in dc.Attributes)
                    {
                        var km = GetKeyMap(a.Parameters);
                        if (a.Name == "FK")
                        {
                            var IsReverse = false;
                            var IsUnique = true;
                            var ThisKey = km.ThisKey;
                            var OtherKey = km.OtherKey;
                            fa = RS.FieldAttribute.CreateNavigation(new RS.NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                        }
                        else if (a.Name == "RFK")
                        {
                            var IsReverse = true;
                            var IsUnique = true;
                            var ThisKey = km.ThisKey;
                            var OtherKey = km.OtherKey;
                            fa = RS.FieldAttribute.CreateNavigation(new RS.NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                        }
                        else if (a.Name == "FNK")
                        {
                            var IsReverse = false;
                            var IsUnique = false;
                            var ThisKey = km.ThisKey;
                            var OtherKey = km.OtherKey;
                            fa = RS.FieldAttribute.CreateNavigation(new RS.NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                        }
                        else if (a.Name == "RFNK")
                        {
                            var IsReverse = true;
                            var IsUnique = false;
                            var ThisKey = km.ThisKey;
                            var OtherKey = km.OtherKey;
                            fa = RS.FieldAttribute.CreateNavigation(new RS.NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                        }
                    }
                    if (fa == null)
                    {
                        fa = RS.FieldAttribute.CreateNavigation(null);
                    }
                }

                return new RS.Field { Name = f.Name, Type = t, Description = dc.Description, Attribute = fa };
            }
            private RS.Record TranslateRecord(OS.RecordDef r)
            {
                var dc = Decompose(r.Description);
                var Fields = r.Fields.Select(f => TranslateField(f)).ToArray();

                foreach (var f in Fields)
                {
                    if (f.Attribute.OnColumn && f.Attribute.Column.TypeParameters == "")
                    {
                        //如果一个String类型的列上没有标记P，则报错
                        if (f.Type.OnTypeRef && f.Type.TypeRef.Value.Equals("String"))
                        {
                            throw new InvalidOperationException(String.Format("字符串没有标明长度，请添加[P:max]等: {0}.{1}", r.Name, f.Name));
                        }
                    }
                }

                //如果不存在CN，则默认使用<EntityName>
                var CollectionName = r.Name;

                RS.Key PrimaryKey = null;
                var UniqueKeys = new List<RS.Key>();
                var NonUniqueKeys = new List<RS.Key>();

                foreach (var a in dc.Attributes)
                {
                    if (a.Name == "CN")
                    {
                        CollectionName = a.Parameters;
                    }
                    else if (a.Name == "PK")
                    {
                        PrimaryKey = new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = false };
                    }
                    else if (a.Name == "PKC")
                    {
                        PrimaryKey = new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = true };
                    }
                    else if (a.Name == "UK")
                    {
                        UniqueKeys.Add(new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = false });
                    }
                    else if (a.Name == "UKC")
                    {
                        UniqueKeys.Add(new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = true });
                    }
                    else if (a.Name == "NK")
                    {
                        NonUniqueKeys.Add(new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = false });
                    }
                    else if (a.Name == "NKC")
                    {
                        NonUniqueKeys.Add(new RS.Key { Columns = GetColumns(a.Parameters), IsClustered = true });
                    }
                }

                //如果没有声明PK，则自动寻找名称为Id或者<EntityName>Id(不区分大小写)的列为[PK]，但不会将该字段记为[I]
                if (PrimaryKey == null)
                {
                    var Ids = Fields.Where(f => f.Name.Equals("Id", StringComparison.Ordinal) || f.Name.Equals(r.Name + "Id", StringComparison.Ordinal)).ToArray();
                    if (Ids.Length > 1)
                    {
                        throw new InvalidOperationException(String.Format("没有标注主键，且找到多个默认主键字段: {0}", r.Name));
                    }
                    if (Ids.Length <= 0)
                    {
                        throw new InvalidOperationException(String.Format("没有标注主键，且找不到默认主键字段: {0}", r.Name));
                    }
                    var Id = Ids.Single();
                    if (!Id.Attribute.OnColumn)
                    {
                        throw new InvalidOperationException(String.Format("没有标注主键，且默认主键字段类型不为简单类型: {0}", r.Name));
                    }
                    PrimaryKey = new RS.Key { Columns = new KeyColumn[] { new KeyColumn { Name = Id.Name, IsDescending = false } }, IsClustered = false };
                    //Id.Attribute.Column.IsIdentity = true;
                }

                //如果没有一个Key有C，则默认PK有C
                if ((!PrimaryKey.IsClustered) && !UniqueKeys.Any(k => k.IsClustered) && !NonUniqueKeys.Any(k => k.IsClustered))
                {
                    PrimaryKey.IsClustered = true;
                }

                foreach (var k in (new RS.Key[] { PrimaryKey }).Concat(UniqueKeys).Concat(NonUniqueKeys))
                {
                    foreach (var c in k.Columns)
                    {
                        if (Fields.Where(f => f.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).Count() == 0)
                        {
                            throw new InvalidOperationException(String.Format("键中的字段不存在: {0}.{1}", r.Name, c.Name));
                        }
                    }
                }

                return new RS.Record { Name = r.Name, CollectionName = CollectionName, Fields = Fields, PrimaryKey = PrimaryKey, UniqueKeys = UniqueKeys.ToArray(), NonUniqueKeys = NonUniqueKeys.ToArray(), Description = dc.Description };
            }
            private void FillRecordNavigations(RS.Record r)
            {
                //如果一个非简单类型属性(导航属性)没有标明外键或反外键，则
                //    1)如果有<Name>Id的列，且该列为简单类型，类型表的主键列数量为1，则将该字段标明为[FK:<Name>Id=<Type/ElementType>.<PrimaryKey>]
                //    2)如果类型表有一个<TableName>Id的列，且该列为简单类型，当前表的主键列数量为1，则将该字段标明为[RFK:<PrimaryKey>=<Type/ElementType>.<TableName>Id]
                foreach (var f in r.Fields)
                {
                    if (f.Attribute.OnNavigation)
                    {
                        var t = f.Type;
                        if (t.OnList)
                        {
                            t = t.List.ElementType;
                        }
                        if (t.OnList)
                        {
                            throw new InvalidOperationException(String.Format("导航属性的类型不能为多重List: {0}.{1}", r.Name, f.Name));
                        }
                        if (!Records.ContainsKey(t.TypeRef.Value))
                        {
                            throw new InvalidOperationException(String.Format("表'{2}'不存在: {0}.{1}", r.Name, f.Name, t.TypeRef.Value));
                        }
                        var TypeRecord = Records[t.TypeRef.Value];

                        if (f.Attribute.Navigation == null)
                        {
                            var FieldNameIds = r.Fields.Where(rf => rf.Attribute.OnColumn && rf.Name.Equals(f.Name + "Id", StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (FieldNameIds.Length == 1)
                            {
                                var FieldNameId = FieldNameIds.Single();
                                if (TypeRecord.PrimaryKey.Columns.Length == 1)
                                {
                                    f.Attribute.Navigation = new RS.NavigationAttribute { IsReverse = false, IsUnique = true, ThisKey = new String[] { FieldNameId.Name }, OtherKey = new String[] { TypeRecord.PrimaryKey.Columns.Select(c => c.Name).Single() } };
                                    continue;
                                }
                            }

                            var TableNameIds = TypeRecord.Fields.Where(rf => rf.Attribute.OnColumn && rf.Name.Equals(r.Name + "Id", StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (TableNameIds.Length == 1)
                            {
                                var TableNameId = TableNameIds.Single();
                                if (r.PrimaryKey.Columns.Length == 1)
                                {
                                    f.Attribute.Navigation = new RS.NavigationAttribute { IsReverse = true, IsUnique = true, ThisKey = new String[] { r.PrimaryKey.Columns.Select(c => c.Name).Single() }, OtherKey = new String[] { TableNameId.Name } };
                                    continue;
                                }
                            }

                            throw new InvalidOperationException(String.Format("没有外键或反外键标记，且默认规则无法找到外键或反外键: {0}.{1}", r.Name, f.Name));
                        }
                    }
                }
            }

            private class Attribute
            {
                public String Name;
                public String Parameters;
            }
            private class DescriptionComposite
            {
                public Attribute[] Attributes;
                public String Description;
            }
            private Regex rAttribute = new Regex(@"\[(?<Name>CN|PK|PKC|UK|UKC|NK|NKC|P|FK|RFK|FNK|RFNK):\s*(?<Params>.*?)\s*\]|\[(?<Name>I|N)\]", RegexOptions.ExplicitCapture);
            private DescriptionComposite Decompose(String Description)
            {
                var l = new List<Attribute>();
                var d = rAttribute.Replace(Description,
                    m =>
                    {
                        l.Add(new Attribute { Name = m.Result("${Name}"), Parameters = m.Result("${Params}") ?? "" });
                        return "";
                    }
                );
                return new DescriptionComposite { Attributes = l.ToArray(), Description = d };
            }

            private class KeyMap
            {
                public String[] ThisKey;
                public String[] OtherKey;
            }
            private KeyMap GetKeyMap(String Parameters)
            {
                var Keys = Regex.Split(Parameters, "=");
                if (Keys.Length != 2)
                {
                    throw new InvalidOperationException(String.Format("映射无效: {0}", Parameters));
                }
                var ThisKey = Keys[0].Split(',').Select(f => f.Trim(' ')).ToArray();
                var OtherKey = Keys[1].Split(',').Select(f => f.Trim(' ')).ToArray();
                if (ThisKey.Length != OtherKey.Length || ThisKey.Length == 0)
                {
                    throw new InvalidOperationException(String.Format("映射无效: {0}", Parameters));
                }
                return new KeyMap { ThisKey = ThisKey, OtherKey = OtherKey };
            }

            private KeyColumn[] GetColumns(String Parameters)
            {
                var Columns = new List<KeyColumn>();
                foreach (var cs in Parameters.Split(',').Select(f => f.Trim(' ')).ToArray())
                {
                    if (cs.EndsWith("-"))
                    {
                        Columns.Add(new KeyColumn { Name = cs.Substring(0, cs.Length - 1), IsDescending = true });
                    }
                    else
                    {
                        Columns.Add(new KeyColumn { Name = cs, IsDescending = false });
                    }
                }
                if (Columns.Count == 0)
                {
                    throw new InvalidOperationException(String.Format("键无效: {0}", Parameters));
                }
                return Columns.ToArray();
            }
        }
    }
}
