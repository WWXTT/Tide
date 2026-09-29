using System;

namespace CardCore.Network
{
    /// <summary>
    /// 网络投影排除（2026-09-22 线上去文本定案）：标记的事件公共属性不进 NetEvent 参数。
    /// 用于服务器侧拼好的显示文本与不可结构化的重对象（如 EffectExecutionSummaryEvent.Description）——
    /// 线上只传 ID 与运行态，客户端按 ID 查本地表（Effects.json/原子表）重渲染文本。
    /// 本地 CLR 订阅者（战报 MatchLogRenderer 等）不受影响；schema-less 演进语义不变（字段从线上消失对旧客户端透明）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class NetProjectionIgnoreAttribute : System.Attribute
    {
    }
}
