using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AutoFillCardPrefabMap : EditorWindow
{
    private CardPrefabMap _targetMap;
    private string _prefabsRootPath = "Assets/Prefabs/SabaccDeck";

    [MenuItem("Tools/Sabacc/Fill Card Prefab Map")]
    public static void ShowWindow()
    {
        GetWindow<AutoFillCardPrefabMap>("Fill Card Prefab Map");
    }

    void OnGUI()
    {
        GUILayout.Label("Auto-fill (con guion bajo)", EditorStyles.boldLabel);
        _targetMap = (CardPrefabMap)EditorGUILayout.ObjectField("Target Map", _targetMap, typeof(CardPrefabMap), false);
        _prefabsRootPath = EditorGUILayout.TextField("Prefabs Root Path", _prefabsRootPath);
        if (GUILayout.Button("Fill Map") && _targetMap != null)
        {
            FillMap();
        }
    }

    void FillMap()
    {
        var pairs = new List<CardPrefabMap.CardPrefabPair>();

        // Función para procesar una carpeta y asignar suit (o null si es especial)
        void ProcessFolder(string folderName, string suit)
        {
            string fullPath = Path.Combine(_prefabsRootPath, folderName);
            if (!AssetDatabase.IsValidFolder(fullPath)) return;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { fullPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // El nombre del prefab es el cardId directamente
                string fileName = Path.GetFileNameWithoutExtension(path); // ej: "Monedas_1" o "El_Idiota"

                pairs.Add(new CardPrefabMap.CardPrefabPair
                {
                    cardId = fileName,
                    prefab = prefab
                });
            }
        }

        // Procesar las 5 carpetas
        ProcessFolder("Monedas", "Monedas");
        ProcessFolder("Frascos", "Frascos");
        ProcessFolder("Sables", "Sables");
        ProcessFolder("Bastones", "Bastones");
        ProcessFolder("Especiales", null); // especiales no tienen suit

        _targetMap.cardPrefabs = pairs;
        EditorUtility.SetDirty(_targetMap);
        AssetDatabase.SaveAssets();
        Debug.Log($"✅ CardPrefabMap llenado con {pairs.Count} cartas.");
    }
}