using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;        // Para usar TextMeshPro

// Muestra el nombre del jugador sobre su avatar en la escena de juego.
public class PlayerNameDisplay : MonoBehaviourPun
{
    public TMP_Text playerNameText; // Texto (de TextMeshPro) que muestra el nombre

    void Start()
    {
        // Verificamos que el PhotonView exista (importante para evitar errores)
        if (photonView == null)
        {
            Debug.LogError("PhotonView es nulo en PlayerNameDisplay!"); 
            return;
        }

        // Mostramos el nombre del dueño de este prefab (el jugador que lo creó)
        playerNameText.text = photonView.Owner.NickName;

        /*
        if (photonView.IsMine)
        {
            // Opcional: podrías querer ocultar tu propio nombre
            // gameObject.SetActive(false);
        }
        else
        {
            // Mostrar nombre del dueño del prefab
            playerNameText.text = photonView.Owner.NickName;
        }*/
    }
}