using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 解决方案的原理说明
///这种方式通过** Curiously Recurring Template Pattern(奇异递归模板模式)**来解决协变问题：

///基类中定义双重泛型参数：
///FSMNode<T, TNode>使每个节点类型都能明确知道自己的子类类型

///将子类自身类型作为第二个参数传递：
///Transition<T> : FSMNode<T, Transition<T>> 建立了类型约束链

///运行时类型安全检查：
///通过在基类中约束TNode必须派生自FSMNode<T, TNode>，确保获取的克隆类型合法
/// </summary>

public abstract class FSMNode<T, TNode> : ScriptableObject
where TNode : FSMNode<T, TNode>
{
    protected T _owner;

    public virtual void OnFSMInit()
    {

    }

    public virtual void OnStateEnter(T owner)
    {
        _owner = owner;
    }

    public virtual void OnStateExit()
    {
        _owner = default;
    }

}
