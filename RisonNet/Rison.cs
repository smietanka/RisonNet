using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RisonNet
{
    public class Rison
    {
        public Rison()
        {
            Table = GetTable();
            EncodeFunctions = GetEncodeFunctions();
        }
        private string currentString;
        private int index = 0;
        private string next_id_rgx = @"[^-0123456789 '!:(),*@$][^ '!:(),*@$]*";

        private Dictionary<Type, Func<object, string>> EncodeFunctions;
        private Dictionary<char, Func<object>> Table;
        private Dictionary<char, Func<Rison, object>> Bands = new Dictionary<char, Func<Rison, object>>()
        {
            { 't', (x) => { return true; } },
            { 'f', (x) => { return false; } },
            { 'n', (x) => { return null; } },
            { '(', (inst) => {
                var ar = new List<dynamic>();
                char c = '\0';
                while((c = inst.Next()) != ')')
                {
                    if(c == '\0') throw new Exception("unmatched !");
                    if(ar.Count > 0)
                    {
                        if(c != ',') throw new Exception("missin ,");
                    } else if (c == ',')
                        throw new Exception("extra ,");
                    else
                        --inst.index;
                    var n = inst.ReadValue();
                    if(n != null)
                    {
                        ar.Add(n);
                    }
                }
                return ar;
            } }
        };

        private Dictionary<char, Func<object>> GetTable()
        {
            return new Dictionary<char, Func<object>>()
            {
                { '!', ExclamationMark },
                { '(', Bracket },
                { '\'', Apostrophe },
                { '-', Dash }
            };
        }

        private Dictionary<Type, Func<object, string>> GetEncodeFunctions()
        {
            return new Dictionary<Type, Func<object, string>>()
            {
                { typeof(Array), ArrayFunc },
                { typeof(Boolean), BooleanFunc },
                { typeof(Nullable), NullableFunc },
                { typeof(Number), NumberFunc },
                { typeof(object), ObjectFunc },
                { typeof(string), StringFunc }
            };
        }
        private class Number { }
        private string ArrayFunc(object x)
        {
            var genericType = x.GetType().GetGenericArguments()[0];
            Type genericListType = typeof(List<>).MakeGenericType(genericType);
            IList res = (IList)Activator.CreateInstance(genericListType);
            foreach (var te in x as IEnumerable)
            {
                res.Add(te);
            }

            var l = res.Count;
            var a = new List<string>() { "!(" };
            bool b = false;
            string v;
            for (var i = 0; i < l; i++)
            {
                v = Enc(res[i]);
                if (v is string)
                {
                    if (b)
                    {
                        a.Add(",");
                    }
                    a.Add(v);
                    b = true;
                }
            }
            a.Add(")");
            return string.Join("", a);
        }
        private bool IsArrayOrCollection(Type type)
        {
            if (type == typeof(string)) return false;
            var a = type.GetInterface(nameof(IEnumerable)) != null && type.IsGenericType;
            var b = type.GetInterface(nameof(ICollection)) != null;
            return (a) ||
                (b) ||
                type.IsArray;
        }
        private string BooleanFunc(object x)
        {
            if (x is bool)
            {
                if ((bool)x)
                {
                    return "!t";
                }
                return "!f";
            }
            return "!f";
        }
        private string NullableFunc(object x)
        {
            return "!n";
        }
        private string NumberFunc(object x)
        {
            if (!IsNumber(x))
            {
                return "!n";
            }
            return Regex.Replace(x.ToString(), @"\+", "");
        }
        private static bool IsNumber<T>(T obj)
        {
            return obj is int || obj is long || obj is double ||
                   obj is decimal || obj is float ||
                   obj is byte || obj is short ||
                   obj is sbyte || obj is ushort ||
                   obj is uint || obj is ulong;
        }
        private string ObjectFunc(object x)
        {
            if (x != null)
            {
                var a = new List<string>() { "(" };
                bool b = false;
                IDictionary<string, object> ks = new Dictionary<string, object>();
                string v;
                if (x.GetType() == typeof(ExpandoObject))
                {
                    ks = (ExpandoObject)x;
                }
                else
                {
                    foreach (var prop in x.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                    {
                        var propVal = prop.GetValue(x);
                        if (propVal != null)
                        {
                            ks.Add(prop.Name, propVal);
                        }
                    }
                }
                foreach (var ki in ks)
                {
                    v = Enc(ki.Value);
                    if (v.GetType() == typeof(string))
                    {
                        if (b)
                        {
                            a.Add(",");
                        }
                        a.Add($"{ki.Key}:{v}");
                        b = true;
                    }
                }
                a.Add(")");
                return string.Join("", a);
            }
            return "!n";
        }
        private string StringFunc(object x)
        {
            var rgx = new Regex(@"^[^-0123456789] '!:(),*@$][^ '!:(),*@$]*");
            if (x.ToString() == "")
                return "''";

            if (rgx.Match(x.ToString()).Success)
            {
                return x.ToString();
            }
            return $@"'{x.ToString()}'";
        }

        private object ExclamationMark()
        {
            var result = new object();
            var s = currentString;
            var c = s[index++];
            if (c == '\0') throw new Exception("asdasd");
            if (Bands.TryGetValue(c, out Func<Rison, object> func))
            {
                if (c == '(')
                    result = func.Invoke(this);
                else
                    result = func.Invoke(null);
            }
            else
            {
                throw new Exception("unkown literal: !" + c);
            }
            return result;
        }
        private object Bracket()
        {
            var o = new Dictionary<string, object>();
            char c;

            int count = 0;
            while ((c = Next()) != ')')
            {
                if (count > 0)
                {
                    if (c != ',') throw new Exception("missing ,");
                }
                else if (c == ',')
                    return new Exception("extra ,");
                else
                    --index;
                var k = ReadValue();
                if (k == null) return null;
                if (Next() != ':') throw new Exception("missing :");
                var v = ReadValue();
                o.Add(k.ToString(), v);
                count++;
            }
            ExpandoObject obj = o.Expando();
            return obj;
        }

        private object Apostrophe()
        {
            var s = currentString;
            var i = index;
            var start = i;
            var segments = new List<string>();
            char c = '\0';
            while ((c = s[i++]) != '\'')
            {
                if (c == '\0') throw new Exception("unmatched '");
                if (c == '!')
                {
                    if (start < i - 1)
                        segments.Add(s.Slice(start, i - 1));
                    c = s[i++];
                    if ("!'".Contains(c.ToString()))
                    {
                        segments.Add(c.ToString());
                    }
                    else
                    {
                        throw new Exception("invalid string escape: !");
                    }
                    start = i;
                }
            }
            if (start < i - 1)
                segments.Add(s.Slice(start, i - 1));
            index = i;
            return segments.Count == 1 ? segments[0] : string.Join("", segments);
        }
        private object Dash()
        {
            var s = currentString;
            var i = index;
            var start = i - 1;
            var permittedSigns = "-";
            var state = true;
            do
            {
                var c = s[i++];
                if (c == '\0') break;
                if (char.IsDigit(c))
                    continue;
                if (permittedSigns.Contains(c.ToString()))
                {
                    permittedSigns = "";
                    continue;
                }
                state = false;
            } while (state);
            index = --i;
            s = s.Slice(start, i);
            if (s.Equals("-")) throw new Exception("invalid number");
            if (s.Contains("e") || s.Contains("."))
            {
                var cultureInfo = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                cultureInfo.NumberFormat.CurrencyDecimalSeparator = ".";
                return double.Parse(s, NumberStyles.Any, cultureInfo);
            }
            return int.Parse(s);
        }

        /// <summary>
        /// Zdekoduje UrlHash do postaci JSON
        /// </summary>
        /// <param name="objectToDecode"></param>
        /// <returns>Obiekt JSON danego obiektu</returns>
        public string Decode(string objectToDecode)
        {
            return JsonConvert.SerializeObject(Parse(objectToDecode));
        }

        /// <summary>
        /// Przyklad w testach jednostkowych dla Risona. Wygeneruje dynamiczny obiekt na podstawie url hasha
        /// </summary>
        /// <param name="objectToDecode"></param>
        /// <returns></returns>
        public dynamic DecodeToObject(string objectToDecode)
        {
            var value = Parse(objectToDecode);
            return value;
        }
        /// <summary>
        /// Metoda zamieniajaca obiekt C# (dynamiczny rowniez) na hasha w postaci Risona
        /// Mozna uzywac ExpandoObject (nawet nalezy) w przypadku uzywania dynamicznych klas
        /// Rowniez przyklad w testach jednostokwych PublicApi_Rison_Test
        /// </summary>
        /// <param name="objectToEncode"></param>
        /// <returns></returns>
        public string Encode(object objectToEncode)
        {
            return Enc(objectToEncode);
        }
        private bool IsKeyValuePair(object o)
        {
            Type type = o.GetType();
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition() != null ? type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) : false;
            }
            return false;
        }
        private string Enc(object obj)
        {
            if (obj is null) return "!n";
            //is array
            Type typeOfObj = obj.GetType();
            if (IsArrayOrCollection(typeOfObj))
                return EncodeFunctions[typeof(Array)].Invoke(obj);
            else
            {
                //sprawdz czy to liczba
                if (IsNumber(obj))
                    return EncodeFunctions[typeof(Number)].Invoke(obj);
                else
                {
                    // check if obj is a string
                    if (typeOfObj == typeof(string) || typeOfObj.BaseType == typeof(Enum))
                    {
                        return EncodeFunctions[typeof(string)].Invoke(obj);
                    }
                    //and then check basetype for object type
                    if (typeOfObj.BaseType == typeof(object) || (IsKeyValuePair(obj)))
                        return EncodeFunctions[typeof(object)].Invoke(obj);
                    else
                    {
                        var encodeFunction = EncodeFunctions[typeOfObj];
                        if (encodeFunction != null)
                            return encodeFunction.Invoke(obj);
                    }

                }
            }
            return string.Empty;
        }
        private object Parse(string obj)
        {
            currentString = obj;
            index = 0;
            var value = ReadValue();
            return value;
        }

        private object ReadValue()
        {
            var nextChar = Next();
            var copiedNextChar = nextChar;
            if (char.IsDigit(nextChar))
            {
                copiedNextChar = '-';
            }
            if (Table.TryGetValue(copiedNextChar, out Func<object> fun))
            {
                var obj = fun.Invoke();
                return obj;
            }

            var s = currentString;
            var i = index - 1;
            //remove from s from begin to i
            var removed = s.Remove(0, i);
            var matchedRgx = Regex.Match(removed, next_id_rgx);
            if (matchedRgx.Success)
            {
                var id = matchedRgx.Value;
                index = i + id.Length;
                return id;
            }
            if (nextChar != '\0')
            {
                throw new Exception("invalid character " + nextChar);
            }
            return string.Empty;
        }

        private char Next()
        {
            char c = '\0';
            var s = currentString;
            var i = index;
            do
            {
                if (i == s.Length) return '\0';
                c = s[i++];
            } while ("".IndexOf(c) >= 0);
            index = i;
            return c;
        }
    }

    public static class Extensions
    {
        public static string Slice(this string source, int start, int end)
        {
            if (end < 0)
            {
                end = source.Length + end;
            }
            int len = end - start;
            return source.Substring(start, len);
        }

        public static ExpandoObject Expando(this IEnumerable<KeyValuePair<string, object>> dictionary)
        {
            var expando = new ExpandoObject();
            var expandoDic = (IDictionary<string, object>)expando;
            foreach (var item in dictionary)
            {
                expandoDic.Add(item);
            }
            return expando;
        }
    }
}
