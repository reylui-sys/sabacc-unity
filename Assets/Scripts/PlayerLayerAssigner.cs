// PlayerLayerAssigner.cs
using UnityEngine;
using Photon.Pun;

// Asigna el layer correcto al jugador basado en su ActorNumber en Photon.
public class PlayerLayerAssigner : MonoBehaviourPun
{
    void Start()
    {
        // Solo el cliente dueño asigna el layer
        if (!photonView.IsMine)
            return;

        int spawnIndex = photonView.OwnerActorNr - 1; // ActorNumber empieza en 1
        string[] playerLayers = { "Player1", "Player2", "Player3", "Player4" };

        if (spawnIndex >= 0 && spawnIndex < playerLayers.Length)
        {
            int layerId = LayerMask.NameToLayer(playerLayers[spawnIndex]);
            if (layerId != -1)
            {
                SetLayerRecursively(gameObject, layerId);
            }
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}