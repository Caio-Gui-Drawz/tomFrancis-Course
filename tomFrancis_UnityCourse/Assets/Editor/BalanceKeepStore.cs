using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Guarda, entre sessões do Editor, as mudanças feitas em objetos DE CENA durante o Play mode
// (para a janela de Balance reaplicá-las ao sair do Play — o "Keep Changes").
[FilePath("Library/CosmoBalanceKeep.asset", FilePathAttribute.Location.ProjectFolder)]
public class BalanceKeepStore : ScriptableSingleton<BalanceKeepStore>
{
    [Serializable]
    public class Entry
    {
        public string objectId;
        public string propertyPath;
        public string kind;
        public string value;
        public string label;
    }

    public List<Entry> entries = new List<Entry>();

    public void SaveStore()
    {
        Save(true);
    }
}
