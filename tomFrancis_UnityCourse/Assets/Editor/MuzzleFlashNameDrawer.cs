using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Lista de seleção para campos [MuzzleFlashName]: mostra os muzzle flashes (ParticleSystems filhos) do MuzzleFlashPlayer
// da cena ou, se não houver um aberto, do primeiro prefab do projeto que tenha esse componente.
[CustomPropertyDrawer(typeof(MuzzleFlashNameAttribute))]
public class MuzzleFlashNameDrawer : PropertyDrawer
{
    static string[] cached = new string[0];
    static double cachedAt = -100.0;

    static string[] GetNames(bool force)
    {
        double now = EditorApplication.timeSinceStartup;
        if (!force && cached.Length > 0 && now - cachedAt < 3.0) return cached;
        if (!force && cached.Length == 0 && now - cachedAt < 3.0) return cached;
        cachedAt = now;

        Transform root = null;
        MuzzleFlashPlayer inScene = Object.FindFirstObjectByType<MuzzleFlashPlayer>(FindObjectsInactive.Include);
        if (inScene != null) root = inScene.transform;

        if (root == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_SpacePirates" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;
                MuzzleFlashPlayer p = prefab.GetComponentInChildren<MuzzleFlashPlayer>(true);
                if (p != null) { root = p.transform; break; }
            }
        }

        List<string> names = new List<string>();
        if (root != null)
        {
            foreach (ParticleSystem ps in MuzzleFlashPlayer.FindRootSystems(root))
            {
                if (!names.Contains(ps.gameObject.name)) names.Add(ps.gameObject.name);
            }
        }
        cached = names.ToArray();
        return cached;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string current = property.stringValue;
        string[] names = GetNames(false);

        List<GUIContent> options = new List<GUIContent>();
        options.Add(new GUIContent("(nenhum)"));
        foreach (string n in names) options.Add(new GUIContent(n));

        int index = 0;
        if (!string.IsNullOrEmpty(current))
        {
            index = System.Array.IndexOf(names, current) + 1;
            if (index == 0)
            {
                // Nome salvo que não existe mais (ou lista vazia): mantém visível e tenta recarregar a lista.
                options.Add(new GUIContent(current + " (não encontrado)"));
                index = options.Count - 1;
                GetNames(true);
            }
        }

        EditorGUI.BeginChangeCheck();
        int picked = EditorGUI.Popup(position, label, index, options.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            if (picked == 0) property.stringValue = "";
            else if (picked <= names.Length) property.stringValue = names[picked - 1];
        }
    }
}
