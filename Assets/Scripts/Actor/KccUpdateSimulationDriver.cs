using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Drives KCC simulation in Update to align with Timeline/Animator gameplay timing.
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class KccUpdateSimulationDriver : MonoBehaviour
{
    private static KccUpdateSimulationDriver _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("KccUpdateSimulationDriver");
        _instance = go.AddComponent<KccUpdateSimulationDriver>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        KinematicCharacterSystem.EnsureCreation();
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f)
            return;

        KinematicCharacterSystem.Simulate(
            Time.deltaTime,
            KinematicCharacterSystem.CharacterMotors,
            KinematicCharacterSystem.PhysicsMovers);
    }
}
