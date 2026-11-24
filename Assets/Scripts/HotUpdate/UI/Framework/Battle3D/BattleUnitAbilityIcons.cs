using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 单位头顶常驻能力图标簇（2026-11-23 用户定案：替退役的点击弹出文字钮组 BattleUnitActionButtons
    /// 与格面平贴状态图标两代）：攻/发动/守卫三图标**固定**钉在单位头顶（wrapper 子件·随单位位移/演出
    /// 自动跟随，YawBillboard 竖直朝相机），三槽固定位不复排——
    /// · **存在**（能力在身：卡面攻击资格/有主动效果或待发奖励/卡面守卫资格）=显示；
    /// · **可用**（此刻可执行）=原色图标+彩色外圈高亮（rim=同图放大 1.22 垫底）+点击碰撞体开；
    /// · **不可用**（横置/非主阶段/守卫窗未开等）=图标灰、圈隐、碰撞体关（点击穿透到格面原路由）。
    /// 命中名 unit-act-{kind}-{runtimeId}（hero=unit-act-{kind}-hero 哨兵）——BattleScreen 点击路由
    /// 认名分发（TryUnitActionIcon→OnUnitActionButton 既有处理器）。状态单源=BattleScreen
    /// ReconcileTileButtons 每帧推（己方）/BattleFieldView.ReconcileUnit 对账推（对手=引擎可用真值）。
    /// 纯 3D 件不经 uGUI；随 wrapper/字段 Dispose 销毁。sprite 缺失=整簇 null 降级（图标不显示不炸）。
    /// </summary>
    public sealed class BattleUnitAbilityIcons
    {
        public enum Kind { Attack, Guard, Activate }

        /// <summary>射线名前缀（与退役文字钮组同前缀——BattleScreen 点击路由口径不变）。</summary>
        public const string NamePrefix = "unit-act-";

        /// <summary>点击路由名解析：unit-act-{kind}-{rid}（rid=数字 runtimeId 或 "hero" 哨兵）。</summary>
        public static bool TryParseHit(string hitName, out Kind kind, out uint runtimeId, out bool isHero)
        {
            kind = Kind.Attack;
            runtimeId = 0;
            isHero = false;
            if (string.IsNullOrEmpty(hitName) || !hitName.StartsWith(NamePrefix, StringComparison.Ordinal))
                return false;
            var parts = hitName.Substring(NamePrefix.Length).Split('-');
            if (parts.Length != 2) return false;
            switch (parts[0])
            {
                case "attack": kind = Kind.Attack; break;
                case "guard": kind = Kind.Guard; break;
                case "activate": kind = Kind.Activate; break;
                default: return false;
            }
            if (parts[1] == "hero") { isHero = true; return true; }
            return uint.TryParse(parts[1], out runtimeId);
        }

        // sprite（与 ACard 卡面 Ac/Dc 同图；FX=主动效果 2026-11-20 SD-UI-Kit 生成 主动.png）——
        // 2026-11-23 起本类单源（原 BattleFieldView 格面图标同图同加载口退役）
        private static Sprite _acSprite, _fxSprite, _dcSprite;
        private static bool _spritesTried;

        private static void EnsureSprites()
        {
            if (_spritesTried) return;
            _spritesTried = true;
            _acSprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>("攻击", "Assets/Art/Icons/攻击.png");
            _fxSprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>("主动", "Assets/Art/Icons/主动.png");
            _dcSprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>("守卫", "Assets/Art/Icons/守卫.png");
            if (_acSprite == null || _fxSprite == null || _dcSprite == null)
                Debug.LogWarning("[UnitAbilityIcons] 能力图标缺失（Assets/Art/Icons/攻击.png·主动.png·守卫.png）"
                    + "——头顶图标缺件降级不显示");
        }

        private const float IconWorldSize = 0.34f;      // 图标边长（格面时代同值）
        private const float SlotSpacing = 0.45f;        // 三槽间距（固定位：攻左/发动中/守右）
        private const float RimScale = 1.22f;           // 外圈高亮=同图放大垫底（格面时代同值）
        private static readonly Color UnlitGray = new Color(0.45f, 0.45f, 0.45f); // 不可用=灰

        private readonly struct Slot
        {
            public readonly GameObject Go;          // 显隐=存在
            public readonly SpriteRenderer Icon;    // 颜色=可用原色/不可用灰
            public readonly GameObject Rim;         // 外圈高亮（可用才显）
            public readonly Collider Col;           // 点击面（可用才开）

            public Slot(GameObject go, Sprite sprite, float worldSize, Color rimColor, string hitName)
            {
                Go = go;
                go.name = hitName;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 10;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                float span = Mathf.Max(sprite.bounds.size.x, 0.01f);
                go.transform.localScale = Vector3.one * (worldSize / span);

                var rimGo = new GameObject("rim");
                rimGo.transform.SetParent(go.transform, false);
                rimGo.transform.localScale = Vector3.one * RimScale;
                var rim = rimGo.AddComponent<SpriteRenderer>();
                rim.sprite = sprite;
                rim.color = rimColor;
                rim.sortingOrder = 9;
                rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rim.receiveShadows = false;
                Rim = rimGo;

                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(1.15f, 1.15f, 0.08f); // 局部系（图标 GO 已按 sprite span 反缩）
                col.enabled = false;
                Col = col;
                Icon = sr;
                go.SetActive(false);
            }
        }

        private Slot? _attack, _activate, _guard;   // null=sprite 缺失降级（整槽不建）
        private bool _visible = true;               // 死亡随读数灭（SetVisible）

        /// <summary>是否已建（sprite 缺失降级=恒 false，调用方零成本直返）。</summary>
        public bool Built => _root != null;

        private GameObject _root;

        /// <summary>建簇（惰性——首次状态推送或视图对账时调）：parent=单位 wrapper，localY=头顶锚高；
        /// idTag=命中名尾段（单位=runtimeId 数字串 / 角色="hero" 哨兵）；clickable=false=纯状态显示
        ///（对手单位——碰撞体恒关）。</summary>
        public void Ensure(Transform parent, float localY, string idTag, bool clickable)
        {
            if (_root != null || (_spritesTried && _acSprite == null)) return;
            EnsureSprites();
            if (_acSprite == null) return; // 缺图降级：整簇不建（Built=false）

            _root = new GameObject("ability-icons-" + idTag);
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = new Vector3(0f, localY, 0f);
            _root.AddComponent<YawBillboard>();
            BuildSlots(idTag);
            _clickable = clickable;
            ApplyLitToCollider();
        }

        /// <summary>世界位建簇（角色柱用——柱体带非均匀缩放不宜做父，外挂 Map 总根钉世界位；
        /// 角色不位移，落位一次即可）。收进 BattleStageDirector.MapRoot（运行时临时对象不堆场景根）。</summary>
        public void EnsureWorld(Vector3 worldPos, string idTag, bool clickable)
        {
            if (_root != null || (_spritesTried && _acSprite == null)) return;
            EnsureSprites();
            if (_acSprite == null) return;

            _root = new GameObject("ability-icons-" + idTag);
            if (BattleStageDirector.MapRoot != null) _root.transform.SetParent(BattleStageDirector.MapRoot, true);
            _root.transform.position = worldPos;
            _root.AddComponent<YawBillboard>();
            BuildSlots(idTag);
            _clickable = clickable;
            ApplyLitToCollider();
        }

        /// <summary>三槽构建（攻左/发动中/守右固定位——状态切换不跳位）。</summary>
        private void BuildSlots(string idTag)
        {
            _attack = new Slot(new GameObject("icon"), _acSprite, IconWorldSize, new Color(0.95f, 0.28f, 0.30f),
                NamePrefix + "attack-" + idTag);
            _activate = new Slot(new GameObject("icon"), _fxSprite, IconWorldSize, new Color(0.30f, 0.58f, 0.95f),
                NamePrefix + "activate-" + idTag);
            _guard = new Slot(new GameObject("icon"), _dcSprite, IconWorldSize, new Color(0.32f, 0.85f, 0.42f),
                NamePrefix + "guard-" + idTag);
            // 三槽固定位（攻左/发动中/守右）——不复排（状态切换不跳位，2026-11-23 用户定案「固定」口径）
            _attack.Value.Go.transform.SetParent(_root.transform, false);
            _attack.Value.Go.transform.localPosition = new Vector3(-SlotSpacing, 0f, 0f);
            _activate.Value.Go.transform.SetParent(_root.transform, false);
            _activate.Value.Go.transform.localPosition = Vector3.zero;
            _guard.Value.Go.transform.SetParent(_root.transform, false);
            _guard.Value.Go.transform.localPosition = new Vector3(SlotSpacing, 0f, 0f);
        }

        private bool _clickable;

        /// <summary>碰撞体总闸（对手单位纯状态=false；点击穿透到格面原路由）。</summary>
        public void SetColliderClickable(bool clickable)
        {
            _clickable = clickable;
            ApplyLitToCollider();
        }

        // 最近一次状态（碰撞体总闸翻转时按位重放——lit 且 clickable 才开）
        private bool _aLit, _fLit, _dLit;

        /// <summary>状态推送（存在/可用六位——调用方每帧或对账喂）。</summary>
        public void SetStates(bool aPresent, bool aLit, bool fPresent, bool fLit, bool dPresent, bool dLit)
        {
            _aLit = aLit;
            _fLit = fLit;
            _dLit = dLit;
            if (_root == null) return;
            ApplySlot(_attack, aPresent, aLit);
            ApplySlot(_activate, fPresent, fLit);
            ApplySlot(_guard, dPresent, dLit);
            ApplyLitToCollider(); // 点击面随可用态开合（灰=点击穿透到格面原路由）
        }

        private static void ApplySlot(Slot? slot, bool present, bool lit)
        {
            if (slot == null) return;
            var s = slot.Value;
            if (s.Go.activeSelf != present) s.Go.SetActive(present);
            if (!present) return; // 隐槽不涂色（恢复显时下轮推送重涂）
            s.Icon.color = lit ? Color.white : UnlitGray;
            if (s.Rim.activeSelf != lit) s.Rim.SetActive(lit);
        }

        private void ApplyLitToCollider()
        {
            if (_root == null) return;
            SetCol(_attack, _aLit);
            SetCol(_activate, _fLit);
            SetCol(_guard, _dLit);
        }

        private void SetCol(Slot? slot, bool lit)
        {
            if (slot == null) return;
            bool on = lit && _clickable;
            if (slot.Value.Col.enabled != on) slot.Value.Col.enabled = on;
        }

        /// <summary>死亡随读数灭（SetStatsVisible 同拍）；复活由下一次状态推送复原。</summary>
        public void SetVisible(bool on)
        {
            _visible = on;
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
        }

        /// <summary>销毁（字段 Dispose/Retire 口；wrapper 子件随单位销毁，这里兜显式清）。</summary>
        public void Destroy()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root);
                else Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        /// <summary>竖直 yaw 朝向（StatsBillboard 同数学·2026-10-26 竖直口径）：只按相机方位角回正
        /// yaw、俯仰恒 0——图标行恒竖立可读；与模型层朝向解耦。</summary>
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
