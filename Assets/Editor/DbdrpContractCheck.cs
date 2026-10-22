// Dbdrp 版本契约检查（2026-10-22 建立；同日 P3 判据翻转：渲染层 uint 编码一致性）
// 目的：防止 GBuffer ABI 四组（①写出端 ②分配端 ③读取端 ④宏定义）被"单边升级"。
// 背景：9-30 的 URP 17.5 合并只升了 shader 侧，C# 被退回 fork 原版，
//       导致 17.5 的 GBufferFragOutput 与 fork 的 GBuffer 分配器/读取端错配，
//       D3D12 下渲染层切片解码为 0 → 光层闸门判空 → 所有方向光被跳过（"纯色不受光"）。
// 2026-10-22 P3 方案 A 落地后判据翻转：布局槽位保持 fork（渲染层@MRT4，无深度切片），
//       编码全面 uint：②GetFormat=R8_UInt/R16_UInt/R32_UInt；③compute 以 typed uint4 读取；
//       ①写出端 17.5 原生（PackGBuffers*）与 fork 兼容层（BRDFDataToGbuffer）皆合法，
//       但渲染层写出一律 uint；④登记 fork 衍生 GBufferOutput.hlsl 的 UseBakedGI 位。
// 用法：菜单 Tide/Dbdrp/检查版本契约（Ctrl/Cmd+Shift+D），或 CI 调 RunSilent()。
// 产出：Console + Logs/DbdrpContractCheck.txt
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tide.EditorTools
{
    public static class DbdrpContractCheck
    {
        const string Pkg = "Packages/Dbdrp/DanbaidongRP-urp17";

        [MenuItem("Tide/Dbdrp/检查版本契约 %#d")]
        public static void RunMenu()
        {
            bool ok = RunSilent();
            EditorUtility.DisplayDialog(
                "Dbdrp 版本契约",
                ok ? "检查通过。" : "检查未通过：详见 Console 与 Logs/DbdrpContractCheck.txt",
                "好");
        }

        /// <summary>返回 true = 全部通过（可用于 CI / 批处理）</summary>
        public static bool RunSilent()
        {
            var sb = new StringBuilder();
            int fails = 0;
            int warns = 0;

            sb.AppendLine("=== Dbdrp 版本契约检查 ===");
            sb.AppendLine("时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("根目录: " + ProjectRoot());
            sb.AppendLine();

            // ---------- 1. GBuffer ABI 四组一致性 ----------
            sb.AppendLine("[1] GBuffer ABI 四组（P3 判据：槽位=fork(渲染层@MRT4)，编码=uint）");
            string writer = Read("Shaders/LitGBufferPass.hlsl");
            string compute = Read("Shaders/Lighting/DeferredLighting.compute");
            string deferredLights = Read("Runtime/DeferredLights.cs");
            string gbufferCommon = Read("ShaderLibrary/GBufferCommon.hlsl");
            string unityGBuffer = Read("ShaderLibrary/UnityGBuffer.hlsl");
            string gbufferOutput = Read("ShaderLibrary/GBufferOutput.hlsl");
            string layerUtils = Read("Runtime/RenderingLayerUtils.cs");

            if (writer == null) { Fail(sb, ref fails, "缺少 Shaders/LitGBufferPass.hlsl"); }
            if (compute == null) { Fail(sb, ref fails, "缺少 Shaders/Lighting/DeferredLighting.compute"); }
            if (deferredLights == null) { Fail(sb, ref fails, "缺少 Runtime/DeferredLights.cs"); }
            if (gbufferCommon == null) { Fail(sb, ref fails, "缺少 ShaderLibrary/GBufferCommon.hlsl"); }
            if (unityGBuffer == null) { Fail(sb, ref fails, "缺少 ShaderLibrary/UnityGBuffer.hlsl（fork 兼容层）"); }
            if (gbufferOutput == null) { Fail(sb, ref fails, "缺少 ShaderLibrary/GBufferOutput.hlsl"); }
            if (layerUtils == null) { Fail(sb, ref fails, "缺少 Runtime/RenderingLayerUtils.cs"); }

            // ① 写出端：17.5 原生（PackGBuffers*）与 fork 兼容层（BRDFDataToGbuffer）都合法，
            //    编码不变量由下方 UnityGBuffer 的 uint 写出断言把关。
            if (writer != null)
            {
                bool w175 = writer.Contains("PackGBuffersBRDFData");
                bool w173 = writer.Contains("BRDFDataToGbuffer");
                sb.AppendLine("  ①写出端: " + (w175 ? "17.5(PackGBuffersBRDFData)" : (w173 ? "fork兼容层(BRDFDataToGbuffer)" : "未知")));

                if (w175 && w173) Fail(sb, ref fails, "写出端同时出现 17.3/17.5 两种写出函数，确认是否被手工拼接");
                if (!w175 && !w173) Fail(sb, ref fails, "写出端既不是 BRDFDataToGbuffer 也不是 PackGBuffersBRDFData，无法识别");
            }

            if (unityGBuffer != null)
            {
                bool uintWrite = unityGBuffer.Contains("GBUFFER_LIGHT_LAYERS = EncodeMeshRenderingLayer()");
                bool floatWrite = unityGBuffer.Contains("float4(EncodeMeshRenderingLayer");
                bool bakedGI = unityGBuffer.Contains("kMaterialFlagUseBakedGI");
                sb.AppendLine("  ①编码: UnityGBuffer uint写出=" + uintWrite + " 残留float写出=" + floatWrite + " kMaterialFlagUseBakedGI=" + bakedGI);
                if (floatWrite || !uintWrite)
                    Fail(sb, ref fails, "UnityGBuffer.hlsl 渲染层写出仍是 float 归一化 —— 与 ②R8_UInt 分配错配（10-22 不受光事故形态）");
                if (!bakedGI)
                    Fail(sb, ref fails, "UnityGBuffer.hlsl 丢失 kMaterialFlagUseBakedGI —— lightmap 场景会双重环境光");
            }

            // ② 分配端：GetFormat 必须 uint 化；DeferredLights 槽位检查保持 fork 布局。
            if (layerUtils != null)
            {
                bool uintFormats = layerUtils.Contains("R8_UInt") && layerUtils.Contains("R16_UInt") && layerUtils.Contains("R32_UInt");
                bool unormLeft = layerUtils.Contains("R8_UNorm");
                sb.AppendLine("  ②分配端: GetFormat uint三档=" + uintFormats + " 残留UNorm=" + unormLeft);
                if (!uintFormats || unormLeft)
                    Fail(sb, ref fails, "RenderingLayerUtils.GetFormat 未全面 uint 化（残留 UNorm 分配会与 uint 写出/读取错配）");
            }

            if (deferredLights != null)
            {
                bool hasSlice = deferredLights.Contains("GBufferRenderingLayers");
                bool hasFF = deferredLights.Contains("UseFramebufferFetch");
                bool hasName4 = deferredLights.Contains("\"_GBuffer4\"");
                sb.AppendLine("  ②分配端: GBufferRenderingLayers=" + hasSlice + " UseFramebufferFetch=" + hasFF + " _GBuffer4=" + hasName4);
                if (!hasSlice) Fail(sb, ref fails, "DeferredLights 缺少 GBufferRenderingLayers（渲染层切片）");
                if (!hasName4) Warn(sb, ref warns, "DeferredLights 里没找到 \"_GBuffer4\"，确认 MRT 命名是否变了");
            }

            // ③ 读取端：compute 必须以 typed uint4 读取渲染层，槽位宏仍取自 UnityGBuffer.hlsl。
            if (compute != null)
            {
                bool typed = compute.Contains("TYPED_TEXTURE2D_X(uint4, _GBuffer4)");
                bool macro = compute.Contains("GBUFFER_LIGHT_LAYERS");
                bool floatRead = compute.Contains("DecodeMeshRenderingLayer(renderingLayers");
                sb.AppendLine("  ③读取端: typed uint4=" + typed + " GBUFFER_LIGHT_LAYERS宏=" + macro + " 残留float解码=" + floatRead);
                if (!typed || !macro || floatRead)
                    Fail(sb, ref fails, "DeferredLighting.compute 渲染层读取未 uint 化 —— 与 ②R8_UInt/①uint 写出错配");
            }

            // ④ 宏定义与 fork 衍生登记。
            if (gbufferCommon != null)
            {
                bool hasIdx = gbufferCommon.Contains("GBUFFER_IDX_R_RENDERING_LAYERS");
                sb.AppendLine("  ④宏定义: GBUFFER_IDX_R_RENDERING_LAYERS=" + hasIdx);
                if (!hasIdx) Warn(sb, ref warns, "GBufferCommon.hlsl 缺少 GBUFFER_IDX_R_RENDERING_LAYERS");
            }

            if (gbufferOutput != null)
            {
                bool baked = gbufferOutput.Contains("kMaterialFlagUseBakedGI");
                bool uintSlot = gbufferOutput.Contains("DECL_OPT_GBUFFER_TARGET(uint, meshRenderingLayers");
                sb.AppendLine("  ④fork衍生: GBufferOutput UseBakedGI位=" + baked + " uint渲染层槽=" + uintSlot);
                if (!baked)
                    Fail(sb, ref fails, "GBufferOutput.hlsl 缺少 kMaterialFlagUseBakedGI 写入（fork 衍生标记）—— lightmap 场景双重环境光");
                if (!uintSlot)
                    Fail(sb, ref fails, "GBufferOutput.hlsl 渲染层槽不是 uint —— 与 ②R8_UInt 分配错配");
            }
            sb.AppendLine();

            // ---------- 2. 17.5 独有但被依赖的文件 ----------
            sb.AppendLine("[2] 被依赖的 17.5 独有文件");
            string[] required =
            {
                "Shaders/Utils/SurfaceType.hlsl",
                "ShaderLibrary/GBufferOutputFormat.hlsl",
                "ShaderLibrary/UnityGBuffer.deprecated.hlsl",
                "Editor/ShaderGraph/Targets/UniversalTerrainLitSubTarget.cs",
                "Editor/ShaderGraph/Targets/UniversalUISubTarget.cs",
            };
            foreach (var rel in required)
            {
                bool ok = File.Exists(PkgPath(rel));
                sb.AppendLine("  " + (ok ? "✔ " : "✘ ") + rel);
                if (!ok) Fail(sb, ref fails, "缺失被依赖文件: " + rel);
            }
            sb.AppendLine();

            // ---------- 3. Shader 编译错误 ----------
            sb.AppendLine("[3] Shader 编译错误");
            var shaders = new List<Shader>();

            string[] keyNames =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Complex Lit",
                "Tide/YGOMonster",
                "Tide/IconAlpha",
                "Tide/BattleTileFresnel",
                "Tide/BattleVortex",
            };
            foreach (var n in keyNames)
            {
                var sh = Shader.Find(n);
                if (sh != null) shaders.Add(sh);
                else Warn(sb, ref warns, "未找到 shader: " + n);
            }

            var guids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Art/Shaders" });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh != null && !shaders.Contains(sh)) shaders.Add(sh);
            }

            int errCount = 0;
            foreach (var sh in shaders)
            {
                int errs = CountShaderErrors(sh);
                if (errs > 0)
                {
                    errCount++;
                    sb.AppendLine("  ✘ " + sh.name + "  (errors=" + errs + ")");
                }
            }
            sb.AppendLine("  检查 shader 数: " + shaders.Count + "，报错: " + errCount);
            if (errCount > 0)
                Fail(sb, ref fails, errCount + " 个 shader 编译失败（先清 Console 重编；若仍存在，多半是跨版本 include/宏错配）");
            sb.AppendLine();

            // ---------- 汇总 ----------
            sb.AppendLine("=== 汇总 ===");
            sb.AppendLine("FAIL: " + fails + "   WARN: " + warns + "   => " + (fails == 0 ? "PASS" : "NOT PASS"));

            var report = sb.ToString();
            Debug.Log(report);

            try
            {
                var outDir = Path.Combine(ProjectRoot(), "Logs");
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "DbdrpContractCheck.txt"), report, Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("写报告失败: " + e.Message);
            }

            return fails == 0;
        }

        // ---------------- helpers ----------------

        /// <summary>shader 是否有编译错误（ShaderUtil.ShaderHasError，已在本机 Unity 6000.5 验证可用）</summary>
        static int CountShaderErrors(Shader sh)
        {
            try
            {
                return ShaderUtil.ShaderHasError(sh) ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        static string ProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        static string PkgPath(string rel)
        {
            return Path.Combine(ProjectRoot(), Pkg.Replace('/', Path.DirectorySeparatorChar), rel.Replace('/', Path.DirectorySeparatorChar));
        }

        static string Read(string rel)
        {
            var p = PkgPath(rel);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        static void Fail(StringBuilder sb, ref int fails, string msg)
        {
            fails++;
            sb.AppendLine("  [FAIL] " + msg);
        }

        static void Warn(StringBuilder sb, ref int warns, string msg)
        {
            warns++;
            sb.AppendLine("  [WARN] " + msg);
        }
    }
}
