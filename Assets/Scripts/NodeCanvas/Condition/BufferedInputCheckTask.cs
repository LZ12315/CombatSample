using System;
using System.Collections.Generic;
using NodeCanvas.Framework;
using UnityEngine;
using ParadoxNotion.Design;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions
{
    /// <summary>
    /// 替代旧版 InputCheckTask：在最近 <see cref="waitTime"/> 秒内扫描 <see cref="Actor.logicInput"/> 输入缓冲，
    /// 是否命中序列化的按钮检测（与 <see cref="ButtonInputCheck"/> 规则一致）。
    /// </summary>
    [Name("Buffered Input Check")]
    [Category("Custom/Combat")]
    public class BufferedInputCheckTask : ConditionTask
    {
        [Serializable]
        public class ButtonCheckEntry
        {
            public ButtonPayload buttonInputCheck;
        }

        [Serializable]
        public class ButtonPayload
        {
            public Enums.InputButton requiredButtons;
            public Enums.ButtonState requiredState;
        }

        [Header("Settings")]
        public BBParameter<Actor> actor;
        public float waitTime = 0.2f;

        [Header("Properties")]
        public List<ButtonCheckEntry> inputChecks = new List<ButtonCheckEntry>();

        protected override bool OnCheck()
        {
            var actorValue = actor?.value;
            if (actorValue == null || actorValue.logicInput == null)
                return false;

            if (inputChecks == null || inputChecks.Count == 0)
                return false;

            var checks = new List<ButtonInputCheck>(inputChecks.Count);
            for (int i = 0; i < inputChecks.Count; i++)
            {
                var entry = inputChecks[i]?.buttonInputCheck;
                if (entry == null)
                    continue;
                checks.Add(new ButtonInputCheck
                {
                    requiredButtons = entry.requiredButtons,
                    requiredState = entry.requiredState
                });
            }

            if (checks.Count == 0)
                return false;

            float threshold = Time.time - Math.Max(0f, waitTime);
            var buffer = actorValue.logicInput.InputBuffer;
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                var bi = buffer[i];
                if (bi == null || bi.Data == null || bi.Timestamp < threshold)
                    continue;

                for (int c = 0; c < checks.Count; c++)
                {
                    if (checks[c].CheckInput(bi.Data))
                        return true;
                }
            }

            return false;
        }
    }
}
