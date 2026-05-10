using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: set_component_property — 设置组件的单个属性值
// ============================================================
internal class SetComponentPropertyTool : IMcpTool
{
    public string Name => "set_component_property";

    public string Description =>
        "设置组件上单个序列化属性的值。属性名支持点分隔路径（如 'm_LocalPosition.x'）。" +
        "值类型取决于属性类型：\n" +
        "- int/float/bool/string: 原生 JSON 类型\n" +
        "- Vector2/3/4: 数组 [x,y,...]\n" +
        "- Color: 数组 [r,g,b,a] (0-1 范围)\n" +
        "- Enum: 字符串（枚举值名）或整数（索引）\n" +
        "- ObjectReference: {\"instance_id\":N} 或 {\"guid\":\"...\"} 或 null";

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
            ["property_name"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "序列化属性名称，支持点分隔的嵌套路径（如 'm_LocalPosition.x'）"
            },
            ["property_value"] = new Dictionary<string, object>
            {
                ["description"] = "新的属性值。类型取决于目标属性，详见工具描述"
            }
        },
        ["required"] = new List<object> { "component_instance_id", "property_name", "property_value" }
    };

    public string Execute(Dictionary<string, object> args)
    {
        int componentId = ConvertArg.ToInt(args, "component_instance_id");
        string propertyName = ConvertArg.ToString(args, "property_name");

        if (!args.TryGetValue("property_value", out object propertyValue))
        {
            return Error("缺少必需参数 'property_value'");
        }

        var component = EditorUtility.InstanceIDToObject(componentId) as Component;
        if (component == null)
        {
            return Error($"未找到 InstanceID={componentId} 的组件");
        }

        var so = new SerializedObject(component);
        try
        {
            SerializedProperty prop = McpTypeConverter.FindPropertyByPath(so, propertyName);
            Undo.RecordObject(component, $"Set {propertyName}");
            McpTypeConverter.SetPropertyValue(prop, propertyValue);
            so.ApplyModifiedProperties();

            // 读取设置后的值用于确认
            object updatedValue = McpTypeConverter.PropertyValueToJson(prop);

            LogChange(component, propertyName);

            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = true,
                ["data"] = new Dictionary<string, object>
                {
                    ["component_instance_id"] = componentId,
                    ["component_type"] = component.GetType().Name,
                    ["game_object_name"] = component.gameObject.name,
                    ["property_name"] = propertyName,
                    ["new_value"] = updatedValue
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

    static void LogChange(Component comp, string propName)
    {
        Debug.Log($"[UnityMCP] 设置属性: {comp.gameObject.name}.{comp.GetType().Name}.{propName}");
    }
}
