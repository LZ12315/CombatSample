using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ActionSystem/ActionList")]
public class ActionAssetList : ScriptableObject
{
    [Header("配置")]
    [SerializeField, Tooltip("默认Action")]
    private ActionAsset defaultAction;
    [SerializeField, Tooltip("可以从任何Action中切换")]
    private List<ActionTransition> anyTransitions = new List<ActionTransition>();

    #region 属性封装

    public ActionAsset DefaultAction { get => defaultAction; }
    public IReadOnlyList<ActionTransition> AnyTransitions => anyTransitions.AsReadOnly();
    #endregion

}
