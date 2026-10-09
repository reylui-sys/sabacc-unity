using UnityEngine;
using Photon.Pun;

// Oculta el avatar del jugador local para que no se tape la vista. 
public class PlayerVisibility : MonoBehaviourPun
{
    void Start()
    {
        // Verificamos que el PhotonView exista
        if (photonView == null)
        {
            Debug.LogError("¡PhotonView es null en PlayerVisibility!");
            return;
        }

        // Comparamos los números de actor para saber si este prefab es "mío"
        if (photonView.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            HidePlayerForLocal();
        }
    }

    // Desactiva todos los componentes gráficos (Renderers) del jugador local
    void HidePlayerForLocal()
    {
        // Buscamos todos los Renderers (incluyendo en objetos desactivados)
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = false; // Los hacemos invisibles
        }
    }
}