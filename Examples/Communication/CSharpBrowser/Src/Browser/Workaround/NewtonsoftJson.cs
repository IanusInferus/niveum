using Bridge.Html5;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Newtonsoft.Json
{
    public enum Formatting
    {
        None,
        Indented
    }
}

namespace Newtonsoft.Json.Linq
{
    public enum JTokenType
    {
        None = 0,
        Object = 1,
        Array = 2,
        Constructor = 3,
        Property = 4,
        Comment = 5,
        Integer = 6,
        Float = 7,
        String = 8,
        Boolean = 9,
        Null = 10,
        Undefined = 11,
        Date = 12,
        Raw = 13,
        Bytes = 14
    }

    public class JToken
    {
        public JTokenType Type { get; protected set; }

        public String ToString(Formatting formatting)
        {
            var j = FromTokens(this);
            if (formatting == Formatting.None)
            {
                return JSON.Stringify(j);
            }
            else
            {
                return JSON.Stringify(j, (Delegate)(null), 4);
            }
        }

        public static JToken Parse(String json)
        {
            var j = JSON.Parse(json);
            return ToTokens(j);
        }

        private static Object FromTokens(JToken j)
        {
            if (j is JValue) { return ((JValue)(j)).Value; }
            if (j is JArray) { return ((JArray)(j)).Children().Select(e => FromTokens(e)).ToArray(); }
            if (j is JObject)
            {
                var o = new Object();
                foreach (var p in (JObject)(j))
                {
                    o[p.Key] = FromTokens(p.Value);
                }
                return o;
            }
            throw new InvalidOperationException();
        }
        private static JToken ToTokens(Object o)
        {
            if (o is Boolean)
            {
                return new JValue((Boolean)(o));
            }
            else if (o is Int64)
            {
                return new JValue((Int64)(o));
            }
            else if (o is Double)
            {
                return new JValue((Double)(o));
            }
            else if (o is String)
            {
                return new JValue((String)(o));
            }
            else if (o is Object[])
            {
                return new JArray(((Object[])(o)).Select(e => ToTokens(e)).ToList());
            }
            else
            {
                return new JObject(o.As<IDictionary<String, Object>>().ToDictionary(p => p.Key, p => ToTokens(p.Value)));
            }
        }
    }

    public class JValue : JToken
    {
        public Object Value { get; private set; }

        public JValue(bool value)
        {
            this.Type = JTokenType.Boolean;
            this.Value = value;
        }
        public JValue(String value)
        {
            this.Type = JTokenType.String;
            this.Value = value;
        }
        public JValue(long value)
        {
            this.Type = JTokenType.Integer;
            this.Value = value;
        }
        public JValue(ulong value)
        {
            this.Type = JTokenType.Integer;
            this.Value = value;
        }
        public JValue(double value)
        {
            this.Type = JTokenType.Float;
            this.Value = value;
        }
    }

    public class JObject : JToken, IDictionary<String, JToken>
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
        }

        JToken IDictionary<string, JToken>.this[string key]
        {
            get
            {
                return Dict[key];
            }
            set
            {
                Dict[key] = value;
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

    public class JArray : JToken, IList<JToken>
    {
        private List<JToken> Values;

        public JArray()
        {
            this.Type = JTokenType.Array;
            this.Values = new List<JToken>();
        }
        public JArray(List<JToken> Values)
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
                return ((IList<JToken>)Values).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public JToken this[int index]
        {
            get
            {
                return ((IList<JToken>)Values)[index];
            }

            set
            {
                ((IList<JToken>)Values)[index] = value;
            }
        }

        public void Add(JToken content)
        {
            Values.Add(content);
        }

        public int IndexOf(JToken item)
        {
            return ((IList<JToken>)Values).IndexOf(item);
        }

        public void Insert(int index, JToken item)
        {
            ((IList<JToken>)Values).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<JToken>)Values).RemoveAt(index);
        }

        public void Clear()
        {
            ((IList<JToken>)Values).Clear();
        }

        public bool Contains(JToken item)
        {
            return ((IList<JToken>)Values).Contains(item);
        }

        public bool Remove(JToken item)
        {
            return ((IList<JToken>)Values).Remove(item);
        }

        public IEnumerator<JToken> GetEnumerator()
        {
            return ((IList<JToken>)Values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<JToken>)Values).GetEnumerator();
        }

        public void CopyTo(JToken[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }
    }
}
