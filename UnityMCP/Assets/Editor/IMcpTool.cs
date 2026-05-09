using System.Collections.Generic;

// ============================================================
// 工具接口 — 每个工具实现此接口即可被 MCP 服务器发现和调用
// Execute 在 Unity 主线程上被调用，实现者无需关心线程调度
// ============================================================
internal interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    Dictionary<string, object> InputSchema { get; }
    string Execute(Dictionary<string, object> args);
}
