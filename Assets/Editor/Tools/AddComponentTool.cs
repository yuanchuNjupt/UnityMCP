using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: add_component — 向 GameObject 添加组件
// ============================================================
internal class AddComponentTool : IMcpTool
{
    public string Name => "add_component";

    public string Description =>
        "向指定 GameObject 添加一个组件。支持简写类型名（如 'Rigidbody'、'BoxCollider'）" +
        "或完整类型名。返回新组件的 InstanceID。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["gameobject_instance_id"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "目标 GameObject 的 InstanceID"
            },
            ["component_type"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "要添加的组件类型名称，如 'Rigidbody'、'BoxCollider'、'Light' 等"
            }
        },
        ["required"] = new List<object> { "gameobject_instance_id", "component_type" }
    };

    public string Execute(Dictionary<string, object> args)
    {
        int goId = ConvertArg.ToInt(args, "gameobject_instance_id");
        string componentTypeName = ConvertArg.ToString(args, "component_type");

        var go = EditorUtility.InstanceIDToObject(goId) as GameObject;
        if (go == null)
        {
            return Error($"未找到 InstanceID={goId} 的 GameObject");
        }

        // 解析类型
        Type type = McpTypeConverter.ResolveType(componentTypeName);
        if (type == null)
        {
            // 回退：在当前 GameObject 已有组件中模糊搜索
            type = FuzzySearchOnGameObject(go, componentTypeName);
        }

        if (type == null)
        {
            return Error(
                $"无法解析组件类型 '{componentTypeName}'。" +
                "请使用完整类型名（如 'UnityEngine.Rigidbody'）或确认该组件名称正确。");
        }

        if (!typeof(Component).IsAssignableFrom(type))
        {
            return Error($"'{type.FullName}' 不是 Component 类型");
        }

        // Unity 不允许手动添加 Transform
        if (type == typeof(Transform) || type == typeof(RectTransform))
        {
            return Error("不能手动添加 Transform 组件（每个 GameObject 自动拥有）");
        }

        // 检查是否已存在且不允许重复
        if (type.GetCustomAttributes(typeof(DisallowMultipleComponent), true).Length > 0)
        {
            Component existing = go.GetComponent(type);
            if (existing != null)
            {
                return Error($"该 GameObject 已存在 '{componentTypeName}' 组件（该组件不允许重复）");
            }
        }

        Component newComp = Undo.AddComponent(go, type);
        int newCompId = newComp.GetInstanceID();

        Debug.Log($"[UnityMCP] 添加组件: {go.name} ← {newComp.GetType().Name} (ID={newCompId})");

        return McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = new Dictionary<string, object>
            {
                ["gameobject_instance_id"] = goId,
                ["gameobject_name"] = go.name,
                ["component_instance_id"] = newCompId,
                ["type_name"] = newComp.GetType().Name,
                ["full_type_name"] = newComp.GetType().FullName
            }
        });
    }

    static Type FuzzySearchOnGameObject(GameObject go, string typeName)
    {
        string lower = typeName.ToLowerInvariant();
        Component[] comps = go.GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            Type t = c.GetType();
            if (t.Name.ToLowerInvariant().Contains(lower))
                return t;
        }

        // 遍历所有已加载程序集
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (!typeof(Component).IsAssignableFrom(t)) continue;
                if (t.Name.ToLowerInvariant() == lower || t.FullName.ToLowerInvariant() == lower)
                    return t;
            }
        }

        return null;
    }

    static string Error(string msg) =>
        McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = false,
            ["error"] = msg
        });
}
