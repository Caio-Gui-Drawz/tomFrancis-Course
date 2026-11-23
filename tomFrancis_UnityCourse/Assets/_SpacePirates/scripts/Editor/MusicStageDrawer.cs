#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Listas de seleção do Inspector para os campos do WaveEventController:
//   [AnimatorTrigger]  -> parâmetros Trigger do Animator do mesmo evento
//   [AnimatorState]    -> estados do Animator do mesmo evento (na layer escolhida)
// (O nome do arquivo ficou "MusicStageDrawer" por histórico.)

static class PopupDrawerUtil
{
    public static void Draw(Rect position, SerializedProperty property, GUIContent label, string noneLabel, List<string> options)
    {
        List<string> names = new List<string> { noneLabel };
        names.AddRange(options.Distinct());

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

    public static AnimatorController GetController(SerializedProperty property)
    {
        string path = property.propertyPath;
        int dot = path.LastIndexOf('.');
        string parent = dot >= 0 ? path.Substring(0, dot + 1) : "";
        SerializedProperty a = property.serializedObject.FindProperty(parent + "animator");
        if (a == null) return null;
        Animator animator = a.objectReferenceValue as Animator;
        if (animator == null) return null;

        RuntimeAnimatorController rc = animator.runtimeAnimatorController;
        AnimatorOverrideController ov = rc as AnimatorOverrideController;
        if (ov != null) rc = ov.runtimeAnimatorController;
        return rc as AnimatorController;
    }

    public static int GetLayer(SerializedProperty property)
    {
        string path = property.propertyPath;
        int dot = path.LastIndexOf('.');
        string parent = dot >= 0 ? path.Substring(0, dot + 1) : "";
        SerializedProperty l = property.serializedObject.FindProperty(parent + "animatorLayer");
        return l != null ? l.intValue : 0;
    }

    public static void CollectStates(AnimatorStateMachine sm, List<string> list)
    {
        foreach (ChildAnimatorState s in sm.states) list.Add(s.state.name);
        foreach (ChildAnimatorStateMachine sub in sm.stateMachines) CollectStates(sub.stateMachine, list);
    }
}

[CustomPropertyDrawer(typeof(AnimatorTriggerAttribute))]
public class AnimatorTriggerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        AnimatorController controller = PopupDrawerUtil.GetController(property);
        List<string> names = new List<string>();
        if (controller != null)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) names.Add(p.name);
        }

        EditorGUI.BeginDisabledGroup(controller == null);
        PopupDrawerUtil.Draw(position, property, label, controller == null ? "(arraste o Animator acima)" : "(sem animação)", names);
        EditorGUI.EndDisabledGroup();
    }
}

[CustomPropertyDrawer(typeof(AnimatorStateAttribute))]
public class AnimatorStateDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        AnimatorController controller = PopupDrawerUtil.GetController(property);
        List<string> names = new List<string>();
        if (controller != null)
        {
            int layer = PopupDrawerUtil.GetLayer(property);
            if (layer >= 0 && layer < controller.layers.Length)
                PopupDrawerUtil.CollectStates(controller.layers[layer].stateMachine, names);
        }

        EditorGUI.BeginDisabledGroup(controller == null);
        PopupDrawerUtil.Draw(position, property, label, controller == null ? "(arraste o Animator acima)" : "(não esperar a animação)", names);
        EditorGUI.EndDisabledGroup();
    }
}
#endif
