using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// 工具: get_scene_gameobjects — 浏览当前场景中所有 GameObject
// ============================================================
internal class GetSceneGameObjectsTool : IMcpTool
{
    public string Name => "get_scene_gameobjects";

    public string Description =>
        "获取当前激活场景中所有 GameObject 的基本信息。支持按名称筛选。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["name_filter"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "可选：按名称筛选（不区分大小写的部分匹配），不传则返回全部"
            }
        }
    };

    public string Execute(Dictionary<string, object> args)
    {
        string filter = null;
        if (args.TryGetValue("name_filter", out object f) && f is string fs && !string.IsNullOrWhiteSpace(fs))
            filter = fs.ToLowerInvariant();

        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        var goList = new List<object>();

        // 迭代栈遍历，避免深层嵌套递归爆栈
        var stack = new Stack<Transform>();
        foreach (var root in roots)
            stack.Push(root.transform);

        while (stack.Count > 0)
        {
            Transform t = stack.Pop();
            GameObject go = t.gameObject;

            // 名称筛选
            if (filter != null && !go.name.ToLowerInvariant().Contains(filter))
            {
                // 不匹配但子物体可能匹配，继续入栈
                for (int i = t.childCount - 1; i >= 0; i--)
                    stack.Push(t.GetChild(i));
                continue;
            }

            // 收集组件类型名
            Component[] components = go.GetComponents<Component>();
            var compTypes = new List<object>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c != null)
                    compTypes.Add(c.GetType().Name);
            }

            Vector3 pos = t.position;
            goList.Add(new Dictionary<string, object>
            {
                ["instance_id"] = go.GetInstanceID(),
                ["name"] = go.name,
                ["active"] = go.activeSelf,
                ["component_types"] = compTypes,
                ["position"] = new List<object>
                {
                    (double)pos.x, (double)pos.y, (double)pos.z
                }
            });

            // 子物体入栈
            for (int i = t.childCount - 1; i >= 0; i--)
                stack.Push(t.GetChild(i));
        }

        var result = new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = new Dictionary<string, object>
            {
                ["count"] = goList.Count,
                ["game_objects"] = goList
            }
        };

        return McpJson.Stringify(result);
    }
}
