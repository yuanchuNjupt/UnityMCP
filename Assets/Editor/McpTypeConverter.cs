using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// 类型转换工具 — JSON ↔ Unity SerializedProperty 双向转换
// 所有属性读写工具共享此工具类，确保类型处理一致
// ============================================================
internal static class McpTypeConverter
{
    /// <summary>序列化属性遍历时跳过的内部属性名</summary>
    public static readonly HashSet<string> SkipPropertyNames = new HashSet<string>
    {
        "m_ObjectHideFlags",
        "m_CorrespondingSourceObject",
        "m_PrefabInstance",
        "m_PrefabAsset",
        "m_GameObject"
    };

    // ---- 类型解析 ----

    /// <summary>
    /// 解析组件类型名称，支持简写（"Rigidbody"）、全名（"UnityEngine.Rigidbody"）、
    /// 程序集限定名（"UnityEngine.Rigidbody, UnityEngine"）
    /// </summary>
    public static Type ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new ArgumentException("typeName 不能为空");

        // 1. 直接解析（支持程序集限定名和完整类型名）
        Type type = Type.GetType(typeName);
        if (type != null) return type;

        // 2. 尝试 UnityEngine 命名空间
        type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
        if (type != null) return type;

        // 3. 尝试 UnityEngine.UI 命名空间
        type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
        if (type != null) return type;

        // 4. 尝试 UnityEngine.EventSystems 命名空间
        type = Type.GetType($"UnityEngine.EventSystems.{typeName}, UnityEngine.UI");
        if (type != null) return type;

        // 5. 尝试 Assembly-CSharp（用户脚本）
        type = Type.GetType($"{typeName}, Assembly-CSharp");
        if (type != null) return type;

        // 6. 遍历所有已加载程序集进行模糊匹配
        string lower = typeName.ToLowerInvariant();
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

    // ---- 输出: SerializedProperty → JSON ----

    /// <summary>将 SerializedProperty 的值转换为 JSON 兼容对象（带类型标记）</summary>
    public static object PropertyValueToJson(SerializedProperty prop)
    {
        var result = new Dictionary<string, object>();

        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                result["type"] = "int";
                result["value"] = (double)prop.intValue;
                break;
            case SerializedPropertyType.Float:
                result["type"] = "float";
                result["value"] = (double)prop.floatValue;
                break;
            case SerializedPropertyType.Boolean:
                result["type"] = "bool";
                result["value"] = prop.boolValue;
                break;
            case SerializedPropertyType.String:
                result["type"] = "string";
                result["value"] = prop.stringValue;
                break;
            case SerializedPropertyType.Vector2:
                result["type"] = "Vector2";
                result["value"] = Vec2ToList(prop.vector2Value);
                break;
            case SerializedPropertyType.Vector3:
                result["type"] = "Vector3";
                result["value"] = Vec3ToList(prop.vector3Value);
                break;
            case SerializedPropertyType.Vector4:
                result["type"] = "Vector4";
                result["value"] = Vec4ToList(prop.vector4Value);
                break;
            case SerializedPropertyType.Quaternion:
                var q = prop.quaternionValue;
                result["type"] = "Quaternion";
                result["value"] = Vec4ToList(new Vector4(q.x, q.y, q.z, q.w));
                break;
            case SerializedPropertyType.Color:
                result["type"] = "Color";
                result["value"] = ColorToList(prop.colorValue);
                break;
            case SerializedPropertyType.Enum:
                result["type"] = "enum";
                result["type_name"] = prop.enumDisplayNames.Length > 0
                    ? string.Join(", ", prop.enumDisplayNames)
                    : prop.type;
                result["value"] = prop.enumDisplayNames.Length > prop.enumValueIndex
                    ? prop.enumDisplayNames[prop.enumValueIndex]
                    : prop.enumValueIndex.ToString();
                result["index"] = prop.enumValueIndex;
                break;
            case SerializedPropertyType.ObjectReference:
                return ObjectReferenceToJson(prop.objectReferenceValue);
            case SerializedPropertyType.Rect:
                var r = prop.rectValue;
                result["type"] = "Rect";
                result["value"] = new List<object> { (double)r.x, (double)r.y, (double)r.width, (double)r.height };
                break;
            case SerializedPropertyType.Bounds:
                var b = prop.boundsValue;
                result["type"] = "Bounds";
                result["center"] = Vec3ToList(b.center);
                result["size"] = Vec3ToList(b.size);
                break;
            case SerializedPropertyType.LayerMask:
                result["type"] = "LayerMask";
                result["value"] = (double)prop.intValue;
                break;
            case SerializedPropertyType.Character:
                result["type"] = "char";
                result["value"] = (double)prop.intValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                var curve = prop.animationCurveValue;
                result["type"] = "AnimationCurve";
                result["keys_count"] = (double)(curve?.keys.Length ?? 0);
                break;
            case SerializedPropertyType.ExposedReference:
                return ObjectReferenceToJson(prop.exposedReferenceValue);
            case SerializedPropertyType.Generic:
                result["type"] = "generic";
                result["value"] = prop.isArray ? $"Array[{prop.arraySize}]" : "<unsupported>";
                break;
            default:
                result["type"] = "unknown";
                result["value"] = $"<{prop.propertyType}>";
                break;
        }

        return result;
    }

    // ---- 输入: JSON 值 → 设置属性 ----

    /// <summary>将 JSON 值转换并赋值到 SerializedProperty</summary>
    public static void SetPropertyValue(SerializedProperty prop, object jsonValue)
    {
        try
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = Convert.ToInt32(jsonValue);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(jsonValue);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = (bool)jsonValue;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = (string)jsonValue;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ListToVec2((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ListToVec3((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ListToVec4((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = ListToQuaternion((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = ListToColor((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Enum:
                    if (jsonValue is string enumName)
                        prop.enumValueIndex = FindEnumIndex(prop, enumName);
                    else if (jsonValue is double d)
                        prop.enumValueIndex = (int)d;
                    else if (jsonValue is long l)
                        prop.enumValueIndex = (int)l;
                    else if (jsonValue is int i)
                        prop.enumValueIndex = i;
                    else
                        throw new Exception($"无法将 {jsonValue?.GetType().Name} 转换为枚举");
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ResolveObjectReference(jsonValue);
                    break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = ListToRect((List<object>)jsonValue);
                    break;
                case SerializedPropertyType.Bounds:
                    var boundsDict = (Dictionary<string, object>)jsonValue;
                    prop.boundsValue = new Bounds(
                        ListToVec3((List<object>)boundsDict["center"]),
                        ListToVec3((List<object>)boundsDict["size"]));
                    break;
                case SerializedPropertyType.LayerMask:
                    prop.intValue = Convert.ToInt32(jsonValue);
                    break;
                case SerializedPropertyType.Character:
                    prop.intValue = Convert.ToInt32(jsonValue);
                    break;
                case SerializedPropertyType.ExposedReference:
                    prop.exposedReferenceValue = ResolveObjectReference(jsonValue);
                    break;
                default:
                    throw new Exception($"不支持的属性类型: {prop.propertyType}");
            }
        }
        catch (McpTypeException) { throw; }
        catch (Exception ex)
        {
            throw new McpTypeException(
                $"设置属性 '{prop.displayName}' (类型 {prop.propertyType}) 失败: {ex.Message}");
        }
    }

    // ---- 属性路径查找 ----

    /// <summary>通过点分隔路径查找 SerializedProperty（如 "m_LocalPosition.x"）</summary>
    public static SerializedProperty FindPropertyByPath(SerializedObject so, string dottedPath)
    {
        string[] segments = dottedPath.Split('.');
        SerializedProperty prop = so.FindProperty(segments[0]);
        if (prop == null)
            throw new Exception($"未找到属性 '{segments[0]}'");

        for (int i = 1; i < segments.Length; i++)
        {
            prop = prop.FindPropertyRelative(segments[i]);
            if (prop == null)
                throw new Exception($"未找到嵌套属性 '{segments[i]}' (路径: {dottedPath})");
        }

        return prop;
    }

    /// <summary>获取属性可用名称列表（用于错误提示）</summary>
    public static List<string> GetPropertyNames(SerializedObject so)
    {
        var names = new List<string>();
        SerializedProperty prop = so.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (!SkipPropertyNames.Contains(prop.name))
                    names.Add(prop.name);
            } while (prop.NextVisible(false));
        }
        return names;
    }

    // ---- 内部转换辅助 ----

    static int FindEnumIndex(SerializedProperty prop, string name)
    {
        for (int i = 0; i < prop.enumNames.Length; i++)
        {
            if (string.Equals(prop.enumNames[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new McpTypeException(
            $"枚举 '{prop.type}' 中没有值 '{name}'，可用值: [{string.Join(", ", prop.enumNames)}]");
    }

    static UnityEngine.Object ResolveObjectReference(object jsonValue)
    {
        if (jsonValue == null) return null;

        if (jsonValue is Dictionary<string, object> dict)
        {
            // 通过 InstanceID 查找（场景对象）
            if (dict.TryGetValue("instance_id", out object idObj))
            {
                int id = Convert.ToInt32(idObj);
                var obj = EditorUtility.InstanceIDToObject(id);
                if (obj == null)
                    throw new McpTypeException($"未找到 InstanceID={id} 的对象（可能已被删除）");
                return obj;
            }
            // 通过 GUID 查找（资源对象）
            if (dict.TryGetValue("guid", out object guidObj))
            {
                string path = AssetDatabase.GUIDToAssetPath((string)guidObj);
                if (string.IsNullOrEmpty(path))
                    throw new McpTypeException($"未找到 GUID='{guidObj}' 对应的资源");
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj == null)
                    throw new McpTypeException($"无法加载路径 '{path}' 的资源");
                return obj;
            }
            throw new McpTypeException("对象引用格式错误，需要 'instance_id' 或 'guid' 字段");
        }

        throw new McpTypeException($"对象引用类型错误，期望 object 或 null，收到 {jsonValue?.GetType().Name}");
    }

    static object ObjectReferenceToJson(UnityEngine.Object obj)
    {
        if (obj == null) return null;

        var result = new Dictionary<string, object>
        {
            ["type"] = "object_reference",
            ["object_type"] = obj.GetType().Name,
            ["name"] = obj.name
        };

        // 资源对象 → 返回 GUID
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long _))
        {
            result["is_asset"] = true;
            result["guid"] = guid;
            result["path"] = AssetDatabase.GetAssetPath(obj);
        }
        // 场景对象 → 返回 InstanceID
        else
        {
            result["is_asset"] = false;
            result["instance_id"] = obj.GetInstanceID();
        }

        return result;
    }

    // ---- Vector / Color 序列化辅助 ----

    static List<object> Vec2ToList(Vector2 v) => new List<object> { (double)v.x, (double)v.y };
    static List<object> Vec3ToList(Vector3 v) => new List<object> { (double)v.x, (double)v.y, (double)v.z };
    static List<object> Vec4ToList(Vector4 v) => new List<object> { (double)v.x, (double)v.y, (double)v.z, (double)v.w };
    static List<object> ColorToList(Color c) => new List<object> { (double)c.r, (double)c.g, (double)c.b, (double)c.a };

    static Vector2 ListToVec2(List<object> a) => new Vector2(ConvF(a[0]), ConvF(a[1]));
    static Vector3 ListToVec3(List<object> a) => new Vector3(ConvF(a[0]), ConvF(a[1]), ConvF(a[2]));
    static Vector4 ListToVec4(List<object> a) => new Vector4(ConvF(a[0]), ConvF(a[1]), ConvF(a[2]), ConvF(a[3]));
    static Quaternion ListToQuaternion(List<object> a) => new Quaternion(ConvF(a[0]), ConvF(a[1]), ConvF(a[2]), ConvF(a[3]));
    static Color ListToColor(List<object> a) => new Color(ConvF(a[0]), ConvF(a[1]), ConvF(a[2]), a.Count > 3 ? ConvF(a[3]) : 1f);

    static Rect ListToRect(List<object> a) => new Rect(ConvF(a[0]), ConvF(a[1]), ConvF(a[2]), ConvF(a[3]));

    static float ConvF(object o) => Convert.ToSingle(o);
}

/// <summary>类型转换专用异常</summary>
internal class McpTypeException : Exception
{
    public McpTypeException(string message) : base(message) { }
}

/// <summary>从 JSON args 字典中安全提取类型化参数</summary>
internal static class ConvertArg
{
    public static int ToInt(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value))
            throw new McpTypeException($"缺少必需参数 '{key}'");
        return Convert.ToInt32(value);
    }

    public static string ToString(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value) || value == null)
            throw new McpTypeException($"缺少必需参数 '{key}'");
        return value.ToString();
    }

    public static float ToFloat(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value))
            throw new McpTypeException($"缺少必需参数 '{key}'");
        return Convert.ToSingle(value);
    }

    public static bool ToBool(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value))
            throw new McpTypeException($"缺少必需参数 '{key}'");
        if (value is bool b) return b;
        throw new McpTypeException($"参数 '{key}' 应该是 boolean 类型");
    }

    public static List<object> ToList(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value))
            throw new McpTypeException($"缺少必需参数 '{key}'");
        if (value is List<object> list) return list;
        throw new McpTypeException($"参数 '{key}' 应该是 array 类型");
    }

    public static Dictionary<string, object> ToDict(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out object value))
            throw new McpTypeException($"缺少必需参数 '{key}'");
        if (value is Dictionary<string, object> dict) return dict;
        throw new McpTypeException($"参数 '{key}' 应该是 object 类型");
    }
}
