using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomEditor(typeof(BpmMarkerTrack))]
    [CanEditMultipleObjects]
    internal sealed class BpmMarkerTrackInspector : Editor
    {
        // Fields

        private SerializedProperty _bpm;
        private SerializedProperty _beatsPerBar;
        private SerializedProperty _beatUnit;
        private SerializedProperty _beatOriginSeconds;
        private SerializedProperty _beatColor;
        private SerializedProperty _barColor;


        // Methods

        private void OnEnable()
        {
            _bpm = serializedObject.FindProperty(nameof(_bpm));
            _beatsPerBar = serializedObject.FindProperty(nameof(_beatsPerBar));
            _beatUnit = serializedObject.FindProperty(nameof(_beatUnit));
            _beatOriginSeconds = serializedObject.FindProperty(nameof(_beatOriginSeconds));
            _beatColor = serializedObject.FindProperty(nameof(_beatColor));
            _barColor = serializedObject.FindProperty(nameof(_barColor));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_bpm, new GUIContent("BPM"));
            EditorGUILayout.PropertyField(_beatsPerBar, new GUIContent("Beats Per Bar"));
            EditorGUILayout.PropertyField(_beatUnit, new GUIContent("Beat Unit"));
            EditorGUILayout.PropertyField(_beatOriginSeconds, new GUIContent("Beat Origin (s)"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_beatColor, new GUIContent("Beat Color"));
            EditorGUILayout.PropertyField(_barColor, new GUIContent("Bar Color"));

            var changed = serializedObject.ApplyModifiedProperties();
            if (!serializedObject.isEditingMultipleObjects)
            {
                if (target is BpmMarkerTrack track && !track.TryGetGridSettings(out _, out var error))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (changed)
            {
                BpmMarkerTrackGrid.RequestRepaint();
                TimelineEditor.GetWindow()?.Repaint();
            }
        }
    }
}
