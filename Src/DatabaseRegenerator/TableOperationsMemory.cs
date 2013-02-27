//==========================================================================
//
//  File:        TableOperationsMemory.cs
//  Location:    Yuki.DatabaseRegenerator <Visual C#>
//  Description: Memory数据表操作
//  Version:     2013.02.27.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat.Semantics;

namespace Yuki.DatabaseRegenerator
{
    public static class TableOperationsMemory
    {
        public static void ImportTable(Dictionary<String, RelationSchema.EntityDef> TableMetas, Dictionary<String, String> EnumUnderlyingTypes, Dictionary<String, Dictionary<String, Int64>> EnumMetas, IWritableStream ms, KeyValuePair<string, List<Node>> t)
        {
            var CollectionName = t.Key;
            var Meta = TableMetas[CollectionName];
            var Name = Meta.Name;
            var Values = t.Value;
            var Columns = Meta.Fields.Where(f => f.Attribute.OnColumn).ToArray();

            ms.WriteInt32(Values.Count);
            foreach (var v in Values)
            {
                foreach (var f in Columns)
                {
                    var cvs = v.Stem.Children.Where(col => col._Tag == NodeTag.Stem && col.Stem.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (cvs.Length != 1)
                    {
                        throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", CollectionName, f.Name));
                    }

                    var cv = cvs.Single().Stem.Children.Single().Leaf;
                    String TypeName;
                    Boolean IsNullable;
                    if (f.Type.OnTypeRef)
                    {
                        TypeName = f.Type.TypeRef.Value;
                        IsNullable = false;
                    }
                    else if (f.Type.OnOptional)
                    {
                        TypeName = f.Type.Optional.Value;
                        IsNullable = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("InvalidType: {0}.{1}", CollectionName, f.Name));
                    }
                    try
                    {
                        if (IsNullable)
                        {
                            if (cv == "-")
                            {
                                ms.WriteInt32(0);
                                continue;
                            }
                            else
                            {
                                ms.WriteInt32(1);
                            }
                        }
                        if (EnumMetas.ContainsKey(TypeName))
                        {
                            if (IsNullable) { ms.WriteInt32(1); }
                            var e = EnumMetas[TypeName];
                            Int64 ev = 0;
                            if (e.ContainsKey(cv))
                            {
                                ev = e[cv];
                            }
                            else
                            {
                                ev = Int64.Parse(cv);
                            }
                            TypeName = EnumUnderlyingTypes[TypeName];
                            cv = ev.ToInvariantString();
                        }
                        if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                        {
                            ms.WriteByte((Byte)(Boolean.Parse(cv) ? 0xFF : 0));
                        }
                        else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                        {
                            var Bytes = TextEncoding.UTF16.GetBytes(cv);
                            ms.WriteInt32(Bytes.Length);
                            ms.Write(Bytes);
                        }
                        else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                        {
                            ms.WriteInt32(Int32.Parse(cv));
                        }
                        else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                        {
                            ms.WriteFloat64(Double.Parse(cv));
                        }
                        else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                        {
                            var Bytes = Regex.Split(cv.Trim(" \t\r\n".ToCharArray()), "( |\t|\r|\n)+", RegexOptions.ExplicitCapture).Select(s => Byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToArray();
                            ms.WriteInt32(Bytes.Length);
                            ms.Write(Bytes);
                        }
                        else
                        {
                            throw new InvalidOperationException("InvalidType: {0}".Formats(TypeName));
                        }
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(String.Format("InvalidField: {0}.{1} = {2}", CollectionName, f.Name, cv), e);
                    }
                }
            }
        }
    }
}
