using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Inicio : MonoBehaviour
{
    public AudioClip musicClip;

    /*
    public void Start()
    {
        // Iniciar musica de fondo
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuMusic();
        }
    }
    */

    // Función para mostrar el panel de reglas
    public void MostrarConfig()
    {
        SceneManager.LoadScene("Configuracion");
    }
    
    // Función para mostrar el panel de reglas
    public void Jugar()
    {
        SceneManager.LoadScene("ConnectToServer");
    }

    public void Tutorial()
    {
        //SceneManager.LoadScene("Tutorial");
        AudioManager.Instance.LoadSceneWithMusicFade("Tutorial");
    }

    public void Creditos()
    {
        SceneManager.LoadScene("Creditos");
    }

    public void Salir()
    {
        // Solo funciona en builds (no en el Editor)
        Application.Quit();
    }
}
