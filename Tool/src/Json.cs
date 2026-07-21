using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PackStudio
{
    /// <summary>
    /// Minimal, dependency-free JSON DOM. Design goals (see PACKTOOL_DESIGN.md):
    ///  - ORDERED objects (key order preserved on round-trip),
    ///  - LOSSLESS values (numbers keep their raw text, so "4" never becomes "4.0"),
    ///  - lossless strings incl. \uXXXX escapes,
    ///  - pretty writer matching the hand-authored pack style (2-space indent,
    ///    one array element per line).
    /// Strict parser: rejects trailing commas / bare words, like JsonUtility.
    /// </summary>
    internal abstract class JNode
    {
        internal abstract void Write(StringBuilder sb, int indent);

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, 0);
            return sb.ToString();
        }

        internal static bool DeepEquals(JNode a, JNode b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null || a.GetType() != b.GetType())
            {
                return false;
            }
            JStr sa = a as JStr;
            if (sa != null)
            {
                return sa.Value == ((JStr)b).Value;
            }
            JNum na = a as JNum;
            if (na != null)
            {
                return na.Raw == ((JNum)b).Raw;
            }
            JBool ba = a as JBool;
            if (ba != null)
            {
                return ba.Value == ((JBool)b).Value;
            }
            if (a is JNull)
            {
                return true;
            }
            JArr aa = a as JArr;
            if (aa != null)
            {
                JArr ab = (JArr)b;
                if (aa.Items.Count != ab.Items.Count)
                {
                    return false;
                }
                for (int i = 0; i < aa.Items.Count; i++)
                {
                    if (!DeepEquals(aa.Items[i], ab.Items[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            JObj oa = (JObj)a;
            JObj ob = (JObj)b;
            if (oa.Keys.Count != ob.Keys.Count)
            {
                return false;
            }
            for (int i = 0; i < oa.Keys.Count; i++)
            {
                // Key ORDER is part of equality: round-trip must preserve it.
                if (oa.Keys[i] != ob.Keys[i] || !DeepEquals(oa[oa.Keys[i]], ob[ob.Keys[i]]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal sealed class JStr : JNode
    {
        internal string Value;

        internal JStr(string value) { Value = value; }

        internal override void Write(StringBuilder sb, int indent)
        {
            WriteEscaped(sb, Value);
        }

        internal static void WriteEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c); // non-ASCII written raw; files are UTF-8
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }

    internal sealed class JNum : JNode
    {
        internal string Raw; // raw numeric text, preserved verbatim

        internal JNum(string raw) { Raw = raw; }

        internal JNum(int value) { Raw = value.ToString(CultureInfo.InvariantCulture); }

        internal double AsDouble()
        {
            double d;
            double.TryParse(Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
            return d;
        }

        internal int AsInt() { return (int)AsDouble(); }

        internal override void Write(StringBuilder sb, int indent) { sb.Append(Raw); }
    }

    internal sealed class JBool : JNode
    {
        internal bool Value;

        internal JBool(bool value) { Value = value; }

        internal override void Write(StringBuilder sb, int indent) { sb.Append(Value ? "true" : "false"); }
    }

    internal sealed class JNull : JNode
    {
        internal override void Write(StringBuilder sb, int indent) { sb.Append("null"); }
    }

    internal sealed class JArr : JNode
    {
        internal readonly List<JNode> Items = new List<JNode>();

        internal override void Write(StringBuilder sb, int indent)
        {
            if (Items.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append("[\n");
            for (int i = 0; i < Items.Count; i++)
            {
                Indent(sb, indent + 1);
                Items[i].Write(sb, indent + 1);
                if (i < Items.Count - 1)
                {
                    sb.Append(',');
                }
                sb.Append('\n');
            }
            Indent(sb, indent);
            sb.Append(']');
        }

        internal static void Indent(StringBuilder sb, int level)
        {
            sb.Append(' ', level * 2);
        }
    }

    internal sealed class JObj : JNode
    {
        private readonly List<string> _keys = new List<string>();
        private readonly Dictionary<string, JNode> _map = new Dictionary<string, JNode>(StringComparer.Ordinal);

        internal IList<string> Keys { get { return _keys; } }

        internal JNode this[string key]
        {
            get
            {
                JNode value;
                return _map.TryGetValue(key, out value) ? value : null;
            }
            set
            {
                if (!_map.ContainsKey(key))
                {
                    _keys.Add(key);
                }
                _map[key] = value;
            }
        }

        internal bool ContainsKey(string key) { return _map.ContainsKey(key); }

        internal void Remove(string key)
        {
            if (_map.Remove(key))
            {
                _keys.Remove(key);
            }
        }

        internal override void Write(StringBuilder sb, int indent)
        {
            if (_keys.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append("{\n");
            for (int i = 0; i < _keys.Count; i++)
            {
                JArr.Indent(sb, indent + 1);
                JStr.WriteEscaped(sb, _keys[i]);
                sb.Append(": ");
                _map[_keys[i]].Write(sb, indent + 1);
                if (i < _keys.Count - 1)
                {
                    sb.Append(',');
                }
                sb.Append('\n');
            }
            JArr.Indent(sb, indent);
            sb.Append('}');
        }
    }

    internal static class Json
    {
        internal static JNode Parse(string text)
        {
            int pos = 0;
            JNode node = ParseValue(text, ref pos);
            SkipWs(text, ref pos);
            if (pos != text.Length)
            {
                throw new FormatException("Unexpected trailing content at offset " + pos);
            }
            return node;
        }

        internal static string Write(JNode node)
        {
            StringBuilder sb = new StringBuilder();
            node.Write(sb, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
            {
                i++;
            }
        }

        private static JNode ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
            {
                throw new FormatException("Unexpected end of JSON");
            }
            char c = s[i];
            if (c == '{')
            {
                return ParseObject(s, ref i);
            }
            if (c == '[')
            {
                return ParseArray(s, ref i);
            }
            if (c == '"')
            {
                return new JStr(ParseString(s, ref i));
            }
            if (c == 't' && Matches(s, i, "true"))
            {
                i += 4;
                return new JBool(true);
            }
            if (c == 'f' && Matches(s, i, "false"))
            {
                i += 5;
                return new JBool(false);
            }
            if (c == 'n' && Matches(s, i, "null"))
            {
                i += 4;
                return new JNull();
            }
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int start = i;
                while (i < s.Length && (s[i] == '-' || s[i] == '+' || s[i] == '.'
                    || s[i] == 'e' || s[i] == 'E' || (s[i] >= '0' && s[i] <= '9')))
                {
                    i++;
                }
                string raw = s.Substring(start, i - start);
                double probe;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out probe))
                {
                    throw new FormatException("Invalid number '" + raw + "' at offset " + start);
                }
                return new JNum(raw);
            }
            throw new FormatException("Unexpected character '" + c + "' at offset " + i);
        }

        private static bool Matches(string s, int i, string word)
        {
            return i + word.Length <= s.Length && string.CompareOrdinal(s, i, word, 0, word.Length) == 0;
        }

        private static JObj ParseObject(string s, ref int i)
        {
            JObj obj = new JObj();
            i++; // '{'
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return obj;
            }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"')
                {
                    throw new FormatException("Expected object key at offset " + i);
                }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException("Expected ':' at offset " + i);
                }
                i++;
                obj[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length)
                {
                    throw new FormatException("Unterminated object");
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] == '}')
                {
                    i++;
                    return obj;
                }
                throw new FormatException("Expected ',' or '}' at offset " + i);
            }
        }

        private static JArr ParseArray(string s, ref int i)
        {
            JArr arr = new JArr();
            i++; // '['
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return arr;
            }
            while (true)
            {
                arr.Items.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length)
                {
                    throw new FormatException("Unterminated array");
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] == ']')
                {
                    i++;
                    return arr;
                }
                throw new FormatException("Expected ',' or ']' at offset " + i);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            i++; // opening quote
            while (true)
            {
                if (i >= s.Length)
                {
                    throw new FormatException("Unterminated string");
                }
                char c = s[i];
                if (c == '"')
                {
                    i++;
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length)
                    {
                        throw new FormatException("Unterminated escape");
                    }
                    char e = s[i];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 >= s.Length)
                            {
                                throw new FormatException("Bad \\u escape");
                            }
                            sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default:
                            throw new FormatException("Bad escape '\\" + e + "' at offset " + i);
                    }
                    i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
        }
    }
}
