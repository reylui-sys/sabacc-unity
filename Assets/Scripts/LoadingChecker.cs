using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;  
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class LoadingChecker : MonoBehaviour
{
    public GameObject menu; 
    
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"¡{PhotonNetwork.NickName} está en MainScene! PlayerCount: {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");
    }

    public void CargarReglas()
    {
        SceneManager.LoadScene("Reglas");
    }

    public void CargarConfiguracion()
    {
        SceneManager.LoadScene("Configuracion");
    }

    public void CargarLobby()
    {
        SceneManager.LoadScene("Lobby");
    }

    public void Volver()
    {
        if (menu != null)
        {
            menu.SetActive(false); 
        }
        // Reanudar el juego si se había pausado
        Time.timeScale = 1f;
    }

    public void AbrirMenu()
    {
        if (menu != null)
        {
            menu.SetActive(true);
        }
        // Pausar el juego
        Time.timeScale = 0f;
    }
}
