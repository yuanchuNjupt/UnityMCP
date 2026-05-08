using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// Unity 编辑器窗口 — MCP 服务器控制台
// ============================================================
internal class McpServerWindow : EditorWindow
{
    const string PrefKeyHost = "UnityMCP_Host";
    const string PrefKeyPort = "UnityMCP_Port";
    const string DefaultHost = "localhost";
    const int DefaultPort = 9100;

    string _host;
    int _port;
    readonly List<string> _logLines = new();
    Vector2 _scrollPos;
    bool _scrollToBottom;

    [MenuItem("UnityMCP Server/打开MCP Server窗口")]
    static void Open()
    {
        var window = GetWindow<McpServerWindow>("UnityMCP Server");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    void OnEnable()
    {
        _host = EditorPrefs.GetString(PrefKeyHost, DefaultHost);
        _port = EditorPrefs.GetInt(PrefKeyPort, DefaultPort);
        SimpleMcpServer.OnLog += OnServerLog;

        if (SimpleMcpServer.IsRunning)
            _logLines.Add($"[{Now()}] 窗口已附加到运行中的服务器");
    }

    void OnDisable()
    {
        SimpleMcpServer.OnLog -= OnServerLog;
    }

    void OnDestroy()
    {
        SimpleMcpServer.OnLog -= OnServerLog;
    }

    void OnServerLog(string message)
    {
        _logLines.Add($"[{Now()}] {message}");
        _scrollToBottom = true;

        while (_logLines.Count > 200)
            _logLines.RemoveAt(0);

        EditorApplication.delayCall += Repaint;
    }

    static string Now() => DateTime.Now.ToString("HH:mm:ss");

    void OnGUI()
    {
        bool isRunning = SimpleMcpServer.IsRunning;

        // ---- 标题 ----
        EditorGUILayout.LabelField("UnityMCP 服务器控制台", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // ---- 连接设置 ----
        EditorGUILayout.LabelField("连接设置", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(isRunning);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("主机:", GUILayout.Width(40));
        _host = EditorGUILayout.TextField(_host);
        EditorGUILayout.LabelField("端口:", GUILayout.Width(40));
        _port = EditorGUILayout.IntField(_port);
        EditorGUILayout.EndHorizontal();

        if (!isRunning)
        {
            if (_host != EditorPrefs.GetString(PrefKeyHost, DefaultHost))
                EditorPrefs.SetString(PrefKeyHost, _host);
            if (_port != EditorPrefs.GetInt(PrefKeyPort, DefaultPort))
                EditorPrefs.SetInt(PrefKeyPort, _port);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);

        // ---- 启动 / 停止 ----
        EditorGUILayout.BeginHorizontal();
        if (!isRunning)
        {
            if (GUILayout.Button("启动服务器", GUILayout.Height(30)))
                SimpleMcpServer.Start(_host, _port);
        }
        else
        {
            if (GUILayout.Button("停止服务器", GUILayout.Height(30)))
                SimpleMcpServer.Stop();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // ---- 状态 ----
        EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
        var statusStyle = new GUIStyle(EditorStyles.label) { richText = true };
        if (isRunning)
        {
            EditorGUILayout.LabelField(
                $"<color=#00cc00>● 运行中</color> — http://{_host}:{_port}/",
                statusStyle);
        }
        else
        {
            EditorGUILayout.LabelField("<color=#888888>● 已停止</color>", statusStyle);
        }

        EditorGUILayout.Space(5);

        // ---- 日志 ----
        EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
        foreach (var line in _logLines)
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();

        if (_scrollToBottom)
        {
            _scrollPos.y = float.MaxValue;
            _scrollToBottom = false;
        }
    }
}
