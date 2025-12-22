using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSample.Utils
{ 
    public static partial class EnumUtils
    {
        //若需要包含 0 值，则将includeZero传入为真
        public static IEnumerable<T> GetFlags<T>(T value, bool includeZero = false) where T : Enum
        {
            // 获取该枚举类型所有定义的值
            foreach (T flag in Enum.GetValues(typeof(T)))
            {
                long flagValue = Convert.ToInt64(flag);

                // 处理0值
                if (flagValue == 0)
                {
                    if (includeZero && value.HasFlag(flag))
                        yield return flag;
                    continue;
                }

                // 检查该标志是否被设置
                if (value.HasFlag(flag))
                    yield return flag;
            }
        }

        public static bool ContainsAll(this Enum value, Enum flags)
        {
            long valueFlags = Convert.ToInt64(value);
            long requiredFlags = Convert.ToInt64(flags);
            return (valueFlags & requiredFlags) == requiredFlags;
        }

        public static bool ContainsAny(this Enum value, Enum flags)
        {
            long valueFlags = Convert.ToInt64(value);
            long requiredFlags = Convert.ToInt64(flags);
            return (valueFlags & requiredFlags) != 0;
        }
    }

}
