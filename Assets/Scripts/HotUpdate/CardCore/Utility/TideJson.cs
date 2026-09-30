using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CardCore
{
    /// <summary>
    /// 序列化回调（2026-09-27 去 Unity 化）：对齐 Unity ISerializationCallbackReceiver 语义的纯 C# 接口。
    /// TideJson 在序列化前/反序列化后按对象图遍历调用（根对象与嵌套可序列化对象皆生效）。
    /// </summary>
    public interface ITideSerializationCallback
    {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }

    /// <summary>
    /// [TideSerialized]：私有字段的序列化标记（2026-09-27 去 Unity 化）——取代共享源码中的
    /// [SerializeField]，语义相同：TideJson 对 public 字段与此标记的私有字段序列化。
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class TideSerializedAttribute : System.Attribute
    {
    }

    /// <summary>
    /// JsonUtility 等价序列化门面（2026-09-27 去 Unity 化）：Newtonsoft.Json 双端同实现
    /// （Unity 经 com.unity.nuget.newtonsoft-json 包，TideServer 经 NuGet 13.x）——彻底消灭
    /// 两套序列化器并存的对拍风险。
    ///
    /// 成员规则与 JsonUtility 对齐：
    /// - 只认 public 字段 + [TideSerialized] 私有字段；属性/静态字段/[NonSerialized] 一律不序列化；
    /// - 全字段写出（含默认值与 null——JsonUtility 同款，见 Cards.json/AttributeValueConfig.json 实样）；
    /// - 读侧：缺字段落默认值、未知字段忽略、枚举按数值、数值/字符串标准 JSON——
    ///   存量数据文件（JsonUtility 写出）零迁移直读。
    ///
    /// 内容哈希无关性（重要边界）：卡/效果 ID（ContentHasher）与卡组摘要（NetMatchHandshake）
    /// 均为手工拼串哈希，不经本序列化器——序列化器替换不影响任何 ID 稳定性。
    /// </summary>
    public static class TideJson
    {
        private static readonly TideContractResolver Resolver = new TideContractResolver();
        private static readonly RefEqualityComparer RefEq = RefEqualityComparer.Instance;

        public static string ToJson(object obj, bool prettyPrint = false)
        {
            if (obj == null) return "";
            InvokeCallbacks(obj, before: true);
            return JsonConvert.SerializeObject(obj, Settings(prettyPrint));
        }

        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            var result = JsonConvert.DeserializeObject<T>(json, Settings(pretty: false));
            if (result != null) InvokeCallbacks(result, before: false);
            return result;
        }

        private static JsonSerializerSettings Settings(bool pretty)
        {
            return new JsonSerializerSettings
            {
                ContractResolver = Resolver,
                Formatting = pretty ? Formatting.Indented : Formatting.None,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
            };
        }

        // ============================================================ 序列化回调遍历 ============================================================

        private static void InvokeCallbacks(object root, bool before)
        {
            var visited = new HashSet<object>(RefEq);
            Walk(root, before, visited);
        }

        private static void Walk(object node, bool before, HashSet<object> visited)
        {
            if (node == null) return;
            var type = node.GetType();
            if (type.IsPrimitive || type.IsEnum || node is string) return;
            if (!visited.Add(node)) return; // 引用去重防环（数据树本应无环）

            if (node is ITideSerializationCallback cb)
            {
                if (before) cb.OnBeforeSerialize();
                else cb.OnAfterDeserialize();
            }

            // 容器穿透（List/数组等被序列化的集合元素递归回调）
            if (node is IEnumerable seq)
            {
                foreach (var item in seq) Walk(item, before, visited);
                return;
            }

            foreach (var f in GetSerializableFields(type))
            {
                object value;
                try { value = f.GetValue(node); }
                catch { continue; }
                Walk(value, before, visited);
            }
        }

        // ============================================================ 契约解析器 ============================================================

        private sealed class TideContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = new List<JsonProperty>();
                foreach (var f in GetSerializableFields(type))
                {
                    var jp = CreateProperty(f, memberSerialization);
                    jp.Readable = true;
                    jp.Writable = true;
                    props.Add(jp);
                }
                return props;
            }
        }

        /// <summary>JsonUtility 口径的成员筛选：public 字段 + [TideSerialized] 私有字段；跳过 [NonSerialized]/静态。</summary>
        private static List<FieldInfo> GetSerializableFields(Type type)
        {
            var result = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized) continue;
                    if (!f.IsPublic && !f.IsDefined(typeof(TideSerializedAttribute), inherit: false)) continue;
                    result.Add(f);
                }
            }
            return result;
        }

        private sealed class RefEqualityComparer : IEqualityComparer<object>
        {
            public static readonly RefEqualityComparer Instance = new RefEqualityComparer();

            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);

            int IEqualityComparer<object>.GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }
}
