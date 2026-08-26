using System;
using System.Collections.Generic;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 可存档的效果图 —— 效果合成界面的产物。
    ///   header：效果元信息（DisplayName/Description/TriggerTiming/ActivationType 等），
    ///           复用现有 CardEffectData 结构，卡牌合成时可直接快照拷贝进卡牌。
    ///   steps ：有序步骤列表，每步是「原子效果」或「条件分支(then/else)」。
    ///
    /// 注：CardEffectData / EffectStepData 都是 CardCore 下的 [Serializable] 类，
    /// JsonUtility 可直接序列化；本类仅再包一层 name 便于按文件名存读。
    /// </summary>
    [Serializable]
    public class EffectGraphData
    {
        public string name;
        public CardEffectData header = new CardEffectData();
        public List<EffectStepData> steps = new List<EffectStepData>();

        public EffectGraphData() { }

        public EffectGraphData(string name)
        {
            this.name = name;
        }
    }
}
