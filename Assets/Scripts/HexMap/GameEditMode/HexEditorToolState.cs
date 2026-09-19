using System;
using UnityEngine;

namespace HexMap
{
    /// <summary>游戏内编辑器的工具模式</summary>
    public enum HexEditorToolMode
    {
        /// <summary>地形编辑（默认）：左键涂地块、右键抬升、Shift+右键降低——改造前现状行为</summary>
        Terrain,
        /// <summary>左键点击放泉水 POI</summary>
        PlaceSpring,
        /// <summary>左键点击放路点 POI</summary>
        PlaceRoadNode,
        /// <summary>左键点击删除半径内最近 POI</summary>
        DeletePoi,
    }

    /// <summary>
    /// 编辑器工具状态桥（游戏内面板写 → HexMapEditingSystem 读，仿 HexMapRuntime 的
    /// MonoBehaviour→System 流向）。静态类而非 ECS 单例：编辑系统每帧主线程读，
    /// 无需跨 Job 访问；面板与系统同在主线程。
    ///
    /// 默认值下（无面板实例）Mode=Terrain、PointerOverUI=false、TextInputFocused=false，
    /// 编辑系统行为与改造前逐位一致。
    /// </summary>
    public static class HexEditorToolState
    {
        /// <summary>当前工具模式（面板按钮/快捷键切换）</summary>
        public static HexEditorToolMode Mode = HexEditorToolMode.Terrain;

        /// <summary>笔刷地块索引（原 HexMapEditingSystem._activeTerrain 迁入；数字键与面板共写）</summary>
        public static int BrushTerrainIndex = 1;

        /// <summary>笔刷地块上限（ResolveTerrainLimit 从材质贴图数组解析后写入，面板建按钮行用）</summary>
        public static int TerrainLimit = 9;

        /// <summary>POI 放置/删除半径（世界单位）</summary>
        public static float PoiRadius = 20f;

        /// <summary>指针悬停在编辑面板上（PointerEnter/Leave 维护）——为真时编辑系统不吃鼠标</summary>
        public static bool PointerOverUI;

        /// <summary>面板文本输入框聚焦中（Focus/Blur 维护）——为真时数字键/快捷键不抢输入</summary>
        public static bool TextInputFocused;

        /// <summary>模式/笔刷变化通知（面板刷新按钮高亮）</summary>
        public static event Action Changed;

        public static void SetMode(HexEditorToolMode mode)
        {
            if (Mode == mode)
                return;
            Mode = mode;
            Changed?.Invoke();
        }

        public static void SetBrushTerrain(int index)
        {
            if (BrushTerrainIndex == index)
                return;
            BrushTerrainIndex = index;
            Changed?.Invoke();
        }

        /// <summary>域重载关闭时防跨 Play 残留（与 HexMapAuthoring.Install 的 ResetFrom 双保险）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = HexEditorToolMode.Terrain;
            BrushTerrainIndex = 1;
            TerrainLimit = 9;
            PoiRadius = 20f;
            PointerOverUI = false;
            TextInputFocused = false;
            Changed = null;
        }
    }
}
