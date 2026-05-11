using KinematicCharacterController;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键将当前场景中所有 Actor 从 Unity CharacterController 迁移到 KCC。
/// 菜单: Tools > KCC Migration > Migrate Current Scene
/// </summary>
public static class KccMigrationTool
{
    [MenuItem("Tools/KCC Migration/Migrate Current Scene")]
    public static void MigrateCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        int migrated = 0;
        foreach (var root in roots)
        {
            foreach (var actor in root.GetComponentsInChildren<Actor>(true))
            {
                MigrateActor(actor);
                migrated++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[KCC Migration] 完成，处理了 {migrated} 个 Actor。请保存场景。");
    }

    private static void MigrateActor(Actor actor)
    {
        GameObject go = actor.gameObject;

        // 1. 保存旧 CC 参数。CC 的 skinWidth 会扩大有效碰撞半径，CapsuleCollider 没有这个参数，
        //    所以把 skinWidth 加到 radius 上以保持等效碰撞体积。
        var oldCC = go.GetComponent<CharacterController>();
        float radius = 0.5f;
        float height = 2f;
        float yOffset = 1f;
        if (oldCC != null)
        {
            radius = oldCC.radius + oldCC.skinWidth;
            height = oldCC.height;
            yOffset = oldCC.center.y;
        }

        // 2. 移除 CharacterController 和 CharacterControllerRigidbodyPush
        if (oldCC != null)
            Undo.DestroyObjectImmediate(oldCC);

        var push = go.GetComponent("CharacterControllerRigidbodyPush");
        if (push != null)
            Undo.DestroyObjectImmediate(push);

        // 3. 添加/配置 CapsuleCollider（KCC 要求）
        var capsule = go.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = Undo.AddComponent<CapsuleCollider>(go);
        }
        capsule.radius = radius;
        capsule.height = height;
        capsule.center = new Vector3(0, yOffset, 0);
        capsule.direction = 1; // Y-axis

        // 4. 添加 KinematicCharacterMotor
        var motor = go.GetComponent<KinematicCharacterMotor>();
        if (motor == null)
        {
            motor = Undo.AddComponent<KinematicCharacterMotor>(go);
        }

        // 5. 添加 ActorMotor
        var actorMotor = go.GetComponent<ActorMotor>();
        if (actorMotor == null)
        {
            actorMotor = Undo.AddComponent<ActorMotor>(go);
        }

        // 6. 更新 Actor 引用（kccMotor 已移除，KCC 由 ActorMotor 自持）
        var actorObj = new SerializedObject(actor);
        actorObj.FindProperty("actorMotor").objectReferenceValue = actorMotor;
        actorObj.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
    }
}
