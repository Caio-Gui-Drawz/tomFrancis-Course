using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Janela que lista TUDO que desenha na tela agrupado por Sorting Layer e ordenado pelo Order in Layer
// (de cima = mais na frente, até embaixo = mais atrás). Cobre a cena aberta e/ou os prefabs do projeto
// (inimigos, balas, caixas e muzzle flashes só existem na cena quando o jogo roda).
// Dá para EDITAR direto aqui: número (Order in Layer), botões +1/-1 e a Sorting Layer de cada item.
// Clique no nome para selecionar o objeto. As mudanças entram no Undo (Ctrl+Z).
public class SortingOrderOverview : EditorWindow
{
    enum Scope { CenaAberta, PrefabsDoProjeto, Ambos }

    class Row
    {
        public string path;
        public string type;
        public string source;
        public string layerName;
        public int layerValue;
        public int order;
        public Object target;      // o Renderer ou Canvas que será editado
        public Object selectTarget; // o GameObject que o clique seleciona
    }

    Scope scope = Scope.Ambos;
    string filter = "";
    string prefabFolder = "Assets/_SpacePirates";
    Vector2 scroll;
    readonly List<Row> rows = new List<Row>();
    GUIStyle pathStyle;
    GUIStyle orderStyle;
    bool needsResort;
    string[] layerNames;
    int[] layerIds;

    [MenuItem("Cosmo Treasure/Sorting Overview")]
    static void Open()
    {
        SortingOrderOverview w = GetWindow<SortingOrderOverview>("Sorting");
        w.Refresh();
        w.Show();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Refresh()
    {
        rows.Clear();
        needsResort = false;

        SortingLayer[] layers = SortingLayer.layers;
        layerNames = new string[layers.Length];
        layerIds = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            layerNames[i] = layers[i].name;
            layerIds[i] = layers[i].id;
        }

        if (scope != Scope.PrefabsDoProjeto)
        {
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                AddRenderer(r, "cena");
            }
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                AddCanvas(c, "cena");
            }
        }

        if (scope != Scope.CenaAberta)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;
                string source = "prefab";
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true)) AddRenderer(r, source);
                foreach (Canvas c in prefab.GetComponentsInChildren<Canvas>(true)) AddCanvas(c, source);
            }
        }

        SortRows();
    }

    // Mais na frente primeiro: layer com valor maior, depois order maior.
    void SortRows()
    {
        rows.Sort((a, b) =>
        {
            if (a.layerValue != b.layerValue) return b.layerValue.CompareTo(a.layerValue);
            if (a.order != b.order) return b.order.CompareTo(a.order);
            return string.Compare(a.path, b.path, System.StringComparison.Ordinal);
        });
        needsResort = false;
    }

    void AddRenderer(Renderer r, string source)
    {
        Row row = new Row();
        row.path = PathOf(r.transform);
        row.type = r.GetComponent("SkeletonRenderer") != null ? "Spine" : r.GetType().Name;
        row.source = source;
        row.layerName = SortingLayer.IDToName(r.sortingLayerID);
        row.layerValue = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
        row.order = r.sortingOrder;
        row.target = r;
        row.selectTarget = r.gameObject;
        rows.Add(row);
    }

    void AddCanvas(Canvas c, string source)
    {
        if (!c.isRootCanvas && !c.overrideSorting) return;
        Row row = new Row();
        row.path = PathOf(c.transform);
        row.type = "Canvas";
        row.source = source;
        row.layerName = SortingLayer.IDToName(c.sortingLayerID);
        row.layerValue = SortingLayer.GetLayerValueFromID(c.sortingLayerID);
        row.order = c.sortingOrder;
        row.target = c;
        row.selectTarget = c.gameObject;
        rows.Add(row);
    }

    // Grava layer e/ou order no objeto (via SerializedObject: entra no Undo e vira override certo em prefab).
    void SetSorting(Row row, int? layerId, int? order)
    {
        if (row.target == null) return;
        SerializedObject so = new SerializedObject(row.target);
        SerializedProperty layerProp = so.FindProperty("m_SortingLayerID");
        SerializedProperty orderProp = so.FindProperty("m_SortingOrder");
        if (layerProp == null || orderProp == null) return;

        if (layerId.HasValue)
        {
            layerProp.intValue = layerId.Value;
            row.layerName = SortingLayer.IDToName(layerId.Value);
            row.layerValue = SortingLayer.GetLayerValueFromID(layerId.Value);
        }
        if (order.HasValue)
        {
            orderProp.intValue = order.Value;
            row.order = order.Value;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(row.target);

        if (row.source == "cena")
        {
            Component c = row.target as Component;
            if (c != null) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }
        else
        {
            AssetDatabase.SaveAssets();
        }
        needsResort = true;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    void OnGUI()
    {
        if (pathStyle == null)
        {
            pathStyle = new GUIStyle(EditorStyles.label);
            pathStyle.alignment = TextAnchor.MiddleLeft;
            pathStyle.clipping = TextClipping.Clip;
            orderStyle = new GUIStyle(EditorStyles.boldLabel);
            orderStyle.alignment = TextAnchor.MiddleRight;
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            scope = (Scope)EditorGUILayout.EnumPopup(scope, EditorStyles.toolbarPopup, GUILayout.Width(130f));
            if (EditorGUI.EndChangeCheck()) Refresh();
            GUILayout.Label("Filtro:", EditorStyles.miniLabel, GUILayout.Width(36f));
            filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarTextField);
            if (GUILayout.Button("Atualizar", EditorStyles.toolbarButton, GUILayout.Width(64f))) Refresh();
        }

        EditorGUILayout.HelpBox("De cima para baixo = da frente para trás. Dentro de cada Sorting Layer vale o número (Order in Layer): maior = mais na frente. " +
            "Um personagem Spine é UM só desenho (uma ordem para o esqueleto inteiro). Edite o número (Enter confirma), use ▲/▼ para +1/-1 ou troque a Sorting Layer. Clique no nome para selecionar o objeto. Ctrl+Z desfaz.", MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        string lastLayer = null;
        string f = filter == null ? "" : filter.Trim();

        foreach (Row row in rows)
        {
            if (f.Length > 0 && row.path.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                row.type.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            if (row.layerName != lastLayer)
            {
                lastLayer = row.layerName;
                GUILayout.Space(4f);
                GUILayout.Label("Sorting Layer: " + row.layerName, EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int newOrder = EditorGUILayout.DelayedIntField(row.order, GUILayout.Width(52f));
                if (newOrder != row.order) SetSorting(row, null, newOrder);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20f))) SetSorting(row, null, row.order + 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(20f))) SetSorting(row, null, row.order - 1);

                int layerIndex = System.Array.IndexOf(layerNames, row.layerName);
                int pickedLayer = EditorGUILayout.Popup(layerIndex < 0 ? 0 : layerIndex, layerNames, GUILayout.Width(96f));
                if (pickedLayer != layerIndex && pickedLayer >= 0 && pickedLayer < layerIds.Length) SetSorting(row, layerIds[pickedLayer], null);

                GUILayout.Label(row.type, EditorStyles.miniLabel, GUILayout.Width(80f));
                GUILayout.Label(row.source, EditorStyles.miniLabel, GUILayout.Width(36f));

                Rect r = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                GUI.Label(r, new GUIContent(row.path, row.path), pathStyle);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition) && row.selectTarget != null)
                {
                    Selection.activeObject = row.selectTarget;
                    EditorGUIUtility.PingObject(row.selectTarget);
                    Event.current.Use();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        // Reordena só depois de desenhar a lista inteira (mexer na lista no meio do loop quebra o IMGUI).
        if (needsResort)
        {
            SortRows();
            Repaint();
        }
    }
}
