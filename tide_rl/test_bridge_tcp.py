"""
快速测试 TCP 桥接（推荐）。

使用步骤：
1. 打开 Unity 编辑器，加载 Tide 项目
2. Unity 菜单：Tools > AI > 启动训练服务器 (TCP 9999)
3. 运行此脚本：python tide_rl/test_bridge_tcp.py
"""

import sys
from pathlib import Path

# Setup paths
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from tide_env_tcp import TideEnvTcp


def test_tcp_bridge():
    print("=" * 60)
    print("测试 Unity TCP 桥接")
    print("=" * 60)
    print()
    print("确保 Unity 编辑器已打开，并执行了：")
    print("  Tools > AI > 启动训练服务器 (TCP 9999)")
    print()

    # 创建环境
    print("正在连接到 Unity...")
    try:
        env = TideEnvTcp(host="localhost", port=9999)
        print("[✓] 环境创建成功")
    except Exception as e:
        print(f"[✗] 连接失败: {e}")
        return False

    # 测试 reset
    print()
    print("测试 reset()...")
    try:
        obs, info = env.reset()
        print("[✓] Reset 成功")
        print(f"  观测键: {list(obs.keys())}")
        print(f"  Cards shape: {obs['cards_'].shape}")
        print(f"  Global shape: {obs['global_'].shape}")
        print(f"  Actions shape: {obs['actions_'].shape}")
        print(f"  Legal actions: {(~obs['a_mask']).sum()}")
        print(f"  Info: {info}")
    except Exception as e:
        print(f"[✗] Reset 失败: {e}")
        import traceback
        traceback.print_exc()
        env.close()
        return False

    # 测试 step
    print()
    print("测试 step()（执行 10 步）...")
    try:
        for i in range(10):
            # 找第一个合法动作
            legal_actions = [j for j, mask in enumerate(obs['a_mask']) if not mask]
            if not legal_actions:
                print(f"  步 {i+1}: 无合法动作")
                break

            action = legal_actions[0]
            obs, reward, done, truncated, info = env.step(action)

            print(f"  步 {i+1}: 动作={action}, 奖励={reward:.4f}, done={done}, winner={info.get('winner', 'N/A')}")

            if done:
                print(f"[✓] 对局结束！胜者: {info.get('winner', '?')}, 原因: {info.get('reason', '?')}")
                break

        print("[✓] Step 测试成功")
    except Exception as e:
        print(f"[✗] Step 失败: {e}")
        import traceback
        traceback.print_exc()
        env.close()
        return False

    # 测试第二局（验证 reset 复用连接）
    print()
    print("测试第二局 reset()（复用连接）...")
    try:
        obs, info = env.reset()
        print("[✓] 第二局 Reset 成功")
        print(f"  Legal actions: {(~obs['a_mask']).sum()}")
    except Exception as e:
        print(f"[✗] 第二局 Reset 失败: {e}")
        env.close()
        return False

    # 清理
    print()
    print("关闭环境...")
    env.close()
    print("[✓] 环境已关闭")

    print()
    print("=" * 60)
    print("✓ TCP 桥接测试通过！")
    print("=" * 60)

    return True


if __name__ == "__main__":
    success = test_tcp_bridge()
    sys.exit(0 if success else 1)
