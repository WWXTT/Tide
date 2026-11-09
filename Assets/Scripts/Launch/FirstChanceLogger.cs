using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide.Launch
{
    /// <summary>
    /// FirstChance 异常日志（2026-11-09 A4）：真机联机失败排障实录——热更代码踩 AOT 泛型缺口
    /// 抛 MissingMethodException 后被上层 catch 静默吞掉，logcat 零痕迹，只能靠 strace 定位。
    /// 本类在异常「抛出的第一时刻」落日志（无论后续是否被 catch 捕获）：AOT 类异常
    /// （MissingMethod/MissingField/TypeLoad/TypeInitialization）全量+堆栈，其余类型只留
    /// 首行防刷屏；release 包同样生效（不挂 DEVELOPMENT_BUILD 宏）。
    /// 挂载走 RuntimeInitializeOnLoadMethod(BeforeSceneLoad)——不依赖热更与启动脚本调用链
    /// （HotUpdateLoader/Launch 均在 TIDE_HYBRID_YOO 宏内，宏关时缺席），任何构建形态启动即生效。
    /// </summary>
    public static class FirstChanceLogger
    {
        /// <summary>同类异常最多记录条数（超出静默——防异常风暴刷屏淹没 logcat）。</summary>
        private const int MaxEntriesPerType = 20;

        private static readonly Dictionary<Type, int> Counts = new Dictionary<Type, int>();
        private static bool _installed;
        private static bool _inHandler; // 日志链自身抛异常时防递归

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            AppDomain.CurrentDomain.FirstChanceException += (_, e) => Handle(e.Exception);
            Debug.Log("[FirstChance] 异常日志已挂载");
        }

        private static void Handle(Exception ex)
        {
            if (_inHandler) return;
            _inHandler = true;
            try
            {
                Type type = ex.GetType();
                Counts.TryGetValue(type, out int n);
                if (n >= MaxEntriesPerType) return;
                Counts[type] = n + 1;
                if (n == MaxEntriesPerType - 1)
                    Debug.Log($"[FirstChance] {type.Name} 已达记录上限（{MaxEntriesPerType}），后续静默");
                bool aot = ex is MissingMethodException || ex is MissingFieldException
                    || ex is TypeLoadException || ex is TypeInitializationException;
                if (aot)
                    Debug.Log($"[FirstChance] AOT {type.Name}: {ex.Message}\n{ex.StackTrace}");
                else
                    Debug.Log($"[FirstChance] other {type.Name}: {ex.Message}");
            }
            finally { _inHandler = false; }
        }
    }
}
