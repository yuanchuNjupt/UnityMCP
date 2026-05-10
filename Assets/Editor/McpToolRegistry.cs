using System.Collections.Generic;

// ============================================================
// 工具注册表 — 管理所有已注册的 MCP 工具
// 采用显式注册模型，新增工具只需在这里加一行 Register
// ============================================================
internal static class McpToolRegistry
{
    static readonly List<IMcpTool> _tools = new List<IMcpTool>();
    static bool _registered;

    /// <summary>所有已注册的工具列表（只读）</summary>
    public static IReadOnlyList<IMcpTool> All => _tools;

    /// <summary>注册单个工具（在 RegisterAll 中调用）</summary>
    public static void Register(IMcpTool tool)
    {
        _tools.Add(tool);
    }

    /// <summary>按名称查找工具，未找到返回 null</summary>
    public static IMcpTool Find(string name)
    {
        foreach (var tool in _tools)
        {
            if (tool.Name == name)
                return tool;
        }
        return null;
    }

    /// <summary>
    /// 一次性注册所有内置工具（多次调用安全）
    /// 在此方法中添加新的工具: Register(new MyNewTool());
    /// </summary>
    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        Register(new CreateGameObjectTool());
        Register(new GetSceneGameObjectsTool());
        Register(new GetGameObjectInfoTool());
        Register(new GetComponentPropertiesTool());
        Register(new SetComponentPropertyTool());
        Register(new SetComponentPropertiesTool());
        Register(new AddComponentTool());
        Register(new RemoveComponentTool());
        Register(new SaveSceneTool());
    }
}
