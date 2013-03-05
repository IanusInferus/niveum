﻿//==========================================================================
//
//  File:        TreeBinaryConverter.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: Tree格式数据与二进制数据转换器
//  Version:     2012.04.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Firefly.Streaming;
using Firefly.Mapping;
using Firefly.Mapping.Binary;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema
{
    public class TreeBinaryConverter
    {
        private BinarySerializer bs;
        private XmlSerializer xs;

        public TreeBinaryConverter()
        {
            bs = new BinarySerializer();
            var st = new StringTranslator();
            bs.PutReaderTranslator(st);
            bs.PutWriterTranslator(st);
            bs.PutCounterTranslator(st);
            xs = new XmlSerializer(true);
        }

        public Byte[] TreeToBinary(Type t, XElement x)
        {
            var tbc = Activator.CreateInstance(typeof(TypedTreeBinaryConverter<>).MakeGenericType(t), bs, xs) as ITreeBinaryConverter;
            return tbc.TreeToBinary(x);
        }
        public XElement BinaryToTree(Type t, Byte[] b)
        {
            var tbc = Activator.CreateInstance(typeof(TypedTreeBinaryConverter<>).MakeGenericType(t), bs, xs) as ITreeBinaryConverter;
            return tbc.BinaryToTree(b);
        }
        public Byte[] TreeToBinary<T>(XElement x)
        {
            var tbc = new TypedTreeBinaryConverter<T>(bs, xs) as ITreeBinaryConverter;
            return tbc.TreeToBinary(x);
        }
        public XElement BinaryToTree<T>(Byte[] b)
        {
            var tbc = new TypedTreeBinaryConverter<T>(bs, xs) as ITreeBinaryConverter;
            return tbc.BinaryToTree(b);
        }

        private class StringTranslator : IProjectorToProjectorDomainTranslator<String, Byte[]>, IProjectorToProjectorRangeTranslator<String, Byte[]>
        {
            public Func<String, R> TranslateProjectorToProjectorDomain<R>(Func<Byte[], R> Projector)
            {
                return s => Projector(TextEncoding.UTF16.GetBytes(s));
            }

            public Func<D, String> TranslateProjectorToProjectorRange<D>(Func<D, Byte[]> Projector)
            {
                return k => TextEncoding.UTF16.GetString(Projector(k));
            }
        }

        private interface ITreeBinaryConverter
        {
            Byte[] TreeToBinary(XElement x);
            XElement BinaryToTree(Byte[] b);
        }

        private class TypedTreeBinaryConverter<T> : ITreeBinaryConverter
        {
            private BinarySerializer bs;
            private XmlSerializer xs;
            public TypedTreeBinaryConverter(BinarySerializer bs, XmlSerializer xs)
            {
                this.bs = bs;
                this.xs = xs;
            }

            public Byte[] TreeToBinary(XElement x)
            {
                var v = xs.Read<T>(x);
                using (var s = Streams.CreateMemoryStream())
                {
                    bs.Write(v, s);
                    s.Position = 0;
                    return s.Read((int)(s.Length));
                }
            }

            public XElement BinaryToTree(Byte[] b)
            {
                using (var s = Streams.CreateMemoryStream())
                {
                    s.Write(b);
                    s.Position = 0;
                    var v = bs.Read<T>(s);
                    return xs.Write(v);
                }
            }
        }
    }
}