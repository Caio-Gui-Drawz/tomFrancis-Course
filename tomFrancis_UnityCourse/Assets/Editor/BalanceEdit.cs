using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pipeline único de edição da janela de Balance:
//  - Asset (ScriptableObject ou prefab): grava no asset (salva sozinho) e, em Play mode, copia o valor
//    para as instâncias vivas desse prefab (inclusive as do pool).
//  - Objeto de cena (ex.: WaveManager): vale na hora; em Play mode, anota a mudança para reaplicar
//    quando você sair do Play (Keep Changes).
[InitializeOnLoad]
public static class BalanceEdit
{
    const string KeepPref = "CosmoBalance.keepPlayChanges";
    const string AutoSavePref = "CosmoBalance.autoSave";

    static double saveAt = -1;

    static BalanceEdit()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
    }

    public static bool KeepPlayChanges
    {
        get { return EditorPrefs.GetBool(KeepPref, true); }
        set { EditorPrefs.SetBool(KeepPref, value); }
    }

    public static bool AutoSave
    {
        get { return EditorPrefs.GetBool(AutoSavePref, true); }
        set { EditorPrefs.SetBool(AutoSavePref, value); }
    }

    static void Tick()
    {
        if (saveAt > 0 && EditorApplication.timeSinceStartup >= saveAt)
        {
            saveAt = -1;
            AssetDatabase.SaveAssets();
        }
    }

    public static void SaveNow()
    {
        saveAt = -1;
        AssetDatabase.SaveAssets();
    }

    // Chamar depois de so.ApplyModifiedProperties() quando o campo "path" mudou.
    public static void AfterEdit(SerializedObject so, string path)
    {
        UnityEngine.Object target = so.targetObject;
        if (target == null) return;
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null) return;

        if (EditorUtility.IsPersistent(target))
        {
            EditorUtility.SetDirty(target);
            if (AutoSave) saveAt = EditorApplication.timeSinceStartup + 1.0;

            Component component = target as Component;
            if (Application.isPlaying && component != null) PropagateToLive(component, prop);
        }
        else
        {
            if (Application.isPlaying)
            {
                Component component = target as Component;
                if (KeepPlayChanges && component != null) Record(component, prop);
            }
            else
            {
                MarkSceneDirty(target);
            }
        }
    }

    // ---------- Instâncias vivas ----------

    static void PropagateToLive(Component prefabComponent, SerializedProperty source)
    {
        UnityEngine.Object[] live = UnityEngine.Object.FindObjectsByType(prefabComponent.GetType(), FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnityEngine.Object o in live)
        {
            if (o == null || o == prefabComponent) continue;
            if (EditorUtility.IsPersistent(o)) continue;
            if (!DescendsFrom(o, prefabComponent)) continue;

            SerializedObject so = new SerializedObject(o);
            SerializedProperty dst = so.FindProperty(source.propertyPath);
            if (dst == null) continue;
            if (CopyValue(source, dst)) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // O objeto vivo veio deste prefab (direto ou por uma cadeia de variants)?
    static bool DescendsFrom(UnityEngine.Object instance, UnityEngine.Object prefabObject)
    {
        UnityEngine.Object src = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        int guard = 0;
        while (src != null && guard++ < 10)
        {
            if (src == prefabObject) return true;
            src = PrefabUtility.GetCorrespondingObjectFromSource(src);
        }
        return false;
    }

    static bool CopyValue(SerializedProperty src, SerializedProperty dst)
    {
        if (src.propertyType != dst.propertyType) return false;
        switch (src.propertyType)
        {
            case SerializedPropertyType.Float: dst.floatValue = src.floatValue; return true;
            case SerializedPropertyType.Integer: dst.intValue = src.intValue; return true;
            case SerializedPropertyType.Boolean: dst.boolValue = src.boolValue; return true;
            case SerializedPropertyType.String: dst.stringValue = src.stringValue; return true;
            case SerializedPropertyType.Enum: dst.enumValueIndex = src.enumValueIndex; return true;
            case SerializedPropertyType.Color: dst.colorValue = src.colorValue; return true;
            case SerializedPropertyType.Vector2: dst.vector2Value = src.vector2Value; return true;
            case SerializedPropertyType.Vector3: dst.vector3Value = src.vector3Value; return true;
            case SerializedPropertyType.ObjectReference: dst.objectReferenceValue = src.objectReferenceValue; return true;
            case SerializedPropertyType.AnimationCurve: dst.animationCurveValue = src.animationCurveValue; return true;
        }
        return false;
    }

    // ---------- Keep Changes (objetos de cena em Play mode) ----------

    static void Record(Component target, SerializedProperty prop)
    {
        string kind;
        string value;
        if (!Encode(prop, out kind, out value)) return;

        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(target);
        if (gid.identifierType != 2) return; // só objetos que existem no arquivo da cena

        string id = gid.ToString();
        BalanceKeepStore store = BalanceKeepStore.instance;
        BalanceKeepStore.Entry entry = store.entries.Find(e => e.objectId == id && e.propertyPath == prop.propertyPath);
        if (entry == null)
        {
            entry = new BalanceKeepStore.Entry();
            entry.objectId = id;
            entry.propertyPath = prop.propertyPath;
            store.entries.Add(entry);
        }
        entry.kind = kind;
        entry.value = value;
        entry.label = target.gameObject.name + " / " + target.GetType().Name + "." + prop.propertyPath;
        store.SaveStore();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) ApplyKept();
    }

    public static int KeptCount
    {
        get { return BalanceKeepStore.instance.entries.Count; }
    }

    public static List<BalanceKeepStore.Entry> KeptEntries
    {
        get { return BalanceKeepStore.instance.entries; }
    }

    public static void DiscardKept()
    {
        BalanceKeepStore store = BalanceKeepStore.instance;
        store.entries.Clear();
        store.SaveStore();
    }

    public static void ApplyKept()
    {
        BalanceKeepStore store = BalanceKeepStore.instance;
        if (store.entries.Count == 0) return;

        List<BalanceKeepStore.Entry> remaining = new List<BalanceKeepStore.Entry>();
        foreach (BalanceKeepStore.Entry e in store.entries)
        {
            GlobalObjectId gid;
            if (!GlobalObjectId.TryParse(e.objectId, out gid)) continue;

            UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            if (obj == null) { remaining.Add(e); continue; } // cena ainda não carregada: tenta de novo depois

            SerializedObject so = new SerializedObject(obj);
            SerializedProperty prop = so.FindProperty(e.propertyPath);
            if (prop == null || !Decode(prop, e.kind, e.value)) continue;

            so.ApplyModifiedProperties();
            MarkSceneDirty(obj);
        }

        store.entries = remaining;
        store.SaveStore();
    }

    static void MarkSceneDirty(UnityEngine.Object obj)
    {
        Component c = obj as Component;
        if (c != null && c.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }

    public static bool Encode(SerializedProperty p, out string kind, out string value)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float: kind = "f"; value = p.floatValue.ToString("R", inv); return true;
            case SerializedPropertyType.Integer: kind = "i"; value = p.intValue.ToString(inv); return true;
            case SerializedPropertyType.Boolean: kind = "b"; value = p.boolValue ? "1" : "0"; return true;
            case SerializedPropertyType.Enum: kind = "e"; value = p.enumValueIndex.ToString(inv); return true;
            case SerializedPropertyType.String: kind = "s"; value = p.stringValue; return true;
            case SerializedPropertyType.Vector2:
                kind = "v2"; value = p.vector2Value.x.ToString("R", inv) + ";" + p.vector2Value.y.ToString("R", inv); return true;
            case SerializedPropertyType.Color:
                kind = "c";
                value = p.colorValue.r.ToString("R", inv) + ";" + p.colorValue.g.ToString("R", inv) + ";" +
                        p.colorValue.b.ToString("R", inv) + ";" + p.colorValue.a.ToString("R", inv);
                return true;
        }
        kind = null;
        value = null;
        return false;
    }

    public static bool Decode(SerializedProperty p, string kind, string value)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        try
        {
            switch (kind)
            {
                case "f": p.floatValue = float.Parse(value, inv); return true;
                case "i": p.intValue = int.Parse(value, inv); return true;
                case "b": p.boolValue = value == "1"; return true;
                case "e": p.enumValueIndex = int.Parse(value, inv); return true;
                case "s": p.stringValue = value; return true;
                case "v2":
                {
                    string[] a = value.Split(';');
                    p.vector2Value = new Vector2(float.Parse(a[0], inv), float.Parse(a[1], inv));
                    return true;
                }
                case "c":
                {
                    string[] a = value.Split(';');
                    p.colorValue = new Color(float.Parse(a[0], inv), float.Parse(a[1], inv), float.Parse(a[2], inv), float.Parse(a[3], inv));
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }
}
