//==========================================================================
//
//  File:        ObjectSchemaTemplate.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构模板
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.Mapping.MetaSchema;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema
{
    public class ObjectSchemaTemplate
    {
        public List<String> Keywords;
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

    public class ObjectSchemaTemplateInfo
    {
        public HashSet<String> Keywords;
        public Dictionary<String, PrimitiveMapping> PrimitiveMappings;
        public Dictionary<String, Template> Templates;

        public ObjectSchemaTemplateInfo(ObjectSchemaTemplate Template)
        {
            Keywords = new HashSet<String>(Template.Keywords, StringComparer.Ordinal);
            PrimitiveMappings = Template.PrimitiveMappings.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
            Templates = Template.Templates.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        }

        public static ObjectSchemaTemplateInfo FromBinary(Byte[] Bytes)
        {
            XElement x;
            using (ByteArrayStream s = new ByteArrayStream(Bytes))
            {
                using (var sr = Txt.CreateTextReader(s.AsNewReading(), TextEncoding.Default, true))
                {
                    x = TreeFile.ReadFile(sr);
                }
            }

            XmlSerializer xs = new XmlSerializer();
            var t = xs.Read<ObjectSchemaTemplate>(x);
            var ti = new ObjectSchemaTemplateInfo(t);
            return ti;
        }
    }
}
