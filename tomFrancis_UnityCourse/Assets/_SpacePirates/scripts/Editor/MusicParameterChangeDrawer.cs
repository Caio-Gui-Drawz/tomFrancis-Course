#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FMODUnity;

// Dropdown de parâmetro (e de label / valor) do FMOD para o MusicController.
// Lê a lista direto do cache de eventos do FMOD (se estiver vazia: FMOD > Refresh Banks).
[CustomPropertyDrawer(typeof(MusicController.ParameterChange))]
public class MusicParameterChangeDrawer : PropertyDrawer
{
    static float Line { get { return EditorGUIUtility.singleLineHeight; } }
    static float Gap { get { return EditorGUIUtility.standardVerticalSpacing; } }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return Line * 2f + Gap * 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty pName = property.FindPropertyRelative("parameter");
        SerializedProperty pGlobal = property.FindPropertyRelative("isGlobal");
        SerializedProperty pMode = property.FindPropertyRelative("mode");
        SerializedProperty pLabel = property.FindPropertyRelative("label");
        SerializedProperty pValue = property.FindPropertyRelative("value");

        List<EditorParamRef> all = EventManager.Parameters
            .Where(p => p != null && !string.IsNullOrEmpty(p.Name))
            .GroupBy(p => (p.IsGlobal ? "G:" : "L:") + p.Name).Select(g => g.First())
            .OrderByDescending(p => p.IsGlobal).ThenBy(p => p.Name).ToList();

        EditorGUI.BeginProperty(position, label, property);

        // Linha 1: parâmetro
        Rect r1 = new Rect(position.x, position.y, position.width, Line);
        EditorParamRef current = all.FirstOrDefault(p => p.Name == pName.stringValue && p.IsGlobal == pGlobal.boolValue)
                                 ?? all.FirstOrDefault(p => p.Name == pName.stringValue);

        List<string> names = new List<string> { "(escolha o parâmetro)" };
        names.AddRange(all.Select(p => (p.IsGlobal ? "Global/" : "Local/") + p.Name));
        int index = current != null ? all.IndexOf(current) + 1 : 0;
        if (current == null && !string.IsNullOrEmpty(pName.stringValue))
        {
            names.Add(pName.stringValue + "  (não encontrado)");
            index = names.Count - 1;
        }

        int picked = EditorGUI.Popup(r1, "Parâmetro", index, names.ToArray());
        if (picked != index && picked >= 1 && picked <= all.Count)
        {
            EditorParamRef p = all[picked - 1];
            pName.stringValue = p.Name;
            pGlobal.boolValue = p.IsGlobal;
            pMode.enumValueIndex = p.Type == ParameterType.Labeled ? (int)MusicController.ChangeMode.Label : (int)MusicController.ChangeMode.Value;
            pValue.floatValue = Mathf.Clamp(pValue.floatValue, p.Min, p.Max);
            if (p.Type == ParameterType.Labeled && p.Labels != null && p.Labels.Length > 0 && !p.Labels.Contains(pLabel.stringValue))
                pLabel.stringValue = p.Labels[0];
            current = p;
        }

        // Linha 2: label / valor
        Rect r2 = new Rect(position.x, position.y + Line + Gap, position.width, Line);
        if (current != null && current.Type == ParameterType.Labeled && current.Labels != null && current.Labels.Length > 0)
        {
            int li = System.Array.IndexOf(current.Labels, pLabel.stringValue);
            string[] opts = current.Labels.ToArray();
            if (li < 0) li = 0;
            int newLi = EditorGUI.Popup(r2, "Label", li, opts);
            if (newLi != li || pLabel.stringValue != opts[newLi]) pLabel.stringValue = opts[newLi];
            pMode.enumValueIndex = (int)MusicController.ChangeMode.Label;
        }
        else if (current != null)
        {
            pMode.enumValueIndex = (int)MusicController.ChangeMode.Value;
            if (current.Type == ParameterType.Discrete)
                pValue.floatValue = EditorGUI.IntSlider(r2, "Valor", Mathf.RoundToInt(pValue.floatValue), Mathf.RoundToInt(current.Min), Mathf.RoundToInt(current.Max));
            else
                pValue.floatValue = EditorGUI.Slider(r2, "Valor", pValue.floatValue, current.Min, current.Max);
        }
        else
        {
            // Parâmetro não encontrado (ou lista vazia): campos manuais.
            float half = position.width * 0.5f - 2f;
            Rect a = new Rect(r2.x, r2.y, half, Line);
            Rect b = new Rect(r2.x + half + 4f, r2.y, half, Line);
            EditorGUI.PropertyField(a, pName, new GUIContent("Nome"));
            if (pMode.enumValueIndex == (int)MusicController.ChangeMode.Label) EditorGUI.PropertyField(b, pLabel, new GUIContent("Label"));
            else EditorGUI.PropertyField(b, pValue, new GUIContent("Valor"));
        }

        EditorGUI.EndProperty();
    }
}
#endif
