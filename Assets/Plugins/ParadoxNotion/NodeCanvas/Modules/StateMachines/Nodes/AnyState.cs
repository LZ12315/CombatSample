using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.StateMachines
{

    [Name("Any State")]
    [Description("The transitions of this node will be constantly checked. If any becomes true, that transition will take place. This is not a state.")]
    [Color("b3ff7f")]
    public class AnyState : FSMNode, IUpdatable
    {

        [Tooltip("If enabled, a transition to an already running state will not happen.")]
        public bool dontRetriggerRunningStates = false;

        public override string name { //yei for caps
            get { return "FROM ANY STATE"; }
        }

        public override int maxInConnections { get { return 0; } }
        public override int maxOutConnections { get { return -1; } }
        public override bool allowAsPrime { get { return false; } }

        public override void OnGraphStarted() {
            for ( var i = 0; i < outConnections.Count; i++ ) {
                ( outConnections[i] as FSMConnection ).EnableCondition(graphAgent, graphBlackboard);
            }
        }

        public override void OnGraphStoped() {
            for ( var i = 0; i < outConnections.Count; i++ ) {
                ( outConnections[i] as FSMConnection ).DisableCondition();
            }
        }

        void IUpdatable.Update() {

            if ( outConnections.Count == 0 ) {
                return;
            }

            status = Status.Running;

            for ( var i = 0; i < outConnections.Count; i++ ) {

                var connection = (FSMConnection)outConnections[i];
                var condition = connection.condition;

                if ( !connection.isActive || condition == null ) {
                    continue;
                }

                if ( dontRetriggerRunningStates ) {
                    if ( FSM.currentState == (FSMState)connection.targetNode && FSM.currentState.status == Status.Running ) {
                        continue;
                    }
                }

                if ( condition.Check(graphAgent, graphBlackboard) ) {

                    // 新增：此节点切换到新节点时，调用所有Connection的OnDisable和OnEnable
                    for (var j = 0; j < outConnections.Count; j++)
                        (outConnections[j] as FSMConnection).DisableCondition();

                    FSM.EnterState((FSMState)connection.targetNode, connection.transitionCallMode);
                    connection.status = Status.Success; //editor vis

                    for (var j = 0; j < outConnections.Count; j++)
                        (outConnections[j] as FSMConnection).EnableCondition(graphAgent, graphBlackboard);

                    return;
                }

                connection.status = Status.Failure; //editor vis
            }
        }

        ///----------------------------------------------------------------------------------------------
        ///---------------------------------------UNITY EDITOR-------------------------------------------
#if UNITY_EDITOR

        protected override void OnNodeGUI() {
            base.OnNodeGUI();
            if ( dontRetriggerRunningStates ) {
                UnityEngine.GUILayout.Label("<b>[NO RETRIGGER]</b>");
            }
        }

        public override string GetConnectionInfo(int index) {
            if ( ( outConnections[index] as FSMConnection ).condition == null ) {
                return "* Never Triggered *";
            }
            return null;
        }

        protected override void OnNodeInspectorGUI() {
            EditorUtils.CoolLabel("Transitions");
            if ( outConnections.Count == 0 ) {
                UnityEditor.EditorGUILayout.HelpBox("No Transition", UnityEditor.MessageType.None);
            }

            var anyNullCondition = false;
            EditorUtils.ReorderableList(outConnections, (i, picked) =>
            {
                var connection = (FSMConnection)outConnections[i];
                GUILayout.BeginHorizontal("box");
                if ( connection.condition != null ) {
                    GUILayout.Label(connection.condition.summaryInfo, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                } else {
                    GUILayout.Label("OnFinish (This is never triggered)", GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                    anyNullCondition = true;
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("► '" + connection.targetNode.name + "'");
                GUILayout.EndHorizontal();
            });

            EditorUtils.BoldSeparator();

            if ( anyNullCondition ) {
                UnityEditor.EditorGUILayout.HelpBox("This is not a state and as such it never finish, thus OnFinish transitions are never called.\nPlease add a condition in all transitions of this node.", UnityEditor.MessageType.Warning);
            }

            dontRetriggerRunningStates = UnityEditor.EditorGUILayout.ToggleLeft("Don't Retrigger Running States", dontRetriggerRunningStates);
        }
#endif

    }
}