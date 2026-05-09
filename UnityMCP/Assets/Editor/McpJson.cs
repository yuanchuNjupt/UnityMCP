using System;
using System.Collections.Generic;
using System.Text;

// ============================================================
// 轻量 JSON 解析器 / 构建器 — 零依赖
// 支持类型: object, array, string, number, bool, null
// ============================================================
internal static class McpJson
{
    public static object Parse(string json)
    {
        int i = 0;
        SkipWhitespace(json, ref i);
        object result = ParseValue(json, ref i);
        SkipWhitespace(json, ref i);
        if (i != json.Length)
            throw new Exception($"Unexpected trailing content at position {i}");
        return result;
    }

    public static string Stringify(object value)
    {
        var sb = new StringBuilder();
        WriteValue(sb, value);
        return sb.ToString();
    }

    // ---- 解析辅助方法 ----

    static object ParseValue(string json, ref int i)
    {
        SkipWhitespace(json, ref i);
        if (i >= json.Length) throw new Exception("Unexpected end of JSON");
        char c = json[i];

        if (c == '"') return ParseString(json, ref i);
        if (c == '{') return ParseObject(json, ref i);
        if (c == '[') return ParseArray(json, ref i);
        if (c == 't' || c == 'f') return ParseBool(json, ref i);
        if (c == 'n') return ParseNull(ref i);
        return ParseNumber(json, ref i);
    }

    static Dictionary<string, object> ParseObject(string json, ref int i)
    {
        var dict = new Dictionary<string, object>();
        i++; // 跳过 '{'
        SkipWhitespace(json, ref i);

        if (json[i] == '}')
        {
            i++;
            return dict;
        }

        while (true)
        {
            SkipWhitespace(json, ref i);
            if (json[i] != '"') throw new Exception($"Expected string key at position {i}");
            string key = ParseString(json, ref i);
            SkipWhitespace(json, ref i);
            if (json[i] != ':') throw new Exception($"Expected ':' at position {i}");
            i++; // 跳过 ':'
            object val = ParseValue(json, ref i);
            dict[key] = val;
            SkipWhitespace(json, ref i);
            if (json[i] == '}')
            {
                i++;
                return dict;
            }
            if (json[i] != ',') throw new Exception($"Expected ',' or '}}' at position {i}");
            i++; // 跳过 ','
        }
    }

    static List<object> ParseArray(string json, ref int i)
    {
        var list = new List<object>();
        i++; // 跳过 '['
        SkipWhitespace(json, ref i);

        if (json[i] == ']')
        {
            i++;
            return list;
        }

        while (true)
        {
            list.Add(ParseValue(json, ref i));
            SkipWhitespace(json, ref i);
            if (json[i] == ']')
            {
                i++;
                return list;
            }
            if (json[i] != ',') throw new Exception($"Expected ',' or ']' at position {i}");
            i++;
        }
    }

    static string ParseString(string json, ref int i)
    {
        i++; // 跳过起始引号
        var sb = new StringBuilder();
        int start = i;

        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                sb.Append(json, start, i - start);
                i++;
                return sb.ToString();
            }
            if (c == '\\')
            {
                sb.Append(json, start, i - start);
                i++;
                if (i >= json.Length) throw new Exception("Unexpected end in string escape");
                switch (json[i])
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(json[i]); break;
                }
                i++;
                start = i;
            }
            else
            {
                i++;
            }
        }
        throw new Exception("Unterminated string");
    }

    static double ParseNumber(string json, ref int i)
    {
        int start = i;
        if (json[i] == '-') i++;
        while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' ||
               json[i] == 'e' || json[i] == 'E' || json[i] == '+' || json[i] == '-'))
        {
            // 允许 e/E 后的 -/+
            if ((json[i] == '+' || json[i] == '-') && i > start &&
                json[i - 1] != 'e' && json[i - 1] != 'E')
                break;
            i++;
        }
        return double.Parse(json.Substring(start, i - start),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    static bool ParseBool(string json, ref int i)
    {
        if (json[i] == 't') { i += 4; return true; }
        i += 5; return false;
    }

    static object ParseNull(ref int i)
    {
        i += 4;
        return null;
    }

    static void SkipWhitespace(string json, ref int i)
    {
        while (i < json.Length && (json[i] == ' ' || json[i] == '\t' ||
               json[i] == '\n' || json[i] == '\r'))
            i++;
    }

    // ---- 序列化辅助方法 ----

    static void WriteValue(StringBuilder sb, object value)
    {
        if (value == null) { sb.Append("null"); return; }
        if (value is string s) { WriteString(sb, s); return; }
        if (value is double d)
        {
            if (Math.Abs(d - Math.Floor(d)) < 1e-10 && !double.IsInfinity(d))
                sb.Append((long)d);
            else
                sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (value is int i) { sb.Append(i); return; }
        if (value is long l) { sb.Append(l); return; }
        if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
        if (value is Dictionary<string, object> dict) { WriteObject(sb, dict); return; }
        if (value is List<object> list) { WriteArray(sb, list); return; }

        // 回退: 当作 double 处理 (来自解析器)
        sb.Append(Convert.ToDouble(value).ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }

    static void WriteObject(StringBuilder sb, Dictionary<string, object> dict)
    {
        sb.Append('{');
        bool first = true;
        foreach (var kv in dict)
        {
            if (!first) sb.Append(',');
            first = false;
            WriteString(sb, kv.Key);
            sb.Append(':');
            WriteValue(sb, kv.Value);
        }
        sb.Append('}');
    }

    static void WriteArray(StringBuilder sb, List<object> list)
    {
        sb.Append('[');
        for (int j = 0; j < list.Count; j++)
        {
            if (j > 0) sb.Append(',');
            WriteValue(sb, list[j]);
        }
        sb.Append(']');
    }
}
