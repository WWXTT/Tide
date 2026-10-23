using System;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 开场掷硬币演出（2026-10-19 掷币演出挪战场定案·方案B：代码动画网格本体+现成 VFX 装饰）。
    ///
    /// 硬币=运行时拼装（圆柱银边+上下双色圆盘——蓝面=我方先手/红面=对方先手（v3 对调），落定浮字明示），
    /// 结果先定后演（Random.value&lt;0.5 与旧 PreMatch 同源 50/50），自旋终角按结果钉死胜方面朝上；
    /// 落地装饰=Impact 爆点（金）+ world 浮字（BattleFieldView.FloatTextAsync 公开转发）。
    /// 纯演出件：发生在引擎开局前，不接投影器/DeferOrRun/视觉门；物件不带 Collider（不扰棋盘射线）。
    /// 动画风格对齐 BattleBoardAssemble：realtimeSinceStartup 计时+缓动+终态钉死，skipRequested
    /// 跳过=直接落定终态（效果照发，结果不变）。
    /// </summary>
    public static class BattleCoinToss
    {
        private const string PillarMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Zone02_P1Unit.mat";

        private const float RestY = 0.6f;      // 落定悬停高度（板心上方，不落格面）
        private const float ApexY = 3.3f;      // 抛起顶点
        private const float FlightSeconds = 1.35f;
        private const float BounceSeconds = 0.3f;
        private const float HoldSeconds = 1.0f; // 落定驻留（≥浮字 0.9s——浮字挂硬币下，父亡即殁）
        private const float TiltDegrees = 32f;  // 落定后向镜头微倾（-z 俯拍机位可正面读面）
        private const int SpinTurns = 4;        // 自旋整圈数（终角=整圈+按结果 0/180°）

        private static readonly Color SelfFaceColor = new Color(0.30f, 0.55f, 1.00f);  // 蓝面=我方先手（2026-10-23 v3 对调）
        private static readonly Color OppFaceColor = new Color(0.92f, 0.28f, 0.24f);   // 红面=对方先手
        private static readonly Color EdgeColor = new Color(0.82f, 0.84f, 0.9f);       // 银边
        private static readonly Color ImpactGold = new Color(1f, 0.82f, 0.35f);

        private static Material _pillarTemplate; // URP Lit 模板（着色克隆用）
        private static bool _triedLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _pillarTemplate = null; _triedLoad = false; }

        /// <summary>掷币演出（~2.7s+驻留）：restPos=落定点（棋盘中心上方），返回 playerFirst
        ///（true=我方先手——红面朝上）。拆台/异常兜底=不演直接返回随机结果。</summary>
        public static async UniTask<bool> PlayAsync(Vector3 restPos, Func<bool> skipRequested)
        {
            bool playerFirst = UnityEngine.Random.value < 0.5f; // 结果先定后演（与旧 PreMatch 同源 50/50）
            var rest = restPos + Vector3.up * RestY;
            GameObject coin = null;
            try
            {
                coin = BuildCoin(rest);
                float targetSpin = SpinTurns * 360f + (playerFirst ? 0f : 180f);
                float t0 = Time.realtimeSinceStartup;

                // ---- 飞行段：抛物线升降 + 绕世界 X 轴自旋（快起慢收） ----
                while (Time.realtimeSinceStartup - t0 < FlightSeconds)
                {
                    if (coin == null) return playerFirst; // 拆台
                    if (skipRequested != null && skipRequested()) break;
                    float k = (Time.realtimeSinceStartup - t0) / FlightSeconds;
                    float y = Mathf.Lerp(RestY, ApexY, 4f * k * (1f - k)); // 0→顶点→0 抛物线
                    float spin = targetSpin * (1f - Mathf.Pow(1f - k, 2.4f)); // easeOut：起始快终段慢
                    coin.transform.position = rest - Vector3.up * RestY + Vector3.up * y;
                    coin.transform.rotation = Quaternion.AngleAxis(spin, Vector3.right);
                    await UniTask.Yield();
                }

                // ---- 落定段：小弹跳 + 微倾贴镜头可读面 ----
                var finalRot = Quaternion.Euler(-TiltDegrees, 0f, 0f)
                    * Quaternion.AngleAxis(targetSpin, Vector3.right);
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < BounceSeconds)
                {
                    if (coin == null) return playerFirst;
                    if (skipRequested != null && skipRequested()) break;
                    float k = (Time.realtimeSinceStartup - t0) / BounceSeconds;
                    float bounce = Mathf.Sin(k * Mathf.PI) * 0.35f; // 一次小弹跳
                    coin.transform.position = rest + Vector3.up * bounce;
                    coin.transform.rotation = Quaternion.Slerp(
                        Quaternion.AngleAxis(targetSpin, Vector3.right), finalRot, k);
                    await UniTask.Yield();
                }
                coin.transform.position = rest; // 终态钉死（消浮点残差）
                coin.transform.rotation = finalRot;

                // ---- 落地装饰：金爆点 + 结果浮字（先色后文：面已可读，浮字点题） ----
                BattleEffectFx.PlayImpact(rest - Vector3.up * 0.45f, ImpactGold);
                BattleFieldView.ShowFloatText(coin.transform, 1.1f,
                    playerFirst ? "我方先手！" : "对方先手·我方 +5 护甲",
                    playerFirst ? SelfFaceColor : OppFaceColor);
                Debug.Log($"[BattleCoinToss] 掷币落定：playerFirst={playerFirst}（蓝面我方/红面对方·v3 对调）");
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < HoldSeconds)
                {
                    if (coin == null) break; // 提前拆台：浮字随父同殁，直接收
                    await UniTask.Yield();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleCoinToss] 演出异常（兜底=直接返回结果）：{ex.Message}");
            }
            finally
            {
                if (coin != null) Object.Destroy(coin);
            }
            return playerFirst;
        }

        /// <summary>硬币物件：圆柱银边（Ø0.9×厚0.1）+ 上下色面盘（Ø0.8 **圆盘**，2026-10-23 v3：
        /// Quad 方片退役，压扁圆柱同径双面；+Y 蓝=我方 / −Y 红=对方）。
        /// 全件无 Collider（不扰棋盘射线）；挂 MapRoot 随战场生命周期。</summary>
        private static GameObject BuildCoin(Vector3 at)
        {
            var go = new GameObject("coin-toss");
            if (BattleStageDirector.MapRoot != null) go.transform.SetParent(BattleStageDirector.MapRoot, true);
            go.transform.position = at;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            KillCollider(body);
            body.name = "body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
            SetBodyMaterial(body, EdgeColor);

            MakeFaceDisc(go.transform, SelfFaceColor, +1); // 上盘=蓝（我方先手面·v3 对调）
            MakeFaceDisc(go.transform, OppFaceColor, -1);  // 下盘=红（对方先手面）
            return go;
        }

        /// <summary>面盘：压扁圆柱圆盘（Ø0.8×厚≈1mm，轴向 Y 天然对齐圆柱端面——无需转轴；
        /// v3 前 Quad 方片已退役）。两 face 同径。</summary>
        private static void MakeFaceDisc(Transform parent, Color color, int side)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            KillCollider(disc);
            disc.name = side > 0 ? "face-self" : "face-opp";
            disc.transform.SetParent(parent, false);
            disc.transform.localRotation = Quaternion.identity; // 圆柱轴向=Y，与币体同轴
            disc.transform.localPosition = new Vector3(0f, side * 0.053f, 0f);
            disc.transform.localScale = new Vector3(0.8f, 0.0008f, 0.8f); // 压成圆片（厚1.6mm）
            SetBodyMaterial(disc, color);
        }

        /// <summary>着色：分区格材质模板（URP Lit·HeroPillarMaterial 同源）克隆 + _BaseColor——
        /// 内置默认材质不随包分发的坑（出包粉红）同口径规避。</summary>
        private static void SetBodyMaterial(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (!_triedLoad)
            {
                _triedLoad = true;
                _pillarTemplate = HotUpdateAssets.Load<Material>("BattleTile_Zone02", PillarMatPath);
                if (_pillarTemplate == null)
                    Debug.LogWarning("[BattleCoinToss] 材质模板缺失（BattleTile_Zone02_P1Unit.mat）——硬币走内置材质（真机可能粉）");
            }
            if (_pillarTemplate != null)
            {
                var m = new Material(_pillarTemplate) { name = "Coin_" + go.name };
                m.SetColor("_BaseColor", color);
                rend.sharedMaterial = m; // 随硬币 GO 销毁（每次演出新建，无跨局缓存）
            }
            else rend.material.color = color; // 编辑器兜底
        }

        private static void KillCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}
