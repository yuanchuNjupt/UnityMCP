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
        "Create a primitive GameObject in the current scene. " +
        "Supported types: Sphere, Cube, Capsule, Cylinder, Plane, Quad.";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
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

        return $"Created {primitiveType} '{goName}' at ({x:F2}, {y:F2}, {z:F2})";
    }
}
