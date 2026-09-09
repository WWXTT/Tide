"""
Quick test to verify Unity headless bridge is working.
"""

import os
import sys
from pathlib import Path

# Setup paths
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))
sys.path.insert(0, str(project_root / "ygo-agent-main"))

from tide_env import make_tide_env

def test_bridge():
    print("=" * 60)
    print("Testing Unity Headless Bridge")
    print("=" * 60)
    print()

    # Get Unity path（未设环境变量时 TideEnv 自动发现：Hub 扫描匹配工程版本）
    unity_path = os.environ.get("UNITY_PATH")
    if unity_path and not Path(unity_path).exists():
        print(f"[ERROR] UNITY_PATH 指向的 Unity 不存在: {unity_path}")
        return False

    print(f"[OK] Unity: {unity_path or 'auto-detect'}")
    print()

    # Create environment
    print("Creating TideEnv...")
    try:
        env = make_tide_env(
            unity_path=unity_path,
            project_path=str(project_root),
            reward_lambda=0.02,
        )
        print("[OK] Environment created")
    except Exception as e:
        print(f"[ERROR] Failed to create environment: {e}")
        return False

    # Test reset
    print()
    print("Testing reset()...")
    try:
        obs, info = env.reset()
        print("[OK] Reset successful")
        print(f"  Observation keys: {list(obs.keys())}")
        print(f"  Cards shape: {obs['cards_'].shape}")
        print(f"  Global shape: {obs['global_'].shape}")
        print(f"  Actions shape: {obs['actions_'].shape}")
        print(f"  Legal actions: {(~obs['a_mask']).sum()}")
    except Exception as e:
        print(f"[ERROR] Reset failed: {e}")
        env.close()
        return False

    # Test step
    print()
    print("Testing step()...")
    try:
        # Find first legal action
        legal_actions = [i for i, mask in enumerate(obs['a_mask']) if not mask]
        if not legal_actions:
            print("[ERROR] No legal actions available")
            env.close()
            return False

        action = legal_actions[0]
        print(f"  Taking action {action} (first legal action)")

        obs, reward, done, truncated, info = env.step(action)
        print("[OK] Step successful")
        print(f"  Reward: {reward}")
        print(f"  Done: {done}")
        print(f"  Winner: {info.get('winner', 'N/A')}")
    except Exception as e:
        print(f"[ERROR] Step failed: {e}")
        env.close()
        return False

    # Cleanup
    print()
    print("Closing environment...")
    env.close()
    print("[OK] Environment closed")

    print()
    print("=" * 60)
    print("✓ Bridge test passed!")
    print("=" * 60)

    return True

if __name__ == "__main__":
    success = test_bridge()
    sys.exit(0 if success else 1)
