using UnityEditor;
using UnityEngine;

namespace TanLuZhe.EditorTools
{
    /// <summary>
    /// Shared live readout for the tuning inspectors. Shows the running state under the normal
    /// inspector while Play Mode is active, so a slider can be dragged and the effect watched in
    /// the same window.
    /// </summary>
    internal static class LiveStateGui
    {
        public const string TuningHint =
            "调参方式：进入 Play Mode 后，上面带滑块的数值会立刻作用到角色 / 钩锁上。\n" +
            "退出 Play 后数值会还原 —— 想保留：右键组件标题 → Copy Component，退出 Play → Paste Component Values，再 Ctrl+S 保存场景。\n" +
            "永久生效：直接改 C# 里的字段初始值，或改 Assets/Editor/DemoSceneBuilder.cs 里对应的 SetField(...)。";

        public static bool Begin(Object target)
        {
            if (target == null) return false;

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(TuningHint, MessageType.Info);
                return false;
            }

            EditorGUILayout.LabelField("Live state (Play Mode)", EditorStyles.boldLabel);
            return true;
        }

        public static void End()
        {
            // Keep the readout animating while the game runs.
            if (Application.isPlaying) EditorWindow.focusedWindow?.Repaint();
        }

        public static void Row(string label, string value)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(label, value);
            }
        }

        public static void Row(string label, bool value)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle(label, value);
            }
        }

        public static void Row(string label, float value)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(label, value);
            }
        }

        public static void Row(string label, Vector2 value)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2Field(label, value);
            }
        }
    }

    [CustomEditor(typeof(PlayerController2D))]
    [CanEditMultipleObjects]
    public sealed class PlayerController2DInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlayerController2D controller = target as PlayerController2D;
            if (!LiveStateGui.Begin(controller)) return;

            LiveStateGui.Row("Grounded", controller.IsGrounded);
            LiveStateGui.Row("Wall sliding", controller.IsWallSliding);
            LiveStateGui.Row("Pulled by rope", controller.IsBeingPulled);
            LiveStateGui.Row("Control locked (input ignored)", controller.ControlLocked);
            LiveStateGui.Row("Gravity active", !controller.ControlLocked);
            LiveStateGui.Row("Velocity", controller.Velocity);
            LiveStateGui.Row("Speed (m/s)", controller.Velocity.magnitude);
            LiveStateGui.Row("Facing", controller.FacingSign);
            LiveStateGui.Row("Has control", controller.HasControl);

            LiveStateGui.End();
        }
    }

    [CustomEditor(typeof(GrappleHook2D))]
    [CanEditMultipleObjects]
    public sealed class GrappleHook2DInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GrappleHook2D hook = target as GrappleHook2D;
            if (!LiveStateGui.Begin(hook)) return;

            Vector2 origin = hook.OriginPosition;
            Vector2 anchor = hook.CurrentAnchorWorld;

            LiveStateGui.Row("State", hook.State.ToString());
            LiveStateGui.Row("Anchor type", hook.AnchorType.ToString());
            LiveStateGui.Row("Attached", hook.IsAttached);
            LiveStateGui.Row("Rope taut", hook.IsRopeTaut);
            LiveStateGui.Row("Rope length (m)", hook.RopeLength);
            LiveStateGui.Row("Pull speed (m/s)", hook.CurrentPullSpeed);
            LiveStateGui.Row("Distance to anchor", Vector2.Distance(origin, anchor));
            LiveStateGui.Row("Head position", hook.HeadPosition);
            LiveStateGui.Row("Release at (m)", hook.ReleaseDistance);
            LiveStateGui.Row("Pull accel (m/s^2)", hook.PullAcceleration);

            LiveStateGui.End();
        }
    }
}
