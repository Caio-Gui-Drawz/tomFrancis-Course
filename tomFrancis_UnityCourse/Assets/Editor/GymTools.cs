using UnityEditor;
using UnityEngine;

// Menu: Tools > Cosmo Treasure > Convert Selected Prefabs To Gym Slots
// Selecione na Hierarchy os inimigos/tesouros que você já posicionou na cena de GYM e rode o comando:
// cada um vira um GymDummySlot no mesmo lugar (com o mesmo prefab) e a instância original é removida.
public static class GymTools
{
    [MenuItem("Tools/Cosmo Treasure/Convert Selected Prefabs To Gym Slots")]
    static void ConvertSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        int converted = 0;

        foreach (GameObject go in selected)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (source == null || !PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;

            GameObject slotObject = new GameObject("GymSlot_" + source.name);
            Undo.RegisterCreatedObjectUndo(slotObject, "Convert To Gym Slot");
            slotObject.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);

            GymDummySlot slot = slotObject.AddComponent<GymDummySlot>();
            slot.prefab = source;

            Undo.DestroyObjectImmediate(go);
            converted++;
        }

        Debug.Log("Gym: " + converted + " objeto(s) convertido(s) em slots. (Só instâncias de prefab são convertidas.)");
    }
}
