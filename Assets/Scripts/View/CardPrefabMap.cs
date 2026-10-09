using UnityEngine;
using System.Collections.Generic;

// Script hecho e idealizado por ChatGPT, donde se hace un Scriptable object para desacoplar
// datos del código y evitar buscar los nombres por escena.

/*////////////////////////////////////////////////////////////////////////////////////////
Cómo usarlo:
    1. En el proyecto, haced clic derecho → Create > Sabacc > Card Prefab Map.
    2. Abridlo en el Inspector.
    3. Arrastrad cada uno de tus 76 prefabs a la lista, y escribid su cardId correcto.
 ///////////////////////////////////////////////////////////////////////////////////////*/

[CreateAssetMenu(fileName = "CardPrefabMap", menuName = "Sabacc/Card Prefab Map")]
public class CardPrefabMap : ScriptableObject
{
    // Estructura para guardad el prefab con su ID
    [System.Serializable]
    public struct CardPrefabPair
    {
        public string cardId;      // ej: "Especial_ElIdiota"
        public GameObject prefab;  // Referencia al prefab de esa carta
    }

    // Lista de la estructura para guardar los pares
    public List<CardPrefabPair> cardPrefabs = new List<CardPrefabPair>();

    // Diccionario privado para poder hacer búsquedas más eficientes,
    // List<> = O(n) y Dictionary<> = O(1)
    private Dictionary<string, GameObject> _lookup;

    //Recorre la lista cardPrefabs y llena el diccionario.
    public GameObject GetPrefab(string cardId)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<string, GameObject>();
            foreach (var pair in cardPrefabs)
            {
                _lookup[pair.cardId] = pair.prefab;
            }
        }

        // Método del diccionario para intentar obtener el prefab asociado a la ID
        // devolviendo true o false según su existencia y el propio prefab (si no null)
        return _lookup.TryGetValue(cardId, out GameObject prefab) ? prefab : null;
    }
}