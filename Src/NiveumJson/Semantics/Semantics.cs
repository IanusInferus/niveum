//==========================================================================
//
//  File:        Semantics.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 语义结构
//  Version:     2019.08.02.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using Niveum.Json.Syntax;
using Niveum.Json.Semantics;

namespace Niveum.Json
{
    public enum Formatting
    {
        None,
        Indented
    }

    public enum JTokenType
    {
        Null,
        Boolean,
        Number,
        String,
        Object,
        Array
    }

    public class JToken
    {
        public JTokenType Type { get; protected set; }

        public String ToString(Formatting Formatting)
        {
            return Formatter.ToString(this, Formatting);
        }

        public static JToken Parse(String Text, bool Diagnostic = false)
        {
            SyntaxValue t;
            {
                var TextRanges = Diagnostic ? new Dictionary<Object, TextRange>() : null;
                using (var sr = new System.IO.StringReader(Text))
                using (var ptr = new PositionedTextReader(Optional<String>.Empty, sr))
                {
                    t = SyntaxParser.ReadValue(ptr, TextRanges);
                    if (!ptr.EndOfText) { throw new InvalidOperationException(); }
                }
                TextRanges = null;
            }
            return Translator.Translate(t);
        }

        public JToken this[String propertyName]
        {
            get
            {
                var o = this as JObject;
                if (o == null) { throw new InvalidOperationException(); }
                return o[propertyName];
            }
            set
            {
                var o = this as JObject;
                if (o == null) { throw new InvalidOperationException(); }
                o[propertyName] = value;
            }
        }
        public JToken this[int index]
        {
            get
            {
                var a = this as JArray;
                if (a == null) { throw new InvalidOperationException(); }
                return a[index];
            }

            set
            {
                var a = this as JArray;
                if (a == null) { throw new InvalidOperationException(); }
                a[index] = value;
            }
        }

        public T Value<T>(String propertyName)
        {
            var o = this as JObject;
            if (o == null) { throw new InvalidOperationException(); }
            var p = o[propertyName] as JValue;
            if (p == null) { throw new InvalidOperationException(); }
            return (T)Convert.ChangeType(p.Value, typeof(T));
        }
        public T Value<T>(int index)
        {
            var a = this as JArray;
            if (a == null) { throw new InvalidOperationException(); }
            var e = a[index] as JValue;
            if (e == null) { throw new InvalidOperationException(); }
            return (T)Convert.ChangeType(e.Value, typeof(T));
        }
    }

    public sealed class JValue : JToken
    {
        public Object Value { get; private set; }

        public JValue()
        {
            this.Type = JTokenType.Null;
            this.Value = null;
        }
        public JValue(bool value)
        {
            this.Type = JTokenType.Boolean;
            this.Value = value;
        }
        public JValue(double value)
        {
            this.Type = JTokenType.Number;
            this.Value = value;
        }
        public JValue(String value)
        {
            this.Type = JTokenType.String;
            this.Value = value;
        }
    }

    public sealed class JObject : JToken, IDictionary<String, JToken>
    {
        private IDictionary<String, JToken> Dict;
        public JObject()
        {
            this.Type = JTokenType.Object;
            Dict = new Dictionary<String, JToken>();
        }
        public JObject(IDictionary<String, JToken> Dict)
        {
            this.Type = JTokenType.Object;
            this.Dict = Dict;
        }

        public new JToken this[String propertyName]
        {
            get
            {
                return Dict[propertyName];
            }
            set
            {
                if (Dict.ContainsKey(propertyName))
                {
                    Dict[propertyName] = value;
                }
                else
                {
                    Dict.Add(propertyName, value);
                }
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                return Dict.Keys;
            }
        }

        public ICollection<JToken> Values
        {
            get
            {
                return Dict.Values;
            }
        }

        public int Count
        {
            get
            {
                return Dict.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return Dict.IsReadOnly;
            }
        }

        public void Add(string key, JToken value)
        {
            Dict.Add(key, value);
        }

        public void Add(KeyValuePair<string, JToken> item)
        {
            Dict.Add(item);
        }

        public void Clear()
        {
            Dict.Clear();
        }

        public bool Contains(KeyValuePair<string, JToken> item)
        {
            return Dict.Contains(item);
        }

        public bool ContainsKey(string key)
        {
            return Dict.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, JToken>[] array, int arrayIndex)
        {
            Dict.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator()
        {
            return Dict.GetEnumerator();
        }

        public bool Remove(string key)
        {
            return Dict.Remove(key);
        }

        public bool Remove(KeyValuePair<string, JToken> item)
        {
            return Dict.Remove(item);
        }

        public bool TryGetValue(string key, out JToken value)
        {
            return Dict.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Dict.GetEnumerator();
        }
    }

    public sealed class JArray : JToken, IList<JToken>
    {
        private IList<JToken> Values;

        public JArray()
        {
            this.Type = JTokenType.Array;
            this.Values = new List<JToken>();
        }
        public JArray(IList<JToken> Values)
        {
            this.Type = JTokenType.Array;
            this.Values = Values;
        }

        public IEnumerable<JToken> Children()
        {
            return Values;
        }

        public int Count
        {
            get
            {
                return Values.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public new JToken this[int index]
        {
            get
            {
                return Values[index];
            }

            set
            {
                Values[index] = value;
            }
        }

        public void Add(JToken content)
        {
            Values.Add(content);
        }

        public int IndexOf(JToken item)
        {
            return Values.IndexOf(item);
        }

        public void Insert(int index, JToken item)
        {
            Values.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Values.RemoveAt(index);
        }

        public void Clear()
        {
            Values.Clear();
        }

        public bool Contains(JToken item)
        {
            return Values.Contains(item);
        }

        public bool Remove(JToken item)
        {
            return Values.Remove(item);
        }

        public IEnumerator<JToken> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public void CopyTo(JToken[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }
    }
}
