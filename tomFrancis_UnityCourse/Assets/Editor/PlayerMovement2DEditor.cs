using UnityEditor;
using UnityEngine;

// Botão grande no topo do componente para alternar as duas versões do jogo (funciona também em Play).
[CustomEditor(typeof(PlayerMovement2D))]
public class PlayerMovement2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SerializedProperty enabledProp = serializedObject.FindProperty("verticalMovementEnabled");

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = enabledProp.boolValue ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.95f, 0.5f, 0.45f);
        string label = enabledProp.boolValue
            ? "Versão COM movimento vertical (clique para travar o player)"
            : "Versão TRAVADA, como antes (clique para liberar o movimento)";
        if (GUILayout.Button(label, GUILayout.Height(34)))
        {
            enabledProp.boolValue = !enabledProp.boolValue;
            serializedObject.ApplyModifiedProperties();
        }
        GUI.backgroundColor = previous;

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
