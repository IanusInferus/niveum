//==========================================================================
//
//  File:        ObjectSchemaTemplate.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构模板
//  Version:     2012.02.24.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;

namespace Yuki.ObjectSchema
{
    public class ObjectSchemaTemplate
    {
        public String[] Keywords;
        public PrimitiveMapping[] PrimitiveMappings;
        public Template[] Templates;
    }

    public class PrimitiveMapping
    {
        public String Name;
        public String PlatformName;
    }

    public class Template
    {
        public String Name;
        public String Value;
    }
}
