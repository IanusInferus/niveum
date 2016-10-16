//==========================================================================
//
//  File:        Resources.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 资源读取类
//  Version:     2016.10.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firefly;
using Firefly.Streaming;

namespace Yuki.ExpressionSchema.Properties
{
    public static class Resources
    {
        private static Byte[] GetResource(String Name)
        {
            using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(String.Format("Yuki.ExpressionSchema.{0}.Schema.tree", Name)).AsReadableSeekable())
            {
                return s.Read((int)(s.Length));
            }
        }

        public static Byte[] CppBinaryLoader { get { return GetResource("CppBinaryLoader"); } }
    }
}
