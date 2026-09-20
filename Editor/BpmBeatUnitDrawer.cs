using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomPropertyDrawer(typeof(BpmBeatUnit))]
    internal sealed class BpmBeatUnitDrawer : PropertyDrawer
    {
        // Fields

        private static readonly GUIContent[] Options =
        {
            new("1"),
            new("2"),
            new("4"),
            new("8"),
            new("16"),
        };

        private static readonly int[] Values =
        {
            (int)BpmBeatUnit.Whole,
            (int)BpmBeatUnit.Half,
            (int)BpmBeatUnit.Quarter,
            (int)BpmBeatUnit.Eighth,
            (int)BpmBeatUnit.Sixteenth,
        };


        // Methods

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var previousShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            var selectedIndex = FindSelectedIndex(property.intValue);
            var nextIndex = EditorGUI.Popup(position, label, selectedIndex, Options);
            if (nextIndex >= 0 && nextIndex < Values.Length && (property.hasMultipleDifferentValues || nextIndex != selectedIndex))
            {
                property.intValue = Values[nextIndex];
            }

            EditorGUI.showMixedValue = previousShowMixedValue;
            EditorGUI.EndProperty();
        }

        private static int FindSelectedIndex(int value)
        {
            for (var index = 0; index < Values.Length; index++)
            {
                if (Values[index] == value) return index;
            }

            return -1;
        }
    }
}
