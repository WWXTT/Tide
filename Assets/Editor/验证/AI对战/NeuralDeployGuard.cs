using System;
using System.IO;
using CardCore.AI.NeuralEnv;
using Newtonsoft.Json;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 部署一致性哨兵（2026-09-29）：部署三件套 onnx + fixture + manifest 必须同源。
    /// fixture 内 manifest_sha256 是导出时刻录的清单指纹；与磁盘 card_identity_manifest.json
    /// 的 sha256 不等 = 模型与身份清单已分叉（游戏内容变更/续训/漏跑 sync_model_to_main），
    /// 精确哈希 embedding 行会静默串台、强度悄悄劣化——历史上「编辑器窗口 55 开 vs 训练
    /// eval 80 开」的怪差即此因之一。定案：分叉 = 响亮失败（拒跑），不再只是警告。
    /// </summary>
    public static class NeuralDeployGuard
    {
        /// <summary>与 OnnxTidePolicy.ModelAssetFolder 同目录（Assets/Art/AIModels）。</summary>
        public const string FixtureFileName = "tide_policy_fixture.json";

        public static string ManifestPath
            => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

        public static string FixturePath
            => Path.Combine(Application.dataPath, "..", OnnxTidePolicy.ModelAssetFolder, FixtureFileName);

        /// <summary>
        /// 校验 fixture 记录的 manifest sha 与磁盘 manifest 是否同源。
        /// 返回 null = 通过；否则返回错误描述（可直接展示）。detail 附两侧指纹前缀。
        /// </summary>
        public static string Verify(out string detail)
        {
            detail = "";
            if (!File.Exists(FixturePath))
                return $"部署三件套缺 fixture：{FixturePath}（export_onnx.py 应与 onnx 一同复制到该目录）";
            if (!File.Exists(ManifestPath))
                return $"manifest 不存在：{ManifestPath}";

            string fxSha;
            try
            {
                var fx = JsonConvert.DeserializeObject<FixtureHead>(File.ReadAllText(FixturePath));
                fxSha = fx?.manifest_sha256;
            }
            catch (Exception ex)
            {
                return $"fixture 解析失败：{ex.Message}";
            }
            if (string.IsNullOrEmpty(fxSha))
                return "fixture 内无 manifest_sha256 字段（旧版导出产物）——重跑 tide_rl/export_onnx.py";

            string diskSha = Sha256(File.ReadAllBytes(ManifestPath));
            detail = $"fixture {Short(fxSha)}… vs 磁盘 {Short(diskSha)}…";
            if (fxSha != diskSha)
                return "模型导出时的卡身份 manifest 与当前 card_identity_manifest.json 不同源，"
                     + "精确身份 embedding 行会串台。修复：游戏内容变更后重训并重跑 export_onnx.py，"
                     + "或先跑 tide_rl/sync_model_to_main.ps1 同步导出时的清单。";
            return null;
        }

        private static string Short(string sha) => sha.Length > 16 ? sha[..16] : sha;

        private static string Sha256(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        // 只取头部字段——fixture 主体是数百 KB 对拍样例，无需整包反序列化模型
        private sealed class FixtureHead
        {
            public string manifest_sha256 = "";
        }
    }
}
