using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 单位动作 3D 按钮组（2026-10-26 定案：替退役的 uGUI 临时弹窗 BattleUnitActionPopup——
    /// 「不走临时 UI、也是 3D」）：点有可行动作的己方单位/角色格，在单位头顶（旧读数位，
    /// BattleFieldView.StatsAnchorOf 供锚）弹出竖直按钮行，只显可用项（攻击/守卫/发动）。
    /// 可用位图单源=BattleScreen.ReconcileTileButtons 的 _cellActions，开面板后每帧经
    /// SetBits 对账刷新（绑定格失位/全灭=自关；守卫窗翻转等场景自适应）——位图语义与弹窗时代
    /// 完全一致。形态=竖直 yaw 朝向面板（StatsBillboard 同数学·2026-10-26 竖直口径）+
    /// quad 底板（URP Lit 克隆分区格材质，HeroPillarMaterial 同模板同 _BaseColor 口径）+
    /// PZD TMP 文字（攻击/守卫/发动字形已验在图集）；quad 自带 MeshCollider=棋盘射线点选面，
    /// 命中名 unit-act-{kind}（BattleScreen.PollFieldPointer 前置路由，先于格点击/关面板）。
    /// 关闭=任意其他 3D 点击/执行动作/阶段回合变化/进入选择或选格/网络局（BattleScreen 驱动调 Close）。
    /// 纯 3D 件不经 uGUI；面板/按钮/克隆材质随 BattleScreen 离屏 Dispose 销毁。
    /// </summary>
    public sealed class BattleUnitActionButtons
    {
        public enum Kind { Attack, Guard, Activate }

        /// <summary>射线名前缀（BattleScreen 点击路由认名分发；件名=前缀+小写 kind）。</summary>
        public const string NamePrefix = "unit-act-";

        private GameObject _root;                                            // 面板根=可见性开关（YawBillboard 挂载）
        private readonly Dictionary<Kind, GameObject> _buttons = new Dictionary<Kind, GameObject>();
        private readonly Dictionary<Kind, Material> _mats = new Dictionary<Kind, Material>(); // 克隆材质随 Dispose 销毁（跨域 fake-null 防口）
        private readonly List<Kind> _activeBuf = new List<Kind>(3);          // SetBits 布局复用缓冲（免每帧 new）
        private Action<Kind> _handler;

        public bool IsOpen { get; private set; }
        /// <summary>绑定格（staleness 对账：位图失位=BattleScreen 调 Close）。</summary>
        public int BoundX { get; private set; } = -1;
        public int BoundZ { get; private set; } = -1;

        /// <summary>命中名是否本组件按钮（点击路由前置判定；面板关闭时件已失活不会被射线命中）。</summary>
        public static bool IsActionButton(string hitName)
            => !string.IsNullOrEmpty(hitName) && hitName.StartsWith(NamePrefix, StringComparison.Ordinal);

        /// <summary>开/换面板（重复 Show=换锚换内容，弹窗「换点单位即换弹窗」同语义）：anchor=单位
        /// 头顶锚；(x,z)=绑定格；onAction=点击回调（先自关再回调——执行动作即关闭）。</summary>
        public void Show(Vector3 anchor, int x, int z, bool attack, bool guard, bool activate, Action<Kind> onAction)
        {
            Ensure();
            _root.transform.position = anchor;
            BoundX = x;
            BoundZ = z;
            _handler = onAction;
            IsOpen = true;
            _root.gameObject.SetActive(true);
            SetBits(attack, guard, activate);
            Debug.Log($"[UnitActBtn] 开：格({x},{z})（攻击={attack} 守卫={guard} 发动={activate}）");
        }

        /// <summary>可用项对账刷新（BattleScreen 每帧喂当前位图）：居中重排布局；全灭=自关。</summary>
        public void SetBits(bool attack, bool guard, bool activate)
        {
            if (!IsOpen) return;
            _activeBuf.Clear();
            if (attack) _activeBuf.Add(Kind.Attack);
            if (guard) _activeBuf.Add(Kind.Guard);
            if (activate) _activeBuf.Add(Kind.Activate);
            if (_activeBuf.Count == 0)
            {
                Close();
                return;
            }
            const float spacing = 1.15f; // 按钮中距（底板 1.05 宽留 0.1 缝）
            for (int i = 0; i < _activeBuf.Count; i++)
            {
                var go = _buttons[_activeBuf[i]];
                go.transform.localPosition = new Vector3((i - (_activeBuf.Count - 1) * 0.5f) * spacing, 0f, 0f);
                go.SetActive(true);
            }
            foreach (var kv in _buttons)
                if (!_activeBuf.Contains(kv.Key)) kv.Value.SetActive(false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            BoundX = BoundZ = -1;
            if (_root != null) _root.gameObject.SetActive(false);
            Debug.Log("[UnitActBtn] 关");
        }

        /// <summary>点击命中按钮（hitName=unit-act-*）：先关再回调（执行动作即关闭，弹窗 Fire 同口径）。</summary>
        public void Fire(string hitName)
        {
            Kind k;
            if (hitName.EndsWith("attack", StringComparison.Ordinal)) k = Kind.Attack;
            else if (hitName.EndsWith("guard", StringComparison.Ordinal)) k = Kind.Guard;
            else if (hitName.EndsWith("activate", StringComparison.Ordinal)) k = Kind.Activate;
            else return;
            var h = _handler;
            Close();
            h?.Invoke(k);
        }

        /// <summary>离屏销毁（BattleScreen 退出调，防跨屏残留；面板+按钮+克隆材质整清）。</summary>
        public void Dispose()
        {
            IsOpen = false;
            BoundX = BoundZ = -1;
            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root);
                else Object.DestroyImmediate(_root);
                _root = null;
            }
            _buttons.Clear();
            foreach (var m in _mats.Values)
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
            _mats.Clear();
            _handler = null;
        }

        // ======================================== 内部 ========================================

        private void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("unit-actions");
            // 收进 Map 总根（BattleFieldView 同口径：运行时临时对象不堆积场景根目录）
            if (BattleStageDirector.MapRoot != null) _root.transform.SetParent(BattleStageDirector.MapRoot, true);
            _root.AddComponent<YawBillboard>();
            _root.gameObject.SetActive(false);
            _buttons[Kind.Attack] = CreateButton(Kind.Attack, "攻击", new Color(0.55f, 0.26f, 0.22f)); // 锈红族（阵营色同低饱和口径）
            _buttons[Kind.Guard] = CreateButton(Kind.Guard, "守卫", new Color(0.28f, 0.40f, 0.50f));    // 青蓝族
            _buttons[Kind.Activate] = CreateButton(Kind.Activate, "发动", new Color(0.60f, 0.47f, 0.22f)); // 金族
        }

        /// <summary>单枚按钮：quad 底板（自带 MeshCollider=点选面·名字路由）+前置 TMP 文字。
        /// 可读面=本地 -Z（quad/TMP 同侧），随面板 yaw 朝向恒对相机。</summary>
        private GameObject CreateButton(Kind kind, string label, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = NamePrefix + kind.ToString().ToLowerInvariant(); // unit-act-attack / guard / activate
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.05f, 0.55f, 1f);
            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var template = Tide.HotUpdate.HotUpdateAssets.Load<Material>(
                "BattleTile_Zone02", "Assets/Art/Materials/BattleTile_Zone02_P1Unit.mat");
            if (template != null)
            {
                var m = new Material(template) { name = "UnitActBtn_" + kind };
                m.SetColor("_BaseColor", tint); // URP Lit 主色（HeroPillarMaterial 同口）
                _mats[kind] = m;
                rend.sharedMaterial = m;
            }
            else
            {
                Debug.LogWarning("[UnitActBtn] 底板材质模板缺失（BattleTile_Zone02_P1Unit.mat）——按钮回落内置材质着色（真机需重跑资源打包）");
                rend.material.color = tint; // 内置默认材质实例（编辑器可见；真机缺包=粉红已告警，同角色柱兜底口径）
            }

            var txtGo = new GameObject("label");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, -0.02f); // 可读面 -Z——朝相机侧前置防 z-fight
            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = label;
            return go;
        }

        /// <summary>竖直 yaw 朝向（StatsBillboard 同数学·2026-10-26 竖直口径）：只按相机方位角回正
        /// yaw、俯仰恒 0——按钮行恒竖立可读、行轴恒屏幕水平；与模型层朝向解耦。</summary>
        private sealed class YawBillboard : MonoBehaviour
        {
            private void LateUpdate()
            {
                var cam = BattleStageDirector.WorldCam;
                if (cam == null) return;
                var flat = transform.position - cam.transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1e-6f) return;
                transform.rotation = Quaternion.LookRotation(flat.normalized);
            }
        }
    }
}
