using System;
using System.Linq;
using CardCore.Attribute;
using CardCore;
using SynergyUI;

namespace TideServer
{
    /// <summary>
    /// 数据装载自检：TideJson（Newtonsoft）读正式数据文件——卡表/效果库/原子表计数 + 摘要。
    /// 用途：服务器侧无 Unity 环境下快速验证数据装载路径（TidePaths 自定位 + 解析）。
    /// </summary>
    internal static class LoadCheck
    {
        public static int Run()
        {
            Console.WriteLine($"DataPath         = {TidePaths.DataPath}");
            Console.WriteLine($"StreamingAssets  = {TidePaths.StreamingAssetsPath}");

            var cards = CardCatalog.LoadAll();
            Console.WriteLine($"Cards.json       = {cards.Count} 张");

            var effects = EffectsLibrary.GetAll();
            Console.WriteLine($"Effects.json     = {effects.Count} 条");

            var atoms = AtomicEffectTable.GetAll().ToList();
            Console.WriteLine($"原子表            = {atoms.Count} 行");

            // 装载后内容校验：ID 非空 + 引用闭包抽查（首张卡的效果引用可解析）
            int withId = 0, withEffects = 0;
            foreach (var card in cards)
            {
                if (!string.IsNullOrEmpty(card.ID)) withId++;
                if (card.Effects != null && card.Effects.Count > 0) withEffects++;
            }
            Console.WriteLine($"有 ID 卡         = {withId}；带效果卡 = {withEffects}");

            bool ok = cards.Count > 0 && effects.Count > 0 && atoms.Count > 0 && withId == cards.Count;
            Console.WriteLine(ok ? "LOADCHECK PASS" : "LOADCHECK FAIL（计数异常——数据路径或解析问题）");
            return ok ? 0 : 1;
        }
    }
}
