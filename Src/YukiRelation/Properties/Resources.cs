﻿//==========================================================================
//
//  File:        Resources.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 资源读取类
//  Version:     2016.10.12.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firefly;
using Firefly.Streaming;

namespace Yuki.RelationSchema.Properties
{
    public static class Resources
    {
        private static Byte[] GetResource(String Name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(String.Format("Yuki.RelationSchema.{0}.Schema.tree", Name)).AsReadableSeekable())
            {
                return s.Read((int)(s.Length));
            }
        }

        public static Byte[] CppMemory { get { return GetResource("CppMemory"); } }
        public static Byte[] CppPlain { get { return GetResource("CppPlain"); } }
        public static Byte[] CSharpCounted { get { return GetResource("CSharpCounted"); } }
        public static Byte[] CSharpKrustallos { get { return GetResource("CSharpKrustallos"); } }
        public static Byte[] CSharpLinqToEntities { get { return GetResource("CSharpLinqToEntities"); } }
        public static Byte[] CSharpMemory { get { return GetResource("CSharpMemory"); } }
        public static Byte[] CSharpMySql { get { return GetResource("CSharpMySql"); } }
        public static Byte[] CSharpPlain { get { return GetResource("CSharpPlain"); } }
        public static Byte[] CSharpSqlServer { get { return GetResource("CSharpSqlServer"); } }
        public static Byte[] MySql { get { return GetResource("MySql"); } }
        public static Byte[] PostgreSql { get { return GetResource("PostgreSql"); } }
        public static Byte[] Sqlite { get { return GetResource("Sqlite"); } }
        public static Byte[] TSql { get { return GetResource("TSql"); } }
        public static Byte[] Xhtml { get { return GetResource("Xhtml"); } }
    }
}
