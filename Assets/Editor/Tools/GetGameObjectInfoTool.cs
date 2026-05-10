using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: get_gameobject_info — 查看单个 GameObject 详细信息
// ============================================================
internal class GetGameObjectInfoTool : IMcpTool
{
    public string Name => "get_gameobject_info";

    public string Description =>
        "获取指定 GameObject 的详细信息，包括所有组件的 InstanceID 列表。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["instance_id"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "目标 GameObject 的 InstanceID"
            }
        },
        ["required"] = new List<object> { "instance_id" }
    };

    public string Execute(Dictionary<string, object> args)
    {
        int instanceId = ConvertArg.ToInt(args, "instance_id");
        var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

        if (go == null)
        {
            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = $"未找到 InstanceID={instanceId} 的 GameObject"
            });
        }

        // 基本信息
        var data = new Dictionary<string, object>
        {
            ["instance_id"] = go.GetInstanceID(),
            ["name"] = go.name,
            ["active"] = go.activeSelf,
            ["active_in_hierarchy"] = go.activeInHierarchy,
            ["tag"] = go.tag,
            ["layer"] = (double)go.layer,
            ["is_static"] = go.isStatic,
            ["scene"] = go.scene.name
        };

        // Transform
        Transform t = go.transform;
        var q = t.rotation;
        data["transform"] = new Dictionary<string, object>
        {
            ["position"] = Vec3ToList(t.position),
            ["local_position"] = Vec3ToList(t.localPosition),
            ["rotation_euler"] = Vec3ToList(t.rotation.eulerAngles),
            ["local_rotation_euler"] = Vec3ToList(t.localRotation.eulerAngles),
            ["rotation_quat"] = Vec4ToList(new Vector4(q.x, q.y, q.z, q.w)),
            ["scale"] = Vec3ToList(t.lossyScale),
            ["local_scale"] = Vec3ToList(t.localScale),
            ["parent_instance_id"] = t.parent != null ? t.parent.gameObject.GetInstanceID() : 0,
            ["child_count"] = (double)t.childCount
        };

        // 组件列表
        Component[] components = go.GetComponents<Component>();
        var compList = new List<object>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null) continue;

            var compEntry = new Dictionary<string, object>
            {
                ["instance_id"] = c.GetInstanceID(),
                ["type_name"] = c.GetType().Name,
                ["full_type_name"] = c.GetType().FullName
            };

            // Behaviour 才有 enabled
            if (c is Behaviour behaviour)
                compEntry["enabled"] = behaviour.enabled;
            else
                compEntry["enabled"] = null;

            compList.Add(compEntry);
        }
        data["components"] = compList;
        data["component_count"] = (double)compList.Count;

        return McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = data
        });
    }

    static List<object> Vec3ToList(Vector3 v) =>
        new List<object> { (double)v.x, (double)v.y, (double)v.z };

    static List<object> Vec4ToList(Vector4 v) =>
        new List<object> { (double)v.x, (double)v.y, (double)v.z, (double)v.w };
}
