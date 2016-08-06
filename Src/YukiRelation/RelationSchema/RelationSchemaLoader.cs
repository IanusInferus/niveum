//==========================================================================
//
//  File:        RelationSchemaLoader.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构加载器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema
{
    public class RelationSchemaLoaderResult
    {
        public Schema Schema;
        public Dictionary<Object, FileTextRange> Positions;
    }

    public sealed class RelationSchemaLoader
    {
        private List<TypeDef> Types = new List<TypeDef>();
        private List<TypeDef> TypeRefs = new List<TypeDef>();
        private List<String> Imports = new List<String>();
        private Dictionary<Object, FileTextRange> Positions = new Dictionary<Object, FileTextRange>();

        public RelationSchemaLoaderResult GetResult()
        {
            var PrimitiveNames = new HashSet<String>(TypeRefs.Concat(Types).Where(t => t.OnPrimitive).Select(t => t.Primitive.Name).Distinct());
            if (PrimitiveNames.Contains("Byte") && !PrimitiveNames.Contains("Binary"))
            {
                Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Binary", Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
            }

            var rslr = new RelationSchemaLoaderResult { Schema = new Schema { Types = Types, TypeRefs = TypeRefs, Imports = Imports }, Positions = Positions };
            RelationSchemaExtensions.VerifyDuplicatedNames(rslr);
            var Entities = Types.Where(t => t.OnEntity).ToDictionary(t => t.Entity.Name, t => t.Entity);
            foreach (var e in Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                FillEntity(e, Entities);
            }
            foreach (var e in Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                FillEntityNavigations(e, Entities);
            }
            return rslr;
        }

        public void LoadSchema(String TreePath)
        {
            LoadType(TreePath);
        }
        public void LoadSchema(String TreePath, String Content)
        {
            LoadType(TreePath, Content);
        }

        public void AddImport(String Import)
        {
            Imports.Add(Import);
        }

        public void LoadType(String TreePath)
        {
            if (Debugger.IsAttached)
            {
                LoadType(TreePath, Txt.ReadFile(TreePath));
            }
            else
            {
                try
                {
                    LoadType(TreePath, Txt.ReadFile(TreePath));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidSyntaxException("", new FileTextRange { Text = new Text { Path = TreePath, Lines = new List<TextLine> { } }, Range = TreeFormat.Optional<TextRange>.Empty }, ex);
                }
            }
        }
        public void LoadType(String TreePath, String Content)
        {
            var t = OS.TokenParser.BuildText(Content, TreePath);
            var fpr = FileParser.ParseFile(t);
            Types.AddRange(fpr.Schema.Types);
            TypeRefs.AddRange(fpr.Schema.TypeRefs.Where(td => td.OnPrimitive || td.OnEnum));
            Imports.AddRange(fpr.Schema.Imports);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
            }
        }
        public void LoadTypeRef(String TreePath)
        {
            if (Debugger.IsAttached)
            {
                LoadTypeRef(TreePath, Txt.ReadFile(TreePath));
            }
            else
            {
                try
                {
                    LoadTypeRef(TreePath, Txt.ReadFile(TreePath));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidSyntaxException("", new FileTextRange { Text = new Text { Path = TreePath, Lines = new List<TextLine> { } }, Range = TreeFormat.Optional<TextRange>.Empty }, ex);
                }
            }
        }
        public void LoadTypeRef(String TreePath, String Content)
        {
            var t = OS.TokenParser.BuildText(Content, TreePath);
            var fpr = FileParser.ParseFile(t);
            TypeRefs.AddRange(fpr.Schema.Types.Where(td => td.OnPrimitive || td.OnEnum));
            TypeRefs.AddRange(fpr.Schema.TypeRefs.Where(td => td.OnPrimitive || td.OnEnum));
            Imports.AddRange(fpr.Schema.Imports);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
            }
        }

        //在描述中可以在[]中使用如下几个标记
        //在实体上
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
        //  N                           Nullable，不能和FK同时使用
        //  FK和FNK只需要目标表上的键有索引，RFK和RFNK需要当前表和目标表的键都有索引
        //
        //标记可以叠加，如[CN:Users][PKC:Id1, Id2]，但FK、RFK、FNK、RFNK不能叠加
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

        private void FillFieldAttribute(VariableDef f, Dictionary<String, EntityDef> Entities)
        {
            var t = f.Type;
            var IsColumn = false;
            if (t.OnTypeRef)
            {
                IsColumn = !Entities.ContainsKey((String)(t.TypeRef));
            }
            else if (t.OnOptional)
            {
                IsColumn = !Entities.ContainsKey((String)(t.Optional));
            }
            else if (t.OnList)
            {
                IsColumn = !Entities.ContainsKey((String)(t.List));
            }
            else
            {
                throw new InvalidOperationException();
            }
            FieldAttribute fa = null;

            var IsNullable = false;

            if (IsColumn)
            {
                var IsIdentity = false;
                String TypeParameters = null;
                foreach (var a in f.Attributes)
                {
                    if (a.Key == "I")
                    {
                        IsIdentity = true;
                    }
                    else if (a.Key == "N")
                    {
                        IsNullable = true;
                    }
                    else if (a.Key == "P")
                    {
                        TypeParameters = String.Join(", ", a.Value);
                    }
                    else
                    {
                        throw new InvalidEvaluationException(String.Format("InvalidAttribute: {0}", a.Key), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, a);
                    }
                }

                //如果一个列上没有标记P，则自动添加[P:]
                if (TypeParameters == null)
                {
                    TypeParameters = "";
                }

                fa = FieldAttribute.CreateColumn(new ColumnAttribute { IsIdentity = IsIdentity, TypeParameters = TypeParameters });
            }
            else
            {
                if (f.Attributes.Where(a => a.Key == "FK" || a.Key == "RFK" || a.Key == "FNK" || a.Key == "RFNK").Count() > 1)
                {
                    throw new InvalidEvaluationException(String.Format("ConflictedAttributes: {0}", f.Name), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, f);
                }
                foreach (var a in f.Attributes)
                {
                    if (a.Key == "N")
                    {
                        IsNullable = true;
                        continue;
                    }
                    var km = GetKeyMap(String.Join(", ", a.Value));
                    if (a.Key == "FK")
                    {
                        var IsReverse = false;
                        var IsUnique = true;
                        var ThisKey = km.ThisKey;
                        var OtherKey = km.OtherKey;
                        fa = FieldAttribute.CreateNavigation(new NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                    }
                    else if (a.Key == "RFK")
                    {
                        var IsReverse = true;
                        var IsUnique = true;
                        var ThisKey = km.ThisKey;
                        var OtherKey = km.OtherKey;
                        fa = FieldAttribute.CreateNavigation(new NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                    }
                    else if (a.Key == "FNK")
                    {
                        var IsReverse = false;
                        var IsUnique = false;
                        var ThisKey = km.ThisKey;
                        var OtherKey = km.OtherKey;
                        fa = FieldAttribute.CreateNavigation(new NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                    }
                    else if (a.Key == "RFNK")
                    {
                        var IsReverse = true;
                        var IsUnique = false;
                        var ThisKey = km.ThisKey;
                        var OtherKey = km.OtherKey;
                        fa = FieldAttribute.CreateNavigation(new NavigationAttribute { IsReverse = IsReverse, IsUnique = IsUnique, ThisKey = ThisKey, OtherKey = OtherKey });
                    }
                    else
                    {
                        throw new InvalidEvaluationException(String.Format("InvalidAttribute: {0}", a.Key), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, a);
                    }
                }
                if (fa == null)
                {
                    fa = FieldAttribute.CreateNavigation(null);
                }
            }

            if (IsNullable && !t.OnOptional)
            {
                f.Type = TypeSpec.CreateOptional(t.TypeRef);
                if (Positions.ContainsKey(t))
                {
                    Positions.Add(f.Type, Positions[t]);
                }
            }

            f.Attribute = fa;
        }
        private void FillEntity(EntityDef e, Dictionary<String, EntityDef> Entities)
        {
            foreach (var f in e.Fields)
            {
                FillFieldAttribute(f, Entities);
                if (f.Attribute.OnNavigation && !f.Attribute.Navigation.IsReverse && f.Attribute.Navigation.IsUnique && f.Type.OnOptional)
                {
                    //如果N和FK同时使用，则报错
                    throw new InvalidEvaluationException(String.Format("InvalidAttribute: {0}", f.Name), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, f.Attributes);
                }
            }

            //如果不存在CN，则默认使用<EntityName>
            var CollectionName = e.Name;

            Key PrimaryKey = null;
            var UniqueKeys = new List<Key>();
            var NonUniqueKeys = new List<Key>();

            foreach (var a in e.Attributes)
            {
                if (a.Key == "CN")
                {
                    if (a.Value.Count != 1) { throw new InvalidEvaluationException(String.Format("InvalidAttribute: {0}", e.Name), Positions.ContainsKey(e.Attributes) ? Positions[e.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, e.Attributes); }
                    CollectionName = a.Value.Single();
                }
                else if (a.Key == "PK")
                {
                    PrimaryKey = new Key { Columns = GetColumns(a.Value), IsClustered = false };
                }
                else if (a.Key == "PKC")
                {
                    PrimaryKey = new Key { Columns = GetColumns(a.Value), IsClustered = true };
                }
                else if (a.Key == "UK")
                {
                    UniqueKeys.Add(new Key { Columns = GetColumns(a.Value), IsClustered = false });
                }
                else if (a.Key == "UKC")
                {
                    UniqueKeys.Add(new Key { Columns = GetColumns(a.Value), IsClustered = true });
                }
                else if (a.Key == "NK")
                {
                    NonUniqueKeys.Add(new Key { Columns = GetColumns(a.Value), IsClustered = false });
                }
                else if (a.Key == "NKC")
                {
                    NonUniqueKeys.Add(new Key { Columns = GetColumns(a.Value), IsClustered = true });
                }
            }

            //如果没有声明PK，则自动寻找名称为Id或者<EntityName>Id(不区分大小写)的列为[PK]，但不会将该字段记为[I]
            if (PrimaryKey == null)
            {
                var Ids = e.Fields.Where(f => f.Name.Equals("Id", StringComparison.Ordinal) || f.Name.Equals(e.Name + "Id", StringComparison.Ordinal)).ToList();
                if (Ids.Count > 1)
                {
                    throw new InvalidEvaluationException(String.Format("NoPrimaryKeyAttributeAndFoundMultipleDefaultPrimaryKeyField: {0}", e.Name), Positions.ContainsKey(e) ? Positions[e] : TreeFormat.Optional<FileTextRange>.Empty, e);
                }
                if (Ids.Count <= 0)
                {
                    throw new InvalidEvaluationException(String.Format("NoPrimaryKeyAttributeAndNoDefaultPrimaryKeyField: {0}", e.Name), Positions.ContainsKey(e) ? Positions[e] : TreeFormat.Optional<FileTextRange>.Empty, e);
                }
                var Id = Ids.Single();
                if (!Id.Attribute.OnColumn)
                {
                    throw new InvalidEvaluationException(String.Format("NoPrimaryKeyAttributeAndDefaultPrimaryKeyFieldNotPrimitive: {0}", e.Name), Positions.ContainsKey(e) ? Positions[e] : TreeFormat.Optional<FileTextRange>.Empty, e);
                }
                PrimaryKey = new Key { Columns = new List<KeyColumn> { new KeyColumn { Name = Id.Name, IsDescending = false } }, IsClustered = false };
            }

            //如果没有一个Key有C，则默认PK有C
            if ((!PrimaryKey.IsClustered) && !UniqueKeys.Any(k => k.IsClustered) && !NonUniqueKeys.Any(k => k.IsClustered))
            {
                PrimaryKey.IsClustered = true;
            }

            e.CollectionName = CollectionName;
            e.PrimaryKey = PrimaryKey;
            e.UniqueKeys = UniqueKeys;
            e.NonUniqueKeys = NonUniqueKeys;
        }
        private void FillEntityNavigations(EntityDef e, Dictionary<String, EntityDef> Entities)
        {
            //如果一个非简单类型属性(导航属性)没有标明外键或反外键，则
            //    1)如果有<Name>Id的列，且该列为简单类型，类型表的主键列数量为1，则将该字段标明为[FK:<Name>Id=<Type/ElementType>.<PrimaryKey>]
            //    2)如果类型表有一个<TableName>Id的列，且该列为简单类型，当前表的主键列数量为1，则将该字段标明为[RFK:<PrimaryKey>=<Type/ElementType>.<TableName>Id]
            foreach (var f in e.Fields)
            {
                if (f.Attribute.OnNavigation)
                {
                    String Name;
                    if (f.Type.OnTypeRef)
                    {
                        Name = f.Type.TypeRef.Value;
                    }
                    else if (f.Type.OnOptional)
                    {
                        Name = f.Type.Optional.Value;
                    }
                    else if (f.Type.OnList)
                    {
                        Name = f.Type.List.Value;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    if (!Entities.ContainsKey(Name))
                    {
                        throw new InvalidEvaluationException(String.Format("TableForNavigationFieldNotExist: {0}", f.Name), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, f);
                    }
                    var ExternalEntity = Entities[Name];

                    if (f.Attribute.Navigation == null)
                    {
                        var FieldNameIds = e.Fields.Where(rf => rf.Attribute.OnColumn && rf.Name.Equals(f.Name + "Id", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (FieldNameIds.Count == 1)
                        {
                            var FieldNameId = FieldNameIds.Single();
                            if (ExternalEntity.PrimaryKey.Columns.Count == 1)
                            {
                                f.Attribute.Navigation = new NavigationAttribute { IsReverse = false, IsUnique = true, ThisKey = new List<String> { FieldNameId.Name }, OtherKey = new List<String> { ExternalEntity.PrimaryKey.Columns.Select(c => c.Name).Single() } };
                                continue;
                            }
                        }

                        var TableNameIds = ExternalEntity.Fields.Where(rf => rf.Attribute.OnColumn && rf.Name.Equals(e.Name + "Id", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (TableNameIds.Count == 1)
                        {
                            var TableNameId = TableNameIds.Single();
                            if (e.PrimaryKey.Columns.Count == 1)
                            {
                                f.Attribute.Navigation = new NavigationAttribute { IsReverse = true, IsUnique = true, ThisKey = new List<String> { e.PrimaryKey.Columns.Select(c => c.Name).Single() }, OtherKey = new List<String> { TableNameId.Name } };
                                continue;
                            }
                        }

                        throw new InvalidEvaluationException(String.Format("NoForeignKeyOrReverseForeignKey: {0}", f.Name), Positions.ContainsKey(f.Attributes) ? Positions[f.Attributes] : TreeFormat.Optional<FileTextRange>.Empty, f);
                    }
                }
            }
        }

        private class KeyMap
        {
            public List<String> ThisKey;
            public List<String> OtherKey;
        }
        private KeyMap GetKeyMap(String Parameters)
        {
            var Keys = Regex.Split(Parameters, "=");
            if (Keys.Length != 2)
            {
                throw new InvalidOperationException(String.Format("映射无效: {0}", Parameters));
            }
            var ThisKey = Keys[0].Split(',').Select(f => f.Trim(' ')).ToList();
            var OtherKey = Keys[1].Split(',').Select(f => f.Trim(' ')).ToList();
            if (ThisKey.Count != OtherKey.Count || ThisKey.Count == 0)
            {
                throw new InvalidOperationException(String.Format("映射无效: {0}", Parameters));
            }
            return new KeyMap { ThisKey = ThisKey, OtherKey = OtherKey };
        }

        private List<KeyColumn> GetColumns(List<String> Parameters)
        {
            var Columns = new List<KeyColumn>();
            foreach (var cs in Parameters.Select(f => f.Trim(' ')).ToArray())
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
            return Columns;
        }
    }
}
