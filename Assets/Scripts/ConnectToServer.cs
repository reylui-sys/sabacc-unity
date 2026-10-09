using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;                   // Para usar Photon PUN Network
using TMPro;                        // Para usar TextMeshPro
using UnityEngine.UI;               // Para usar UI
using UnityEngine.SceneManagement;  // Para cargar escenas

// Permite al jugador introducir su nombre y conectarse al servidor de Photon, manejando la conexión inicial. 
public class ConnectToServer : MonoBehaviourPunCallbacks
{
    // Referencia al campo de texto donde el jugador escribe su nombre de usuario
    public TMP_InputField usernameInput;
    // Referencia al texto del botón (para cambiarlo a "Conectando...")
    public TMP_Text buttonText; 

    // Se llama cuando el jugador hace clic en el botón de "Conectar"
    public void OnClickConnect()
    {
        // Verficamos que el nombre de usuario no esté vacío
        if (usernameInput.text.Length >= 1)
        {
            // Guardamos el nombre del jugador en Photon (será su NickName)
            PhotonNetwork.NickName = usernameInput.text;
            // Cambiamos el texto del botón para indicar que se está conectando
            buttonText.text = "Conectando...";
            // Esto asegura que todos los jugadores carguen la misma escena cuando se inice el juego
            PhotonNetwork.AutomaticallySyncScene = true;
            //PhotonNetwork.AutomaticallySyncScene = false; // Para poder usar una pantalla de carga en el lobby
            // Iniciamos la conexión al servidor usando la configuración de Photon (desde el archivo de settings)
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // Este método se llama automáticamente cuando el jugador se conecta al servidor maestro
    public override void OnConnectedToMaster()
    {
        // Cargamos la escena del "Lobby" (donde los jugadores se reúnen antes de jugar)
        SceneManager.LoadScene("Lobby");
    }

    public void VolverInicio()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene("Inicio");
    }
}