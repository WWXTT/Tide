"""
共享 rollout 工具（quick_train_tcp / train_tide_complete 共用）。

核心约定：对局跨更新窗口延续。
之前的问题：每次 rollout 开头 env.reset() 把进行中的对局丢掉——128 步窗口远短于
一局对局（数百步），step_count 又随 reset 清零，500 步超时永不触发 → done 永远
观察不到 → episode 统计恒 0。修复后 (obs, rstate) 由调用方跨更新携带，只在
episode 结束时 reset。
"""

import numpy as np
import jax
from collections import namedtuple

from tide_features import init_rstate

# 完结对局统计（2026-11-02 训练管线加固扩容，原 3 元组 (ret, len, timeout)）：
#   ret         整局回报（双座 actor-centric 原始累计——自对弈下混两座次，仅作监控量）
#   len         步数
#   timeout     是否 Python 侧超时判负
#   shaping_ret 非终局奖励小计（λ·ΔΦ 塑形 + 无进展扣分；λ=0 时恒 0。
#               终局步奖励 = ret − shaping_ret，两分量拆分供「塑形在回报中的占比」统计）
#   winner      Unity 报的胜者座次（"0"=P1 / "1"=P2 / ""=平局或超时截断）
#   model_seat  vs 脚本口径的模型座次（自对弈 -1）——W/L/D 统计的视角映射用
#   theme       本局模型侧卡组主题 key（red/green/blue；v19 2026-11-20 绿套补盲：
#               绿专项统计「绿局平均局长」的归属键；取自 env.carry_ep_theme——
#               reset 响应 info.modelTheme / tide_env_tcp._last_model_theme 同源）
EpisodeStats = namedtuple("EpisodeStats", "ret len timeout shaping_ret winner model_seat theme behavior")
# behavior（2026-11-23 B4 用户口径）：本局模型侧行为计数，取自终局 info：
#   {"sac": 牺牲次数, "rec": 归根次数, "chain": 是否走通「先牺牲→后归根」, "agros": 14 费大哥落地次数,
#    "agros_turn": 首次落地全局回合号(0=从未)}。老协议无这些字段时为空 dict（下游按 0 处理）。


def _behavior_of(info):
    """2026-11-23 B4：终局 info → 本局模型行为计数（老协议无字段 → 空 dict，下游按 0 处理）。
    追加（同日晚）：绿成长 grow/land_t1/land_t3、红法术线 blood/bomb/combo。"""
    if not isinstance(info, dict) or ("mSac" not in info and "mAgros" not in info):
        return {}
    return {"sac": int(info.get("mSac", 0)), "rec": int(info.get("mRec", 0)),
            "chain": int(info.get("mChain", 0)), "agros": int(info.get("mAgros", 0)),
            "agros_turn": int(info.get("mAgrosTurn", 0)),
            "grow": int(info.get("mGrow", 0)),
            "land_t1": int(info.get("mLandT1", -1)), "land_t3": int(info.get("mLandT3", -1)),
            "blood": int(info.get("mBlood", 0)), "bomb": int(info.get("mBomb", 0)),
            "combo": int(info.get("mCombo", 0)),
            # 2026-11-23 考察项：攻击目标分类（safe=打已横置且无警戒→不吃反击）
            "atk_safe": int(info.get("mAtkSafe", 0)),
            "atk_risky": int(info.get("mAtkRisky", 0)),
            "atk_face": int(info.get("mAtkFace", 0)),
            "atk_any": int(info.get("mAtkAny", 0))}


def rollout_single_env(agent_apply, params, env, obs, rstate, num_steps, rng, rnn_type="gru",
                       pending=(0.0, 0, 0.0), log_every_steps=0, seat=None,
                       prune_loop_steps=True):
    """从 (obs, rstate) 续跑 num_steps；episode 结束自动 reset 并重置 rstate。

    Args:
        obs / rstate: 上一窗口末尾的观测与 RNN 状态（首次由调用方 env.reset() 获得）
        pending: (return, length, shaping_ret) 上一窗口末尾未完结 episode 的累计
                 （本窗口接着累计；第三项 2026-11-02 加，跨窗口塑形分量不丢）
        log_every_steps: >0 时每 N 步打印一次窗口内进度（心跳）
        seat: 传入 obs 的行动方座次（info.toPlay，0/1）——自对弈 GAE 座次修正用；
              None（旧协议/调用方未传）则整窗不记录座次，退回单代理口径
        prune_loop_steps: L3-A 循环空转步剪枝（2026-11-18 循环治理 §5.4）——回合内
              出现 info.loopPruned 时，该回合在本窗口内的全部步 learner 置 0
              （回合级回溯：空转步不进 PPO 批次，与「对手步剔除」同一条通道；
              GAE 仍在全序列上算，只在入批时过滤行）。--no-prune 回退旧口径。

    对手池快照对手（2026-11-02 C 期）：env 带 opponent_params/learner_seat 属性
    （OpponentPoolEnv，checkpoints/fsp 来源）时，非学习者座次的决策由快照参数驱动——
    - 学习者链 RNN 恒由**当前参数**逐步前向（含对手步），values/logits 口径与纯
      selfplay 单链完全同构 → 座次修正 GAE 的全序列 critic 递推不缺值；
    - 对手链 RNN 只在对手步由快照参数前向（对手自己的递归上下文），仅供其选动作；
    - 对手步 logprob/value 记 0、learner 记 False——PPO 批次经 data["learner"]
      过滤后只含学习者步（对手行为的 ratio 无意义，必须剔除）。
    对手链跨窗口暂存在 env.carry_rstate_opp（对局跨窗口延续不丢上下文）。

    Returns:
        data: dict{obs, rstates, actions, logprobs, rewards, dones, values,
                   seats(可 None), next_seat(可 None), learner}
              rstates = 每步前向实际使用的 incoming rstate（更新期重算 logprob 用，
              与采样口径一致——不能在更新期用重置态重算，否则 ratio 错位）
              seats = 每步行动方座次；next_seat = 窗口末携带 obs 的行动方座次
              （bootstrap 符号用）；中途 toPlay 缺失则两者置 None
              learner = 每步是否学习者决策（无快照对手时恒 True）
        next_obs, next_rstate: 窗口末尾状态（交给下一窗口续跑）
        rng
        episodes: 本窗口内完结 episode 的 [EpisodeStats, ...]（含跨窗口累计）
        pending: (return, length, shaping_ret) 窗口末仍未完结的累计
    """
    obs_list = []
    rstates_list = []
    actions_list = []
    logprobs_list = []
    rewards_list = []
    dones_list = []
    values_list = []
    learner_list = []
    seats_list = [] if seat is not None else None
    episodes = []
    ep_ret, ep_len, ep_shaping = pending
    # 对手链跨窗口续跑（env 是 OpponentPoolEnv 且本局有快照对手时非 None）
    rstate_opp = getattr(env, "carry_rstate_opp", None)

    # ---- L3-A 循环空转步剪枝状态（2026-11-18 循环治理 §5.4）----
    # 回合边界用 info.turn 跳变检测：cur_turn = 本步行动时的回合号（上一步响应的
    # info.turn；窗口首步未知为 None——首步边界不判，仅窗口边界 ≤1 回合段的归段误差，
    # 剪枝口径可接受）。turn_start = 当前回合在本窗口的首步下标；turn_loop_pruned =
    # 该回合内出现过 info.loopPruned（L1 cycle/stall 或 L2 budget 收口）——回合结束
    # （turn 跳变 / done / 窗口尾）时把 [turn_start, end) 的 learner 全部置 0（回合级
    # 回溯：含触发步之前的空转步）。旧协议（无 loopPruned 键）恒 False，零行为差。
    cur_turn = None
    turn_start = 0
    turn_loop_pruned = False
    loop_events = 0      # L1 收口次数（loopReason= cycle/stall）
    loop_budget = 0      # L2 收口次数（loopReason= budget）
    loop_pruned_steps = 0  # 本窗口被置 learner=0 的步数
    max_turn_actions = 0   # 单回合步数峰值（info.turnActions）

    for step in range(num_steps):
        # 转为 batch（h_actions_ 为 None 的树节点保持 None）
        obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)

        # 对手池归属：本局有快照对手且行动方不是学习者 → 对手步
        opp_params = getattr(env, "opponent_params", None)
        opp_active = opp_params is not None
        if opp_active and seat is None:
            # 座次是快照对手归类的唯一依据，缺失=协议回归——静默降级会把对手步
            # 混进学习者批次（错误 logprob 污染 PPO），必须响亮失败
            raise RuntimeError(
                "对手池快照对手在局但 info.toPlay 座次缺失——无法归属决策点，拒绝继续 rollout")
        opp_step = opp_active and seat != env.learner_seat

        # 学习者链前向（记录 incoming rstate 供更新期同口径重算；对手步也走——
        # critic 值保证 GAE 全序列递推不缺位，链动态与纯 selfplay 单链同构）
        rstates_list.append(rstate)
        rstate, logits, value = agent_apply(params, rstate, obs_batch)
        logits = logits[0]
        value = value[0]
        values_list.append(float(value))

        if opp_step:
            # 对手链前向（快照参数，仅供选动作）+ 其分布采样（FSP「平均行为」=
            # 按旧策略的随机性打，不做贪心）。窗口中途接管的快照对手局对手链冷启动。
            if rstate_opp is None:
                rstate_opp = init_rstate(rnn_type)
            rstate_opp, opp_logits, _ = agent_apply(opp_params, rstate_opp, obs_batch)
            rng, subkey = jax.random.split(rng)
            action = jax.random.categorical(subkey, opp_logits[0])
            action_np = int(action)
            logprobs_list.append(0.0)   # 对手步不进 PPO 批次（占位对齐，过滤后剔除）
            learner_list.append(False)
        else:
            # 采样动作（logits 已在模型内屏蔽非法动作：-1e9）
            rng, subkey = jax.random.split(rng)
            action = jax.random.categorical(subkey, logits)
            action_np = int(action)
            logprob = jax.nn.log_softmax(logits)[action]
            logprobs_list.append(float(logprob))
            learner_list.append(True)

        # 记录
        obs_list.append(obs)
        actions_list.append(action_np)
        if seats_list is not None:
            seats_list.append(seat)

        # 执行
        next_obs, reward, done, truncated, info = env.step(action_np)

        rewards_list.append(reward)
        dones_list.append(done or truncated)
        ep_ret += reward
        ep_len += 1
        if not (done or truncated):
            ep_shaping += reward  # 非终局步奖励 = 塑形（λ=0 时恒 0）

        # ---- L3-A：收口步外显消费 + 回合级回溯剪枝（§5.4）----
        info_turn = info.get("turn")
        info_turn = info_turn if isinstance(info_turn, int) else None
        ta = info.get("turnActions")
        if isinstance(ta, int) and ta > max_turn_actions:
            max_turn_actions = ta
        if info.get("loopPruned", False):
            turn_loop_pruned = True
            if str(info.get("loopReason", "") or "") == "budget":
                loop_budget += 1
            else:
                loop_events += 1
        # 回合结束 = done（含 truncated）或本步后回合号跳变（对手回合整段自动打完也命中）
        turn_ended = bool(done or truncated) or (
            cur_turn is not None and info_turn is not None and info_turn != cur_turn)
        if turn_ended:
            if turn_loop_pruned and prune_loop_steps:
                for i in range(turn_start, step + 1):
                    if i < len(learner_list) and learner_list[i]:
                        learner_list[i] = False
                        loop_pruned_steps += 1
            turn_loop_pruned = False
            turn_start = step + 1

        obs = next_obs
        if done or truncated:
            episodes.append(EpisodeStats(
                ep_ret, ep_len, bool(info.get("timeout", False)),
                ep_shaping, str(info.get("winner", "") or ""), int(info.get("modelSeat", -1)),
                getattr(env, "carry_ep_theme", ""), _behavior_of(info)))
            ep_ret, ep_len, ep_shaping = 0.0, 0, 0.0
            obs, info = env.reset()
            rstate = init_rstate(rnn_type)
            # v19 绿套补盲：新局模型侧卡组主题随 reset 更新（tide_env_tcp._last_model_theme
            # = 本局实际下发的 key；"all" 口径每局随机抽一套——绿局长归属按此键统计）
            try:
                env.carry_ep_theme = str(getattr(env, "_last_model_theme", "") or "")
            except AttributeError:
                pass  # 非 setattr 宿主（理论不可达，防御）
            # 新局对手来源已由壳采样（opponent_params 刷新）：有快照对手才重开对手链
            rstate_opp = init_rstate(rnn_type) if getattr(env, "opponent_params", None) is not None else None

        # 下一步行动时的回合号（done 后 info 已是 reset 响应——新局首步由此定）
        if not (done or truncated):
            cur_turn = info_turn
        else:
            cur_turn = None

        # 行动方座次推进：step/reset 响应的 info.toPlay 即下一决策点行动方
        # （done 后 info 来自 reset 响应）。中途缺失 → 整窗退回单代理口径。
        if seats_list is not None:
            to_play = info.get("toPlay")
            seat = int(to_play) if isinstance(to_play, int) and to_play >= 0 else None
            if seat is None:
                seats_list = None

        if log_every_steps and (step + 1) % log_every_steps == 0:
            print(f"  [rollout] step {step + 1}/{num_steps} episodes={len(episodes)}", flush=True)

    # 对手链暂存到壳上（对局跨更新窗口延续时下窗口续用；纯 selfplay 恒 None）
    try:
        env.carry_rstate_opp = rstate_opp
    except AttributeError:
        pass  # 非 setattr 宿主（理论不可达，防御）

    # 窗口尾兜底（L3-A）：回合仍开着但已触发过收口——本窗口内该回合已走步同样剪除
    #（触发已确证该回合空转；该回合在上窗口的前段步当时已消费、无法回溯，口径可接受）
    if turn_loop_pruned and prune_loop_steps:
        for i in range(turn_start, num_steps):
            if i < len(learner_list) and learner_list[i]:
                learner_list[i] = False
                loop_pruned_steps += 1

    data = {
        "obs": obs_list,
        "rstates": rstates_list,
        "actions": np.array(actions_list, dtype=np.int32),
        "logprobs": np.array(logprobs_list, dtype=np.float32),
        "rewards": np.array(rewards_list, dtype=np.float32),
        "dones": np.array(dones_list, dtype=np.bool_),
        "values": np.array(values_list, dtype=np.float32),
        "seats": np.array(seats_list, dtype=np.int32) if seats_list is not None else None,
        "next_seat": seat if seats_list is not None else None,
        "learner": np.array(learner_list, dtype=np.bool_),
        # L3-A 循环治理指标（§5.4/§5.6，train 侧聚合进 stats.jsonl）：
        #   events/budget = 收口次数（loopReason 分桶）/ pruned = 被剪步数 / steps = 窗口步数
        #   max_turn_actions = 单回合步数峰值（info.turnActions 的 max，健康值 ≤60）
        "loop_stats": {
            "events": loop_events,
            "budget": loop_budget,
            "pruned": loop_pruned_steps,
            "steps": num_steps,
            "max_turn_actions": max_turn_actions,
        },
    }
    return data, obs, rstate, rng, episodes, (ep_ret, ep_len, ep_shaping)


def bootstrap_next_value(agent_apply, params, obs, rstate):
    """对窗口末尾的当前观测做价值 bootstrap（GAE 的 next_value）。

    注意用「携带的 rstate + 当前 obs」——用新初始化 rstate 或窗口内最后一个
    pre-action obs 都是口径漂移（done 尾步的 bootstrap 会被 GAE 的 non_terminal 权重清零）。
    """
    obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)
    _, _, value = agent_apply(params, rstate, obs_batch)
    return float(value[0])


def compute_advantages_simple(values, rewards, dones, next_value, gamma, gae_lambda,
                              seats=None, next_seat=None, use_upgo=False):
    """简化版 GAE（单环境序列）。

    seats 给出每步行动方座次（info.toPlay）时启用零和自对弈修正（2026-09-22）：
    自对弈双方决策在同一条轨迹交替，obs / reward / critic 输出全是「当前行动方」视角。
    零和势能 Φ_opp(s) ≡ −Φ_me(s) ⟹ 对手行动步的价值/奖励相对本方反号——旧口径把
    两个座次的流当单代理回溯，advantage 系统性有偏。符号变换法修正：
      sign[0]=+1，行动方换人处翻号（episode 边界重置 +1）；
      r̂=sign·r、V̂=sign·V、bootstrap 按 next_seat 是否换手定号，跑标准 GAE 得 Â；
      换回原空间 A=sign·Â、R=sign·(Â+V̂)=A+V——等价于对每个行动方计算
      「己方交替折扣回报」（对手步的奖励在本方回报里带负号）的 GAE。
    终局 ±1 是 actor-centric（胜者行动步 +1 / 败者行动步 −1），与该约定自洽。
    seats=None 走旧单代理口径（simpleai 训练 / quick 实验兼容；simpleai 模式下
    所有决策点都是模型座次，符号永不翻转，两条路径数值一致）。

    v2 响应窗口（2026-09-30 C 期）：自对弈下决策点可在对方回合出现（守卫/响应出牌/
    响应期自愿触发），toPlay 序列变为 回合方↔响应方 交错。本修正只依赖「逐步座次 +
    换人翻号」，与决策点归属哪个回合无关——Main 多动作（同座次连续步）不翻号、
    响应交错（换人）翻号，数学上仍是零和势能的符号变换，**无需为此改算法**。
    前提：Unity 侧 info.toPlay 必须报真实决策座次（契约第 3 节），reward 归属
    该座次（driver 现行 actor-centric 口径）。

    use_upgo（2026-09-29，仅座次修正口径生效）：UPGO「跟随赢家」优势项
    （ygo-agent truncated_gae_sep 默认开）——在符号修正空间反向递推
      U_t = r̂_t + γ·non_terminal·max(V̂_{t+1}, U_{t+1})
    （对手应手点取 max：对手若走最优，本步的实际延续价值不低于其估计），
    A += U_t − V̂_t。critic 目标 R 保持纯 GAE 回报不变（ygo 同口径：
    targets 先于 upgo 项算出）。
    """
    if seats is None:
        advantages = []
        gae = 0.0
        for t in reversed(range(len(rewards))):
            if t == len(rewards) - 1:
                next_val = next_value
            else:
                next_val = values[t + 1]

            non_terminal = 1.0 - float(dones[t])
            delta = rewards[t] + gamma * next_val * non_terminal - values[t]
            gae = delta + gamma * gae_lambda * non_terminal * gae
            advantages.insert(0, gae)

        advantages = np.array(advantages, dtype=np.float32)
        returns = advantages + values
        return advantages, returns

    n = len(rewards)
    values = np.asarray(values, dtype=np.float32)
    rewards = np.asarray(rewards, dtype=np.float32)

    # 符号序列：新 episode 从 +1 起步，行动方换人翻号（同座次连续动作不翻——
    # 一个回合内多个决策步属同一行动方）
    sign = np.ones(n, dtype=np.float32)
    for t in range(1, n):
        if dones[t - 1]:
            sign[t] = 1.0
        elif seats[t] != seats[t - 1]:
            sign[t] = -sign[t - 1]
        else:
            sign[t] = sign[t - 1]

    # bootstrap 符号：窗口末携带 obs 的行动方（next_seat）相对最后一步是否换手
    flip = next_seat is not None and next_seat >= 0 and next_seat != seats[n - 1]
    nv_hat = (-sign[n - 1] if flip else sign[n - 1]) * next_value

    r_hat = sign * rewards
    v_hat = sign * values

    adv_hat = []
    gae = 0.0
    for t in reversed(range(n)):
        next_val = nv_hat if t == n - 1 else v_hat[t + 1]
        non_terminal = 1.0 - float(dones[t])
        delta = r_hat[t] + gamma * next_val * non_terminal - v_hat[t]
        gae = delta + gamma * gae_lambda * non_terminal * gae
        adv_hat.insert(0, gae)

    adv_hat = np.array(adv_hat, dtype=np.float32)
    advantages = sign * adv_hat
    returns = advantages + values

    # UPGO 项（符号修正空间反向递推，见 docstring；critic 目标 returns 不动）
    if use_upgo:
        upgo_delta = np.empty(n, dtype=np.float32)
        u = nv_hat  # 窗口末 bootstrap（已按换手定号）
        for t in reversed(range(n)):
            next_val = nv_hat if t == n - 1 else v_hat[t + 1]
            non_terminal = 1.0 - float(dones[t])
            u = r_hat[t] + gamma * non_terminal * max(next_val, u)
            upgo_delta[t] = u - v_hat[t]
        advantages = advantages + sign * upgo_delta

    return advantages, returns
