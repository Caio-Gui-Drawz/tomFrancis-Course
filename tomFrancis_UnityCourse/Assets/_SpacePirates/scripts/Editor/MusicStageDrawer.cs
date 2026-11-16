#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Lista de seleção dos estágios do MusicController para campos marcados com [MusicStage].
[CustomPropertyDrawer(typeof(MusicStageAttribute))]
public class MusicStageDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        MusicController mc = Object.FindFirstObjectByType<MusicController>();
        List<string> names = new List<string> { "(não esperar a música)" };
        if (mc != null) names.AddRange(mc.StageNames.Distinct());

        string current = property.stringValue;
        int index = string.IsNullOrEmpty(current) ? 0 : names.IndexOf(current);
        if (index < 0)
        {
            names.Add(current + "  (não existe)");
            index = names.Count - 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        int picked = EditorGUI.Popup(position, label.text, index, names.ToArray());
        if (picked != index) property.stringValue = picked == 0 ? "" : names[picked];
        EditorGUI.EndProperty();
    }
}
#endif
