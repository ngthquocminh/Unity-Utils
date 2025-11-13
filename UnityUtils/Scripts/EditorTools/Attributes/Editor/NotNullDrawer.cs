using UnityEditor;
using UnityEngine;
namespace UnityUtils
{
    [CustomPropertyDrawer(typeof(NotNullAttribute))]
    public class NotNullDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Draw the property field using only its base height so we can render the help box below it without overlap
            float baseHeight = EditorGUI.GetPropertyHeight(property, label, true);
            Rect fieldRect = new Rect(position.x, position.y, position.width, baseHeight);
            EditorGUI.PropertyField(fieldRect, property, label, true);

            // Check if the property is null and draw help box below the field
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
            {
                Rect helpRect = new Rect(position.x, position.y + baseHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(helpRect, $"{property.name} must not be null.", MessageType.Warning);

                // Optional: Log warning to console (commented out to avoid spamming logs every repaint)
                // var context = property.serializedObject.targetObject;
                // Debug.LogWarning($"[NotNull] Field '{property.name}' on '{context.name}' is null.", context);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Add extra height if null (include small spacing between field and help box)
            float baseHeight = EditorGUI.GetPropertyHeight(property, label, true);
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
            {
                return baseHeight + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            return baseHeight;
        }
    }
}
