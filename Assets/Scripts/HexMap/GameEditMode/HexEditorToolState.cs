using System;
using UnityEngine;

namespace HexMap
{
    /// <summary>游戏内编辑器的工具模式（六模式，数字键语义随模式分流）</summary>
    public enum HexEditorToolMode
    {
        /// <summary>地形笔刷：左键拖涂地块贴图，右键拖抬升 / Shift+右键降低。
        /// 数字键 = 贴图索引（选项行 = 层名中文条目）</summary>
        Terrain,
        /// <summary>地形高度：左键拖把涂到的格子设为选项高度；右键拖 ±1 微调。
        /// 数字键 = 目标高程 0..HeightLimit（用户定案：仅 0-9 直接设定）</summary>
        Height,
        /// <summary>水：数字键 1=河流 2=湖泊 0=清除水（涂到的格从河/湖剔除）</summary>
        Water,
        /// <summary>路：数字键 1=道路 0=清路</summary>
        Road,
        /// <summary>植被：数字键 1..N=散布规则原型，0=清除该格实例</summary>
        Vegetation,
        /// <summary>POI：数字键 1=泉水 2=路点 0=删除半径内最近 POI（拖动连续放/删）</summary>
        Poi,
    }

    /// <summary>
    /// 编辑器工具状态桥（游戏内面板写 → HexMapEditingSystem 读，仿 HexMapRuntime 的
    /// MonoBehaviour→System 流向）。静态类而非 ECS 单例：编辑系统每帧主线程读，
    /// 无需跨 Job 访问；面板与系统同在主线程。
    ///
    /// OptionIndex 随 Mode 变化语义（0 通常是「删除/清除」，1..N 为子类型）；
    /// 各模式上限由编辑系统解析写入（ResolveLimits），面板按上限+层名表生成选项行。
    /// 默认值下（无面板实例）Mode=Terrain、PointerOverUI=false、TextInputFocused=false，
    /// 编辑系统行为与改造前逐位一致。
    /// </summary>
    public static class HexEditorToolState
    {
        /// <summary>当前工具模式（面板按钮/快捷键切换）</summary>
        public static HexEditorToolMode Mode = HexEditorToolMode.Terrain;

        /// <summary>当前选项序号（原 BrushTerrainIndex 泛化）：
        /// Terrain=贴图索引 / Height=目标高程 / Water 0..2 / Road 0..1 /
        /// Vegetation 0..VegPrototypeCount / Poi 0..2。0 恒为「删除/清除」。</summary>
        public static int OptionIndex = 1;

        /// <summary>贴图笔刷上限（ResolveLimits 从材质贴图数组 depth 解析后写入）</summary>
        public static int TerrainLimit = 9;

        /// <summary>高度模式上限（= min(9, MaxElevation)，数字键只有 0-9）</summary>
        public static int HeightLimit = 9;

        /// <summary>植被原型数（= HexMapFeatureSettings.scatterRules.Count）</summary>
        public static int VegPrototypeCount = 4;

        /// <summary>
        /// 贴图数组层序 → 中文层名（面板选项行显示；超出表长退「N·层」）。
        /// 层序约定见 HexMapFeatureSettings 类注释 / 贴图数组生成时的层序表。
        /// </summary>
        public static readonly string[] TerrainLayerNames =
        {
            "保留层1", "保留层2", "草绿", "草黄", "泥土", "暗崖壁",
            "碎石", "沙", "裂纹沙", "雪", "亮崖壁", "红崖壁",
        };

        /// <summary>POI 放置/删除半径（世界单位）</summary>
        public static float PoiRadius = 20f;

        /// <summary>指针悬停在编辑面板上（PointerEnter/Leave 维护）——为真时编辑系统不吃鼠标</summary>
        public static bool PointerOverUI;

        /// <summary>面板文本输入框聚焦中（Focus/Blur 维护）——为真时数字键/快捷键不抢输入</summary>
        public static bool TextInputFocused;

        /// <summary>模式/选项变化通知（面板刷新按钮高亮与选项行）</summary>
        public static event Action Changed;

        /// <summary>当前模式的选项上限（数字键 clamp 与面板选项行共用）</summary>
        public static int OptionLimit => Mode switch
        {
            HexEditorToolMode.Terrain => TerrainLimit,
            HexEditorToolMode.Height => HeightLimit,
            HexEditorToolMode.Water => 2,
            HexEditorToolMode.Road => 1,
            HexEditorToolMode.Vegetation => VegPrototypeCount,
            HexEditorToolMode.Poi => 2,
            _ => 9,
        };

        public static void SetMode(HexEditorToolMode mode)
        {
            if (Mode == mode)
                return;
            Mode = mode;
            OptionIndex = Mathf.Clamp(OptionIndex, 0, OptionLimit);
            Changed?.Invoke();
        }

        public static void SetOption(int index)
        {
            index = Mathf.Clamp(index, 0, OptionLimit);
            if (OptionIndex == index)
                return;
            OptionIndex = index;
            Changed?.Invoke();
        }

        /// <summary>域重载关闭时防跨 Play 残留（与 HexMapAuthoring.Install 的 ResetFrom 双保险）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = HexEditorToolMode.Terrain;
            OptionIndex = 1;
            TerrainLimit = 9;
            HeightLimit = 9;
            VegPrototypeCount = 4;
            PoiRadius = 20f;
            PointerOverUI = false;
            TextInputFocused = false;
            Changed = null;
        }
    }
}
