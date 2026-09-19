using System.Collections.Generic;

namespace HexMap
{
    /// <summary>
    /// 运行时 POI 列表（游戏内编辑器的权威数据源，存档覆盖）。
    /// HexMapFeatureSettings.pois 降级为「初始种子」——Install 时经 ResetFrom 覆盖式拷贝进来，
    /// 之后游戏内放置/删除都只改本表（HexPoiData 是 struct，列表拷贝即值拷贝，
    /// 不会误改资产）。特征生成（泉眼/路网）从本表读取。
    /// </summary>
    public static class HexPoiRuntime
    {
        public static readonly List<HexPoiData> Pois = new List<HexPoiData>();

        /// <summary>Install 时播种：settings.pois → 运行时表（覆盖式，防域重载残留累积）</summary>
        public static void ResetFrom(HexMapFeatureSettings s)
        {
            Pois.Clear();
            if (s?.pois != null)
                Pois.AddRange(s.pois);
        }
    }
}
