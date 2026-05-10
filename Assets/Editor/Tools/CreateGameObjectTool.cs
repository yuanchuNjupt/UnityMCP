using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: create_gameobject — 在当前场景中创建基础 GameObject
// ============================================================
internal class CreateGameObjectTool : IMcpTool
{
    public string Name => "create_gameobject";

    public string Description =>
        "在当前场景中创建基础 GameObject（Sphere/Cube/Capsule/Cylinder/Plane/Quad）。" +
        "返回新物体的 InstanceID 及所有组件的 InstanceID，可直接用于后续操作。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["primitive_type"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "基础网格类型",
                ["enum"] = new List<object> { "Sphere", "Cube", "Capsule", "Cylinder", "Plane", "Quad" }
            },
            ["name"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "新 GameObject 的名称（默认与类型同名）"
            },
            ["position"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["description"] = "世界坐标 [x, y, z]，默认为 (0, 0, 0)",
                ["items"] = new Dictionary<string, object> { ["type"] = "number" }
            }
        }
    };

    public string Execute(Dictionary<string, object> args)
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

        // 收集所有组件的 InstanceID，方便 Agent 直接使用
        Component[] components = go.GetComponents<Component>();
        var compList = new List<object>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null) continue;
            compList.Add(new Dictionary<string, object>
            {
                ["instance_id"] = c.GetInstanceID(),
                ["type_name"] = c.GetType().Name
            });
        }

        return McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = new Dictionary<string, object>
            {
                ["instance_id"] = go.GetInstanceID(),
                ["name"] = goName,
                ["type"] = primitiveType,
                ["position"] = new List<object> { (double)x, (double)y, (double)z },
                ["components"] = compList
            }
        });
    }
}
