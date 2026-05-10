using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: set_component_properties — 批量设置组件属性（一次 Undo）
// ============================================================
internal class SetComponentPropertiesTool : IMcpTool
{
    public string Name => "set_component_properties";

    public string Description =>
        "批量设置组件上多个序列化属性的值。所有修改在一个 Undo 步骤中完成，" +
        "任一属性设置失败则全部不应用。每个值的类型规则与 set_component_property 相同。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["component_instance_id"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "目标组件的 InstanceID"
            },
            ["properties"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = "属性名到值的映射对象，如 {\"m_Mass\": 5.0, \"m_UseGravity\": true}"
            }
        },
        ["required"] = new List<object> { "component_instance_id", "properties" }
    };

    public string Execute(Dictionary<string, object> args)
    {
        int componentId = ConvertArg.ToInt(args, "component_instance_id");
        var properties = ConvertArg.ToDict(args, "properties");

        if (properties.Count == 0)
        {
            return Error("'properties' 对象不能为空");
        }

        var component = EditorUtility.InstanceIDToObject(componentId) as Component;
        if (component == null)
        {
            return Error($"未找到 InstanceID={componentId} 的组件");
        }

        var so = new SerializedObject(component);
        var changedProps = new List<object>();
        var errors = new List<string>();

        try
        {
            // 第一遍：验证所有属性路径和类型（不回滚，仅验证）
            foreach (var kv in properties)
            {
                try
                {
                    SerializedProperty prop = McpTypeConverter.FindPropertyByPath(so, kv.Key);
                    McpTypeConverter.SetPropertyValue(prop, kv.Value);
                }
                catch (McpTypeException ex)
                {
                    errors.Add($"{kv.Key}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                // 放弃已验证但未提交的修改（未调用 ApplyModifiedProperties，Dispose 即丢弃）
                so.Dispose();
                return Error($"属性验证失败:\n{string.Join("\n", errors)}");
            }

            // 第二遍：实际应用（Undo 保护）
            Undo.RecordObject(component, $"Set {properties.Count} properties");

            foreach (var kv in properties)
            {
                SerializedProperty prop = McpTypeConverter.FindPropertyByPath(so, kv.Key);
                McpTypeConverter.SetPropertyValue(prop, kv.Value);
                changedProps.Add(kv.Key);
            }

            so.ApplyModifiedProperties();

            Debug.Log($"[UnityMCP] 批量设置属性: {component.gameObject.name}.{component.GetType().Name} " +
                      $"[{string.Join(", ", changedProps)}]");

            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = true,
                ["data"] = new Dictionary<string, object>
                {
                    ["component_instance_id"] = componentId,
                    ["component_type"] = component.GetType().Name,
                    ["game_object_name"] = component.gameObject.name,
                    ["changed_properties"] = changedProps,
                    ["count"] = (double)changedProps.Count
                }
            });
        }
        catch (McpTypeException ex)
        {
            return Error(ex.Message);
        }
        finally
        {
            so.Dispose();
        }
    }

    static string Error(string msg) =>
        McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = false,
            ["error"] = msg
        });
}
