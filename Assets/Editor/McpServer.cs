using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

// ============================================================
// MCP 服务器 — 由编辑器窗口手动控制启停
// ============================================================
internal static class SimpleMcpServer
{
    const string ServerName = "UnityMCP";
    const string ServerVersion = "1.0.0";
    const string ProtocolVersion = "2024-11-05";

    static HttpListener _listener;
    static volatile bool _running;

    public static bool IsRunning => _running;
    public static event Action<string> OnLog;

    static void Log(string message)
    {
        Debug.Log($"[UnityMCP] {message}");
        OnLog?.Invoke(message);
    }

    // ---- 公共 API ----

    public static void Start(string host, int port)
    {
        if (_running)
        {
            Log("服务器已在运行中");
            return;
        }
        McpToolRegistry.RegisterAll();
        StartServer(host, port);
    }

    public static void Stop()
    {
        if (!_running) return;
        _running = false;
        try { _listener?.Close(); } catch { }
        Log("服务器已停止");
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

        if (responseJson != null)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
            try
            {
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = buffer.Length;
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                ctx.Response.OutputStream.Close();
            }
            catch { /* 响应流可能已关闭 */ }
        }
        else
        {
            ctx.Response.StatusCode = 204; // No Content (notification)
            try { ctx.Response.OutputStream.Close(); } catch { }
        }

        BeginGetContext(); // 继续监听
    }

    // ---- 请求路由 ----

    static string ProcessRequest(HttpListenerContext ctx)
    {
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
        {
            body = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(body))
            return BuildErrorResponse(null, -32600, "Invalid Request: empty body");

        if (McpJson.Parse(body) is not Dictionary<string, object> request)
            return BuildErrorResponse(null, -32700, "Parse error");

        object idObj;
        request.TryGetValue("id", out idObj);
        if (idObj is double d) idObj = (long)d; // 标准化为 long

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
                    Log("收到 tools/call 请求");
                    return BuildResponse(idObj, HandleToolsCall(request));
                case "notifications/initialized":
                    Log("收到 notifications/initialized");
                    return null; // JSON-RPC notification，无需回复
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
        foreach (var tool in McpToolRegistry.All)
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

        var tool = McpToolRegistry.Find(toolName);
        if (tool == null)
            throw new Exception($"Unknown tool: {toolName}");

        Log($"执行工具: {toolName}");
        string resultText = McpMainThread.Invoke(() => tool.Execute(args));
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
