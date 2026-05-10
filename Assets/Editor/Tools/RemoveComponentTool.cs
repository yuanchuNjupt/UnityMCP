using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: remove_component — 移除组件
// ============================================================
internal class RemoveComponentTool : IMcpTool
{
    public string Name => "remove_component";

    public string Description =>
        "移除指定组件（通过 InstanceID）。Transform 组件无法被移除。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["instance_id"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "要移除的组件 InstanceID"
            }
        },
        ["required"] = new List<object> { "instance_id" }
    };

    public string Execute(Dictionary<string, object> args)
    {
        int instanceId = ConvertArg.ToInt(args, "instance_id");
        var component = EditorUtility.InstanceIDToObject(instanceId) as Component;

        if (component == null)
        {
            return Error($"未找到 InstanceID={instanceId} 的组件");
        }

        // Transform/RectTransform 不可移除
        if (component is Transform)
        {
            return Error("不能移除 Transform 组件（每个 GameObject 必须拥有一个 Transform）");
        }

        string typeName = component.GetType().Name;
        string goName = component.gameObject.name;

        Undo.DestroyObjectImmediate(component);

        Debug.Log($"[UnityMCP] 移除组件: {goName}.{typeName} (ID={instanceId})");

        return McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = new Dictionary<string, object>
            {
                ["removed_instance_id"] = instanceId,
                ["type_name"] = typeName,
                ["gameobject_name"] = goName
            }
        });
    }

    static string Error(string msg) =>
        McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = false,
            ["error"] = msg
        });
}
