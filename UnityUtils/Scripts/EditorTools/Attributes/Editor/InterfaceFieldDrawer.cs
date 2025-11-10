using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InterfaceFieldAttribute))]
public class InterfaceFieldDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the object field
        var obj = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(Object), true);

        // Validate interface implementation
        var attr = (InterfaceFieldAttribute)attribute;
        if (obj != null && !attr.InterfaceType.IsAssignableFrom(obj.GetType()))
        {
            // Debug.LogWarning($"{obj.name} does not implement {attr.InterfaceType.Name}", obj);
        }

        property.objectReferenceValue = obj;
        EditorGUI.EndProperty();
    }
}