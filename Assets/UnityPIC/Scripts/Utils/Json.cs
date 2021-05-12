using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPIC
{
    namespace Utils
    {
        public class JsonDouble : object
        {
            public JsonDouble(double d)
            {
                _value = d;
            }

            public static implicit operator double(JsonDouble d) => d._value;
            public static implicit operator JsonDouble(double d) => new JsonDouble(d);

            public override string ToString()
            {
                return _value.ToString();
            }

            double _value;
        }

        public class JsonLong
        {
            public JsonLong(long d)
            {
                _value = d;
            }

            public static implicit operator long(JsonLong d) => d._value;
            public static implicit operator JsonLong(long d) => new JsonLong(d);

            public override string ToString()
            {
                return _value.ToString();
            }

            long _value;
        }

        public class JsonList : List<Json>
        {
            public override string ToString()
            {
                return ToStringPrefixed("");
            }

            public string ToStringPrefixed(string prefix)
            {
                string s = "[\n";

                string newPrefix = prefix + "  ";

                for (int i = 0; i < Count; ++i)
                {
                    s += newPrefix;
                    s += this[i].ToStringPrefixed(newPrefix);

                    if (i < Count - 1)
                        s += ",";

                    s += "\n";
                }

                s += prefix + "]";
                return s;
            }
        }

        public class JsonDict : Dictionary<string, Json> {
            public override string ToString()
            {
                return ToStringPrefixed("");
            }

            public string ToStringPrefixed(string prefix)
            {
                string s = "{\n";

                string newPrefix = prefix + "  ";

                int i = 0, n = Keys.Count;
                foreach (string k in Keys)
                {
                    s += newPrefix + "\"" + k + "\": " + this[k].ToStringPrefixed(newPrefix);

                    if (i < n - 1)
                        s += ",";

                    s += "\n";

                    ++i;
                }

                s += prefix + "}";
                return s;
            }
        }

        public class Json
        {
            object _internal;

            public enum DataType
            {
                List,
                Dict,
                String,
                Long,
                Double,
            }

            public DataType Type { get; set; }

            public T Get<T>()
            {
                return (T)_internal;
            }

            public override string ToString()
            {
                return ToStringPrefixed("");
            }

            public string ToStringPrefixed(string prefix)
            {
                switch (Type)
                {
                    case DataType.Long:
                        return Get<JsonLong>().ToString();
                    case DataType.Double:
                        return Get<JsonDouble>().ToString();
                    case DataType.String:
                        return "\"" + Get<string>() + "\"";
                    case DataType.List:
                        return Get<JsonList>().ToStringPrefixed(prefix);
                    case DataType.Dict:
                        return Get<JsonDict>().ToStringPrefixed(prefix);
                    default:
                        return "";
                }
            }

            public static Json Parse(string jsonStr)
            {
                int i = 0;

                try
                {
                    return load(jsonStr, ref i);
                }
                catch (Exception)
                {
                    throw new Exception("Invalid json.");
                }
            }

            private static Json load(string jsonStr, ref int i)
            {
                Json json = new Json();

                while (i < jsonStr.Length && jsonStr[i] != '{' && jsonStr[i] != '[' && !char.IsDigit(jsonStr[i]) && jsonStr[i] != '\"' && jsonStr[i] != '-' && jsonStr[i] != '.') ++i;

                if (i == jsonStr.Length)
                {
                    return null;
                }
                else if (jsonStr[i] == '{')
                {
                    json._internal = loadDict(jsonStr, ref i);
                    json.Type = DataType.Dict;
                }
                else if (jsonStr[i] == '[')
                {
                    json._internal = loadList(jsonStr, ref i);
                    json.Type = DataType.List;
                }
                else if (jsonStr[i] == '\"')
                {
                    json._internal = loadString(jsonStr, ref i);
                    json.Type = DataType.String;
                }
                else if (char.IsDigit(jsonStr[i]) || jsonStr[i] == '-' || jsonStr[i] == '.')
                {
                    bool hasDot = jsonStr[i] == '.';

                    int start = i;

                    while (++i < jsonStr.Length)
                    {
                        if (jsonStr[i] == '.')
                        {
                            if (hasDot)
                            {
                                throw new Exception("Invalid decimal value.");
                            }
                            else
                            {
                                hasDot = true;
                            }
                        }
                        else if (!char.IsDigit(jsonStr[i]))
                        {
                            break;
                        }
                    }

                    if (hasDot)
                    {
                        json._internal = new JsonDouble(double.Parse(jsonStr.Substring(start, i - start)));
                        json.Type = DataType.Double;
                    }
                    else
                    {
                        json._internal = new JsonLong(long.Parse(jsonStr.Substring(start, i - start)));
                        json.Type = DataType.Long;
                    }
                }
                else
                {
                    throw new Exception("Invalid json");
                }

                return json;
            }

            private static JsonDict loadDict(string jsonStr, ref int i)
            {
                JsonDict dict = new JsonDict();

                string key;
                Json val;

                ++i;

                while (true)
                {
                    while (jsonStr[i] != '\"' && jsonStr[i] != '}') ++i;

                    if (jsonStr[i] == '}')
                    {
                        break;
                    }

                    key = loadString(jsonStr, ref i);

                    while (jsonStr[i++] != ':') ;

                    val = load(jsonStr, ref i);

                    dict[key] = val;
                }

                ++i;

                return dict;
            }

            private static JsonList loadList(string jsonStr, ref int i)
            {
                JsonList list = new JsonList();

                ++i;

                while (true)
                {
                    while (char.IsWhiteSpace(jsonStr[i]) || jsonStr[i] == ',') ++i;

                    if (jsonStr[i] == ']')
                        break;

                    list.Add(load(jsonStr, ref i));
                }

                ++i;

                return list;
            }

            private static string loadString(string jsonStr, ref int i)
            {
                ++i;

                int start = i;

                while (jsonStr[i] != '\"') ++i;

                string str = jsonStr.Substring(start, i - start);

                ++i;

                return str;
            }
        }
    }
}
