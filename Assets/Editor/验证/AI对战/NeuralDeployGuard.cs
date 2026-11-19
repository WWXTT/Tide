using System;
using System.Collections.Generic;
using System.IO;
using CardCore.AI.NeuralEnv;
using Newtonsoft.Json;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 部署一致性哨兵（2026-09-29；P-1 2026-11-19 口径更新）：部署件 onnx + fixture 的
    /// **布局/池尺寸**必须与当前代码同代。旧版按 manifest sha 对账（模型与卡身份清单分叉=
    /// embedding 串台）——稳定下标后身份纯派生、与清单无关，该对账退役（card_identity_manifest.json
    /// 仅存档）；改对 fixture 头 layout_version/dims（含 cardPoolSize/typePoolSize）。
    /// 分代 = 响亮失败（拒跑），不再只是警告。
    /// </summary>
    public static class NeuralDeployGuard
    {
        /// <summary>与 OnnxTidePolicy.ModelAssetFolder 同目录（Assets/Art/AIModels）。</summary>
        public const string FixtureFileName = "tide_policy_fixture.json";

        // ManifestPath 已删（P-1 2026-11-19）：manifest 指纹对账随稳定下标退役（文件仅存档）。

        public static string FixturePath
            => Path.Combine(Application.dataPath, "..", OnnxTidePolicy.ModelAssetFolder, FixtureFileName);

        /// <summary>
        /// 校验 fixture 记录的 manifest sha 与磁盘 manifest 是否同源。
        /// 返回 null = 通过；否则返回错误描述（可直接展示）。detail 附布局摘要。
        /// 2026-11-02 起追加布局对账（fixture 头 layout_version/dims vs 当前 TideObservation）：
        /// 盘面重塑/特征增删后旧模型即使清单同源，布局也不同代——一并拦截。
        /// P-1（2026-11-19 稳定下标）：manifest sha 对账**退役**（身份下标纯派生、与清单无关，
        /// 文件仅存档）；新增池尺寸对账（dims.cardPoolSize/typePoolSize vs 当前常量）——
        /// 旧 1024/256 池模型在此被拒（P-1 §0.4.4）。
        /// </summary>
        public static string Verify(out string detail)
        {
            detail = "";
            if (!File.Exists(FixturePath))
                return $"部署件缺 fixture：{FixturePath}（export_onnx.py 应与 onnx 一同复制到该目录）";

            int fxLayout;
            Dims fxDims;
            try
            {
                var fx = JsonConvert.DeserializeObject<FixtureHead>(File.ReadAllText(FixturePath));
                fxLayout = fx?.layout_version ?? 0;
                fxDims = fx?.dims;
            }
            catch (Exception ex)
            {
                return $"fixture 解析失败：{ex.Message}";
            }

            string layoutError = VerifyFixtureLayout(fxLayout, fxDims);
            if (layoutError != null)
                return layoutError;
            detail = $"fixture 布局 v{fxLayout}（cardPool {fxDims?.cardPoolSize ?? 0} / typePool {fxDims?.typePoolSize ?? 0}）";
            return null;
        }

        /// <summary>fixture 头布局（layoutVersion/dims）与当前 TideObservation 常量对账；
        /// 返回 null = 一致，否则返回错误描述。OnnxFixtureSelfCheck 与 Verify 共用单源。
        /// P-1（2026-11-19）：dims 增 cardPoolSize/typePoolSize 对账（缺键=旧版产物拒）。</summary>
        public static string VerifyFixtureLayout(int layoutVersion, Dims dims)
        {
            if (layoutVersion <= 0 || dims == null)
                return "fixture 缺 layout_version/dims 字段（旧版导出产物）——重跑 tide_rl/export_onnx.py";
            if (layoutVersion != CardCore.AI.NeuralEnv.TideObservation.LayoutVersion)
                return $"fixture 布局 v{layoutVersion} ≠ 当前契约 "
                     + $"v{CardCore.AI.NeuralEnv.TideObservation.LayoutVersion}"
                     + "（盘面/特征已改代，旧 onnx 全套作废）——重跑 tide_rl/export_onnx.py 重导出";
            var mismatches = new List<string>();
            if (dims.nCard != CardCore.AI.NeuralEnv.TideObservation.NCard) mismatches.Add($"nCard {dims.nCard}");
            if (dims.nGlobal != CardCore.AI.NeuralEnv.TideObservation.NGlobal) mismatches.Add($"nGlobal {dims.nGlobal}");
            if (dims.nAction != CardCore.AI.NeuralEnv.TideObservation.NAction) mismatches.Add($"nAction {dims.nAction}");
            if (dims.maxActions != OnnxTidePolicy.MaxActions) mismatches.Add($"maxActions {dims.maxActions}");
            if (dims.maxCards != CardCore.AI.NeuralEnv.TideObservation.MaxCardsTotal) mismatches.Add($"maxCards {dims.maxCards}");
            // P-1 池尺寸（旧版 fixture 无这两键=0，自然落入不匹配）
            if (dims.cardPoolSize != TideCardIndex.Capacity) mismatches.Add($"cardPoolSize {dims.cardPoolSize}");
            if (dims.typePoolSize != CardIdentityService.MaxTypeIndex + 1) mismatches.Add($"typePoolSize {dims.typePoolSize}");
            if (mismatches.Count > 0)
                return $"fixture 维度与当前观测编码不一致：{string.Join("、", mismatches)}"
                     + "——重跑 tide_rl/export_onnx.py 重导出";
            return null;
        }

        // 只取头部字段——fixture 主体是数百 KB 对拍样例，无需整包反序列化模型
        // （P-1 2026-11-19：manifest_sha256 字段随 manifest 退役——保留解析容错，缺省空串）
        private sealed class FixtureHead
        {
            public string manifest_sha256 = "";
            public int layout_version;
            public Dims dims;
        }

        // 嵌套 dims 对象（export_onnx.py 2026-11-02 起写入；P-1 2026-11-19 增池尺寸两键；
        // Json.NET 按字段名反序列化）
        public sealed class Dims
        {
            public int nCard;
            public int nGlobal;
            public int nAction;
            public int maxActions;
            public int maxCards;
            public int cardPoolSize;
            public int typePoolSize;
        }
    }
}
