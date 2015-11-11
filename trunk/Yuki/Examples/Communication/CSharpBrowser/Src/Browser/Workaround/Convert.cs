using System;
using System.Collections.Generic;
using Bridge.Html5;

namespace System
{
    public static class Convert
    {
        private static String codes = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
        public static String ToBase64String(byte[] inArray)
        {
            var l = new List<Char>();
            for (int i = 0; i < inArray.Length; i += 3)
            {
                var b = inArray[i];
                l.Add(codes[(b >> 2) & 0x3F]);
                if (i + 1 >= inArray.Length)
                {
                    l.Add(codes[(b & 3) << 4]);
                    l.Add('=');
                    l.Add('=');
                    break;
                }
                var b2 = inArray[i + 1];
                l.Add(codes[((b & 3) << 4) | ((b2 >> 4) & 0xF)]);
                if (i + 2 >= inArray.Length)
                {
                    l.Add(codes[(b2 & 0xF) << 2]);
                    l.Add('=');
                    break;
                }
                var b3 = inArray[i + 2];
                l.Add(codes[((b2 & 0xF) << 2) | ((b3 >> 6) & 3)]);
                l.Add(codes[b3 & 0x3F]);
            }

            return new String(l.ToArray());
        }

        public static Boolean ToBoolean(Object value)
        {
            if (value is Boolean) { return (Boolean)(value); }
            if (value is Int64) { return (Int64)(value) != 0; }
            if (value is Double) { return (Double)(value) != 0; }
            if (value is String) { return !((String)(value) == "False"); }
            return !(value == null);
        }
        public static String ToString(Object value)
        {
            if (value is Boolean) { return (Boolean)(value) ? "True" : "False"; }
            return value.ToString();
        }
        public static Byte ToByte(Object value)
        {
            if (value is Byte) { return (Byte)(value); }
            if (value is Int64) { return (Byte)(Int64)(value); }
            if (value is String) { return (Byte)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static UInt16 ToUInt16(Object value)
        {
            if (value is Int64) { return (UInt16)(Int64)(value); }
            if (value is String) { return (UInt16)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static UInt32 ToUInt32(Object value)
        {
            if (value is Int64) { return (UInt32)(Int64)(value); }
            if (value is String) { return (UInt32)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static UInt64 ToUInt64(Object value)
        {
            if (value is Int64) { return (UInt64)(Int64)(value); }
            if (value is String) { return (UInt64)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static SByte ToSByte(Object value)
        {
            if (value is Int64) { return (SByte)(Int64)(value); }
            if (value is String) { return (SByte)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static Int16 ToInt16(Object value)
        {
            if (value is Int64) { return (Int16)(Int64)(value); }
            if (value is String) { return (Int16)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static Int32 ToInt32(Object value)
        {
            if (value is Int64) { return (Int32)(Int64)(value); }
            if (value is String) { return (Int32)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static Int64 ToInt64(Object value)
        {
            if (value is Int64) { return (Int64)(value); }
            if (value is String) { return (Int64)Global.ParseInt((String)(value)); }
            throw new NotImplementedException();
        }
        public static Single ToSingle(Object value)
        {
            if (value is Double) { return (Single)(Double)(value); }
            if (value is String) { return (Single)Global.ParseFloat((String)(value)); }
            throw new NotImplementedException();
        }
        public static Double ToDouble(Object value)
        {
            if (value is Double) { return (Double)(value); }
            if (value is String) { return (Double)Global.ParseFloat((String)(value)); }
            throw new NotImplementedException();
        }
    }
}
