using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SortingLayerSelectAttribute))]
public class SortingLayerSelectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.LabelField(position, label.text, "Use [SortingLayerSelect] em um campo int");
            return;
        }

        SortingLayer[] layers = SortingLayer.layers;
        string[] names = new string[layers.Length];
        int current = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            names[i] = layers[i].name;
            if (layers[i].id == property.intValue) current = i;
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(position, label.text, current, names);
        if (EditorGUI.EndChangeCheck()) property.intValue = layers[selected].id;
        EditorGUI.EndProperty();
    }
}
