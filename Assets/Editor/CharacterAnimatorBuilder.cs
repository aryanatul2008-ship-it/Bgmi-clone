using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Builds the two Animator Controllers for you: Tools > Build Character Animators.
/// Safe to run again whenever you add clips: it clears and refills the existing controllers,
/// so the Animator components that already use them keep working.
///
/// Required files in Assets/Character/Animations (exact names):
///   idle, walk_forward, walk_back, strafe_left, strafe_right,
///   run_forward, run_back, run_forward_left, run_forward_right,
///   jump, death, crouch_idle, crouch_walk_forward
/// Optional (a better crouch, used only if ALL three exist):
///   crouch_walk_back, crouch_walk_left, crouch_walk_right
///
/// It also fixes the import settings of ALL the clips (loop, and "Bake Into Pose" so the body height
/// isn't lost when Apply Root Motion is off, which is what made the character sink into the ground).
public static class CharacterAnimatorBuilder
{
    const string AnimFolder = "Assets/Character/Animations";
    const string OutFolder = "Assets/Character";
    const float CrouchWalkSpeed = 0.31f; // crouch speed (2.5) divided by sprint speed (8), as sent by PlayerAnimator

    [MenuItem("Tools/Build Character Animators")]
    static void Build()
    {
        string[] required = { "idle", "walk_forward", "walk_back", "strafe_left", "strafe_right",
                              "run_forward", "run_back", "run_forward_left", "run_forward_right",
                              "jump", "death", "crouch_idle", "crouch_walk_forward" };
        string[] optional = { "crouch_walk_back", "crouch_walk_left", "crouch_walk_right" };

        // Make EVERY clip behave before loading them: humanoid rig, loop on or off, and "Bake Into Pose" so the
        // body height stays in the pose (without it, Apply Root Motion = off makes the character sink into the ground).
        string[] looping = { "idle", "walk_forward", "walk_back", "strafe_left", "strafe_right",
                             "run_forward", "run_back", "run_forward_left", "run_forward_right",
                             "crouch_idle", "crouch_walk_forward" };
        foreach (string n in looping) Prepare(n, true);
        foreach (string n in optional) Prepare(n, true);
        Prepare("jump", false);
        Prepare("death", false);

        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        bool missing = false;
        foreach (string n in required)
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
        foreach (string n in optional) clips[n] = LoadClip(n); // may be null

        BuildPlayer(clips);
        BuildBot(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CharacterAnimatorBuilder: rebuilt PlayerAnimator.controller and BotAnimator.controller in " + OutFolder);
    }

    // ------------------------------------------------------------ helpers

    static AnimationClip LoadClip(string fileName)
    {
        string path = AnimFolder + "/" + fileName + ".fbx";
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    // Humanoid rig, loop on/off, and root transform baked into the pose. Only reimports if something changes.
    static void Prepare(string fileName, bool loop)
    {
        string path = AnimFolder + "/" + fileName + ".fbx";
        ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;

        bool changed = false;
        if (imp.animationType != ModelImporterAnimationType.Human)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;

        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime != loop || c.loopPose != loop ||
                !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ ||
                !c.keepOriginalOrientation || !c.keepOriginalPositionY)
            {
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;        // Bake Into Pose: rotation
                c.keepOriginalOrientation = true; // Based Upon: Original
                c.lockRootHeightY = true;         // Bake Into Pose: position Y
                c.keepOriginalPositionY = true;   // Based Upon: Original
                c.lockRootPositionXZ = true;      // Bake Into Pose: position XZ
                changed = true;
            }
        }

        if (changed)
        {
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    // Reuse the existing controller asset (so the model's Animator keeps its reference) and wipe its contents.
    static AnimatorController GetCleanController(string path)
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return AnimatorController.CreateAnimatorControllerAtPath(path);

        for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (AnimatorTransition t in sm.entryTransitions) sm.RemoveEntryTransition(t);
        return ctrl;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, float duration, params (string name, AnimatorConditionMode mode)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        foreach (var cond in conditions) t.AddCondition(cond.mode, 0, cond.name);
    }

    // ------------------------------------------------------------ player

    static void BuildPlayer(Dictionary<string, AnimationClip> c)
    {
        AnimatorController ctrl = GetCleanController(OutFolder + "/PlayerAnimator.controller");

        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ctrl.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // Standing locomotion: 2D blend. x = sideways (right is +), y = forward. Walk is about 0.6 of full run speed.
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

        // Crouch locomotion
        BlendTree crouchTree;
        AnimatorState crouch = ctrl.CreateBlendTreeInController("Crouch", out crouchTree);
        crouchTree.useAutomaticThresholds = false;
        bool allDirections = c["crouch_walk_back"] != null && c["crouch_walk_left"] != null && c["crouch_walk_right"] != null;
        if (allDirections)
        {
            crouchTree.blendType = BlendTreeType.FreeformDirectional2D;
            crouchTree.blendParameter = "MoveX";
            crouchTree.blendParameterY = "MoveZ";
            crouchTree.AddChild(c["crouch_idle"], new Vector2(0f, 0f));
            crouchTree.AddChild(c["crouch_walk_forward"], new Vector2(0f, CrouchWalkSpeed));
            crouchTree.AddChild(c["crouch_walk_back"], new Vector2(0f, -CrouchWalkSpeed));
            crouchTree.AddChild(c["crouch_walk_left"], new Vector2(-CrouchWalkSpeed, 0f));
            crouchTree.AddChild(c["crouch_walk_right"], new Vector2(CrouchWalkSpeed, 0f));
        }
        else
        {
            // Only a forward crouch walk: blend idle -> walk by overall speed, whatever the direction
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = "MoveSpeed";
            crouchTree.AddChild(c["crouch_idle"], 0f);
            crouchTree.AddChild(c["crouch_walk_forward"], CrouchWalkSpeed);
        }

        // Jump / fall: plays while not grounded
        AnimatorState jump = sm.AddState("Jump");
        jump.motion = c["jump"];

        AddTransition(loco, crouch, 0.15f, ("Crouching", AnimatorConditionMode.If));
        AddTransition(crouch, loco, 0.15f, ("Crouching", AnimatorConditionMode.IfNot));
        AddTransition(loco, jump, 0.1f, ("Grounded", AnimatorConditionMode.IfNot));
        AddTransition(crouch, jump, 0.1f, ("Grounded", AnimatorConditionMode.IfNot));
        AddTransition(jump, loco, 0.15f, ("Grounded", AnimatorConditionMode.If), ("Crouching", AnimatorConditionMode.IfNot));
        AddTransition(jump, crouch, 0.15f, ("Grounded", AnimatorConditionMode.If), ("Crouching", AnimatorConditionMode.If));

        // Death: from any state
        AnimatorState death = sm.AddState("Death");
        death.motion = c["death"];
        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
        toDeath.hasExitTime = false;
        toDeath.duration = 0.1f;
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
    }

    // ------------------------------------------------------------ bots

    static void BuildBot(Dictionary<string, AnimationClip> c)
    {
        AnimatorController ctrl = GetCleanController(OutFolder + "/BotAnimator.controller");

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