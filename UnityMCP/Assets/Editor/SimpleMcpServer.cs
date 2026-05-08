using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

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

// ============================================================
// 工具定义
// ============================================================
internal class McpToolDefinition
{
    public string Name;
    public string Description;
    public Dictionary<string, object> InputSchema;
    public Func<Dictionary<string, object>, string> Handler;
}

// ============================================================
// MCP 服务器 — 由编辑器窗口手动控制启停
// ============================================================
internal static class SimpleMcpServer
{
    const string ServerName = "UnityMCP";
    const string ServerVersion = "1.0.0";
    const string ProtocolVersion = "2024-11-05";

    static HttpListener _listener;
    static readonly List<McpToolDefinition> Tools = new();
    static volatile bool _running;
    static bool _toolsRegistered;

    public static bool IsRunning => _running;
    public static event Action<string> OnLog;

    static void Log(string message)
    {
        Debug.Log($"[UnityMCP] {message}");
        OnLog?.Invoke(message);
    }

    public static void Start(string host, int port)
    {
        if (_running)
        {
            Log("服务器已在运行中");
            return;
        }
        if (!_toolsRegistered)
        {
            RegisterTools();
            _toolsRegistered = true;
        }
        StartServer(host, port);
    }

    public static void Stop()
    {
        if (!_running) return;
        _running = false;
        try { _listener?.Close(); } catch { }
        Log("服务器已停止");
    }

    // ---- 工具注册 ----

    static void RegisterTools()
    {
        Tools.Add(new McpToolDefinition
        {
            Name = "create_gameobject",
            Description =
                "Create a primitive GameObject in the current scene. " +
                "Supported types: Sphere, Cube, Capsule, Cylinder, Plane, Quad.",
            InputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["primitive_type"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Type of primitive mesh",
                        ["enum"] = new List<object> { "Sphere", "Cube", "Capsule", "Cylinder", "Plane", "Quad" }
                    },
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name for the new GameObject (defaults to the primitive type name)"
                    },
                    ["position"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["description"] = "World position [x, y, z], defaults to (0, 0, 0)",
                        ["items"] = new Dictionary<string, object> { ["type"] = "number" }
                    }
                }
            },
            Handler = HandleCreateGameObject
        });
    }

    // ---- HTTP 服务器生命周期 ----

    static void StartServer(string host, int port)
    {
        string url = $"http://{host}:{port}/";
        _running = true;
        _listener = new HttpListener();
        _listener.Prefixes.Add(url);
        _listener.Start();
        BeginGetContext();
        Log($"服务器已启动: {url}");
    }

    static void BeginGetContext()
    {
        if (!_running) return;
        _listener.BeginGetContext(OnRequestReceived, null);
    }

    static void OnRequestReceived(IAsyncResult ar)
    {
        HttpListenerContext ctx;
        try { ctx = _listener.EndGetContext(ar); }
        catch { return; }

        string responseJson;
        try
        {
            responseJson = ProcessRequest(ctx);
        }
        catch (Exception ex)
        {
            responseJson = BuildErrorResponse(null, -32603, $"Internal error: {ex.Message}");
        }

        byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
        try
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }
        catch { /* 响应流可能已关闭 */ }

        BeginGetContext(); // 继续监听
    }

    // ---- 请求路由 ----

    static string ProcessRequest(HttpListenerContext ctx)
    {
        // 读取请求体
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
        {
            body = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(body))
            return BuildErrorResponse(null, -32600, "Invalid Request: empty body");

        if (McpJson.Parse(body) is not Dictionary<string, object> request)
            return BuildErrorResponse(null, -32700, "Parse error");

        // 提取 id
        object idObj;
        request.TryGetValue("id", out idObj);
        if (idObj is double d) idObj = (long)d; // 标准化为 long

        // 提取 method
        if (!request.TryGetValue("method", out object methodObj) || methodObj is not string method)
            return BuildErrorResponse(idObj, -32600, "Invalid Request: missing method");

        // 路由
        try
        {
            switch (method)
            {
                case "initialize":
                    Log("收到 initialize 请求");
                    return BuildResponse(idObj, HandleInitialize());
                case "tools/list":
                    Log("收到 tools/list 请求");
                    return BuildResponse(idObj, HandleToolsList());
                case "tools/call":
                    Log($"收到 tools/call 请求");
                    return BuildResponse(idObj, HandleToolsCall(request));
                default:
                    Log($"未知方法: {method}");
                    return BuildErrorResponse(idObj, -32601, $"Method not found: {method}");
            }
        }
        catch (Exception ex)
        {
            Log($"工具错误: {ex.Message}");
            return BuildErrorResponse(idObj, -32603, $"Tool error: {ex.Message}");
        }
    }

    // ---- MCP 方法处理器 ----

    static Dictionary<string, object> HandleInitialize()
    {
        // 接受客户端发送的任意协议版本
        return new Dictionary<string, object>
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new Dictionary<string, object>
            {
                ["tools"] = new Dictionary<string, object>()
            },
            ["serverInfo"] = new Dictionary<string, object>
            {
                ["name"] = ServerName,
                ["version"] = ServerVersion
            }
        };
    }

    static Dictionary<string, object> HandleToolsList()
    {
        var toolList = new List<object>();
        foreach (var tool in Tools)
        {
            toolList.Add(new Dictionary<string, object>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema
            });
        }

        return new Dictionary<string, object>
        {
            ["tools"] = toolList
        };
    }

    static Dictionary<string, object> HandleToolsCall(Dictionary<string, object> request)
    {
        if (!request.TryGetValue("params", out object paramsObj) ||
            paramsObj is not Dictionary<string, object> callParams)
            throw new Exception("Missing params");

        if (!callParams.TryGetValue("name", out object nameObj) || nameObj is not string toolName)
            throw new Exception("Missing tool name");

        Dictionary<string, object> args;
        if (callParams.TryGetValue("arguments", out object argsObj) &&
            argsObj is Dictionary<string, object> a)
        {
            args = a;
        }
        else
        {
            args = new Dictionary<string, object>();
        }

        // 查找并执行工具
        foreach (var tool in Tools)
        {
            if (tool.Name == toolName)
            {
                Log($"执行工具: {toolName}");
                string resultText = InvokeOnMainThread(() => tool.Handler(args));
                Log($"完成: {resultText}");
                return new Dictionary<string, object>
                {
                    ["content"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = resultText
                        }
                    }
                };
            }
        }

        throw new Exception($"Unknown tool: {toolName}");
    }

    // ---- 工具: create_gameobject ----

    static string HandleCreateGameObject(Dictionary<string, object> args)
    {
        string primitiveType = "Cube";
        if (args.TryGetValue("primitive_type", out object pt) && pt is string pts)
            primitiveType = pts;

        string goName = primitiveType;
        if (args.TryGetValue("name", out object nm) && nm is string nms && !string.IsNullOrWhiteSpace(nms))
            goName = nms;

        float x = 0f, y = 0f, z = 0f;
        if (args.TryGetValue("position", out object pos) && pos is List<object> posList)
        {
            if (posList.Count > 0) x = Convert.ToSingle(posList[0]);
            if (posList.Count > 1) y = Convert.ToSingle(posList[1]);
            if (posList.Count > 2) z = Convert.ToSingle(posList[2]);
        }

        var ptEnum = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), primitiveType, ignoreCase: true);
        GameObject go = GameObject.CreatePrimitive(ptEnum);
        go.name = goName;
        go.transform.position = new Vector3(x, y, z);

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        return $"Created {primitiveType} '{goName}' at ({x:F2}, {y:F2}, {z:F2})";
    }

    // ---- 线程调度辅助 ----

    static string InvokeOnMainThread(Func<string> action)
    {
        string result = null;
        Exception exception = null;
        var evt = new ManualResetEventSlim(false);

        EditorApplication.delayCall += () =>
        {
            try { result = action(); }
            catch (Exception ex) { exception = ex; }
            finally { evt.Set(); }
        };

        if (!evt.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Unity main thread did not respond within 30 seconds");

        if (exception != null)
            throw exception;

        return result;
    }

    // ---- JSON-RPC 响应构建器 ----

    static string BuildResponse(object id, Dictionary<string, object> result)
    {
        var resp = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
        return McpJson.Stringify(resp);
    }

    static string BuildErrorResponse(object id, int code, string message)
    {
        var resp = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object>
            {
                ["code"] = code,
                ["message"] = message
            }
        };
        return McpJson.Stringify(resp);
    }
}
