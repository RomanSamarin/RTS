using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SiegeLadderAnimationEditor
{
    [MenuItem("Tools/RTS/Setup Ladder Animations For Selected Unit")]
    private static void Setup()
    {
        var selected = Selection.activeGameObject;
        var unit = selected != null ? selected.GetComponentInParent<Unit>() : null;
        if (unit == null) { Debug.LogWarning("Выберите корень солдата или его модель."); return; }
        var animator = unit.GetComponentInChildren<Animator>(true);
        var original = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        if (original == null) { Debug.LogWarning("Нужен Animator с обычным AnimatorController; OverrideController настройте вручную."); return; }
        var driver = unit.GetComponent<SiegeLadderAnimation>() ?? Undo.AddComponent<SiegeLadderAnimation>(unit.gameObject);
        Undo.RecordObject(driver, "Setup ladder animation clips");
        driver.animator = animator;
        if (driver.pickupClip == null) driver.pickupClip = ClipAt("Assets/Kevin Iglesias/Human Animations/Animations/Male/Work/Carry/HumanM@Carry01_PickUp01.fbx");
        if (driver.installationClip == null)
        {
            foreach (var guid in AssetDatabase.FindAssets("HumanM@HammeringTable01_R"))
            {
                var clip = ClipAt(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && clip.name.Contains("Loop")) { driver.installationClip = clip; break; }
            }
        }
        const string folder = "Assets/_Game/LadderControllers";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game", "LadderControllers");
        string source = AssetDatabase.GetAssetPath(original);
        AnimatorController controller = original;
        // Keep the controller used by other professions intact.
        if (!source.StartsWith(folder + "/"))
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + original.name + "_Ladders.controller");
            if (!AssetDatabase.CopyAsset(source, path)) { Debug.LogError("Не удалось скопировать AnimatorController."); return; }
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Undo.RecordObject(animator, "Use ladder animation controller"); animator.runtimeAnimatorController = controller;
        }
        AddBool(controller, driver.pickupParameter); AddBool(controller, driver.carryParameter);
        AddBool(controller, driver.installParameter); AddBool(controller, driver.climbParameter);
        if (!controller.layers.Any(layer => layer.name == driver.animationLayer)) controller.AddLayer(driver.animationLayer);
        var layers = controller.layers;
        var ladderLayer = layers.First(layer => layer.name == driver.animationLayer);
        ladderLayer.defaultWeight = 0; ladderLayer.blendingMode = AnimatorLayerBlendingMode.Override; ladderLayer.avatarMask = null;
        controller.layers = layers;
        var machine = ladderLayer.stateMachine;
        if (machine.defaultState == null) machine.defaultState = machine.AddState("LadderInactive");
        var installer = unit.GetComponent<SiegeLadderInstaller>();
        State(machine, "SiegeLadder_Pickup", driver.pickupParameter, driver.pickupClip,
            driver.pickupClip != null ? driver.pickupClip.length / (installer != null ? Mathf.Max(.1f, installer.pickupSeconds) : 1.2f) : 1);
        State(machine, "SiegeLadder_Install", driver.installParameter, driver.installationClip, 1);
        State(machine, "SiegeLadder_Climb", driver.climbParameter, driver.climbClip, 1);
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(animator); EditorUtility.SetDirty(driver);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
        AssetDatabase.SaveAssets();
        Debug.Log("[Лестницы] Параметры и состояния настроены. Для Carry можно добавить верхний слой рук поверх ходьбы.", unit);
        if (driver.climbClip == null) Debug.LogWarning("[Лестницы] Клип лазания в проекте не найден. Назначьте Climb Clip в SiegeLadderAnimation и повторите команду. До этого подъём перемещает солдата без движения конечностей.", unit);
    }
    private static AnimationClip ClipAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview"));
    private static void AddBool(AnimatorController controller, string parameter)
    {
        if (!string.IsNullOrEmpty(parameter) && !controller.parameters.Any(p => p.name == parameter))
            controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
    }
    private static void State(AnimatorStateMachine machine, string name, string parameter, AnimationClip clip, float speed)
    {
        if (clip == null || string.IsNullOrEmpty(parameter)) return;
        var state = machine.states.Select(child => child.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
        state.motion = clip; state.speed = speed;
        if (!machine.anyStateTransitions.Any(t => t.destinationState == state))
        {
            var enter = machine.AddAnyStateTransition(state); enter.hasExitTime = false; enter.duration = .1f;
            enter.canTransitionToSelf = false; enter.AddCondition(AnimatorConditionMode.If, 0, parameter);
        }
        if (machine.defaultState != null && !state.transitions.Any(t => t.destinationState == machine.defaultState))
        {
            var exit = state.AddTransition(machine.defaultState); exit.hasExitTime = false; exit.duration = .1f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);
        }
    }
}
