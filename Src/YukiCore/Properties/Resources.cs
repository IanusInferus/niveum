//==========================================================================
//
//  File:        Resources.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 资源读取类
//  Version:     2016.10.11.
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

        public static Byte[] Xhtml { get { return GetResource("Xhtml"); } }
    }
}
