using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Builds the two Animator Controllers for you: Tools > Build Character Animators.
/// Needs the animation files in Assets/Character/Animations named:
/// idle, walk_forward, walk_back, strafe_left, strafe_right, run_forward, run_back,
/// run_forward_left, run_forward_right, jump, death.
/// Creates Assets/Character/PlayerAnimator.controller and BotAnimator.controller.
public static class CharacterAnimatorBuilder
{
    const string AnimFolder = "Assets/Character/Animations";
    const string OutFolder = "Assets/Character";

    [MenuItem("Tools/Build Character Animators")]
    static void Build()
    {
        string[] names = { "idle", "walk_forward", "walk_back", "strafe_left", "strafe_right",
                           "run_forward", "run_back", "run_forward_left", "run_forward_right", "jump", "death" };

        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        bool missing = false;
        foreach (string n in names)
        {
            AnimationClip c = LoadClip(n);
            if (c == null)
            {
                Debug.LogError("CharacterAnimatorBuilder: couldn't find an animation file called '" + n + ".fbx' in " + AnimFolder +
                               ". Rename the file (select it, press F2) so it matches exactly.");
                missing = true;
            }
            clips[n] = c;
        }
        if (missing) return;

        BuildPlayer(clips);
        BuildBot(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CharacterAnimatorBuilder: created PlayerAnimator.controller and BotAnimator.controller in " + OutFolder);
    }

    static AnimationClip LoadClip(string fileName)
    {
        string path = AnimFolder + "/" + fileName + ".fbx";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    static void BuildPlayer(Dictionary<string, AnimationClip> c)
    {
        string path = OutFolder + "/PlayerAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // Locomotion: 2D blend. x = sideways (right is +), y = forward. Walk is about 0.6 of full run speed.
        BlendTree tree;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out tree);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveZ";
        tree.useAutomaticThresholds = false;
        tree.AddChild(c["idle"], new Vector2(0f, 0f));
        tree.AddChild(c["walk_forward"], new Vector2(0f, 0.6f));
        tree.AddChild(c["walk_back"], new Vector2(0f, -0.6f));
        tree.AddChild(c["strafe_left"], new Vector2(-0.6f, 0f));
        tree.AddChild(c["strafe_right"], new Vector2(0.6f, 0f));
        tree.AddChild(c["run_forward"], new Vector2(0f, 1f));
        tree.AddChild(c["run_back"], new Vector2(0f, -1f));
        tree.AddChild(c["run_forward_left"], new Vector2(-0.7f, 0.7f));
        tree.AddChild(c["run_forward_right"], new Vector2(0.7f, 0.7f));
        sm.defaultState = loco;

        // Jump / fall: plays while not grounded
        AnimatorState jump = sm.AddState("Jump");
        jump.motion = c["jump"];
        AnimatorStateTransition toJump = loco.AddTransition(jump);
        toJump.hasExitTime = false;
        toJump.duration = 0.1f;
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
        AnimatorStateTransition fromJump = jump.AddTransition(loco);
        fromJump.hasExitTime = false;
        fromJump.duration = 0.15f;
        fromJump.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        // Death: from any state
        AnimatorState death = sm.AddState("Death");
        death.motion = c["death"];
        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
        toDeath.hasExitTime = false;
        toDeath.duration = 0.1f;
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
    }

    static void BuildBot(Dictionary<string, AnimationClip> c)
    {
        string path = OutFolder + "/BotAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // Locomotion: 1D blend on the NavMeshAgent's speed (bots move at about 3.5)
        BlendTree tree;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(c["idle"], 0f);
        tree.AddChild(c["walk_forward"], 1.5f);
        tree.AddChild(c["run_forward"], 3.5f);
        sm.defaultState = loco;

        AnimatorState death = sm.AddState("Death");
        death.motion = c["death"];
        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
        toDeath.hasExitTime = false;
        toDeath.duration = 0.1f;
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
    }
}