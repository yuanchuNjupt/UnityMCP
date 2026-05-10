using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// 工具: save_scene — 保存当前场景
// ============================================================
internal class SaveSceneTool : IMcpTool
{
    public string Name => "save_scene";

    public string Description =>
        "保存当前激活场景。所有通过 MCP 工具对场景的修改在保存前不会持久化，" +
        "关闭 Unity 后未保存的修改将丢失。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>()
    };

    public string Execute(Dictionary<string, object> args)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if (string.IsNullOrEmpty(scene.path))
        {
            // 未保存过的新场景 → 需要指定路径
            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = "当前场景从未保存过，请先在 Unity 中手动保存一次（Ctrl+S）以指定场景路径"
            });
        }

        bool saved = EditorSceneManager.SaveScene(scene);
        if (saved)
        {
            Debug.Log($"[UnityMCP] 场景已保存: {scene.path}");
            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = true,
                ["data"] = new Dictionary<string, object>
                {
                    ["scene_name"] = scene.name,
                    ["scene_path"] = scene.path
                }
            });
        }
        else
        {
            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = "保存场景失败"
            });
        }
    }
}
