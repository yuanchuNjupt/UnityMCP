using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 工具: get_component_properties — 读取组件的所有序列化属性
// ============================================================
internal class GetComponentPropertiesTool : IMcpTool
{
    public string Name => "get_component_properties";

    public string Description =>
        "读取指定组件（通过 InstanceID）的所有序列化字段及其当前值。" +
        "返回类型标记的 JSON，可直接用于 set_component_property。";

    public Dictionary<string, object> InputSchema => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["instance_id"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "目标组件（Component）的 InstanceID"
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
            return McpJson.Stringify(new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = $"未找到 InstanceID={instanceId} 的组件"
            });
        }

        var so = new SerializedObject(component);
        var properties = new Dictionary<string, object>();

        try
        {
            SerializedProperty prop = so.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (McpTypeConverter.SkipPropertyNames.Contains(prop.name))
                        continue;

                    if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic)
                    {
                        ReadArrayProperty(prop, properties);
                        continue;
                    }

                    object jsonValue = McpTypeConverter.PropertyValueToJson(prop);
                    if (jsonValue != null)
                        properties[prop.name] = jsonValue;
                }
                while (prop.NextVisible(false));
            }
        }
        finally
        {
            so.Dispose();
        }

        return McpJson.Stringify(new Dictionary<string, object>
        {
            ["success"] = true,
            ["data"] = new Dictionary<string, object>
            {
                ["instance_id"] = instanceId,
                ["type_name"] = component.GetType().Name,
                ["full_type_name"] = component.GetType().FullName,
                ["game_object_name"] = component.gameObject.name,
                ["game_object_instance_id"] = component.gameObject.GetInstanceID(),
                ["property_count"] = (double)properties.Count,
                ["properties"] = properties
            }
        });
    }

    static void ReadArrayProperty(SerializedProperty arrayProp,
        Dictionary<string, object> output)
    {
        int size = arrayProp.arraySize;
        var elements = new List<object>();

        for (int i = 0; i < size; i++)
        {
            SerializedProperty elem = arrayProp.GetArrayElementAtIndex(i);
            object jsonValue = McpTypeConverter.PropertyValueToJson(elem);
            elements.Add(jsonValue ?? new Dictionary<string, object>());
        }

        output[arrayProp.name] = new Dictionary<string, object>
        {
            ["type"] = "array",
            ["size"] = (double)size,
            ["elements"] = elements
        };
    }
}
