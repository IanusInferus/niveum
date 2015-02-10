//==========================================================================
//
//  File:        Resources.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 资源读取类
//  Version:     2012.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firefly;
using Firefly.Streaming;

namespace Yuki.ObjectSchema.Properties
{
    public static class Resources
    {
        private static Byte[] GetResource(String Name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(String.Format("Yuki.ObjectSchema.{0}.Schema.tree", Name)).AsReadableSeekable())
            {
                return s.Read((int)(s.Length));
            }
        }

        public static Byte[] ActionScript { get { return GetResource("ActionScript"); } }
        public static Byte[] ActionScriptBinary { get { return GetResource("ActionScriptBinary"); } }
        public static Byte[] ActionScriptJson { get { return GetResource("ActionScriptJson"); } }
        public static Byte[] Cpp { get { return GetResource("Cpp"); } }
        public static Byte[] CppBinary { get { return GetResource("CppBinary"); } }
        public static Byte[] CSharp { get { return GetResource("CSharp"); } }
        public static Byte[] CSharpCompatible { get { return GetResource("CSharpCompatible"); } }
        public static Byte[] CSharpBinary { get { return GetResource("CSharpBinary"); } }
        public static Byte[] CSharpJson { get { return GetResource("CSharpJson"); } }
        public static Byte[] CSharpRetry { get { return GetResource("CSharpRetry"); } }
        public static Byte[] Haxe { get { return GetResource("Haxe"); } }
        public static Byte[] HaxeJson { get { return GetResource("HaxeJson"); } }
        public static Byte[] Java { get { return GetResource("Java"); } }
        public static Byte[] JavaBinary { get { return GetResource("JavaBinary"); } }
        public static Byte[] VB { get { return GetResource("VB"); } }
        public static Byte[] Xhtml { get { return GetResource("Xhtml"); } }
    }
}
