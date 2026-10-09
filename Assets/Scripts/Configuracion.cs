using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Configuracion : MonoBehaviour
{
    public GameObject panelReglas;
    public GameObject panelInicio; 
    public GameObject panelAudio;

    // Función para mostrar el panel de reglas
    public void MostrarReglas()
    {
        if (panelReglas != null && panelInicio != null)
        {
            panelReglas.SetActive(true);
            panelInicio.SetActive(false); // Ocultamos el panel inicial
        }
    }
    
    // Función para volver al panel inicial
    public void VolverInicio()
    {
        if (panelReglas != null && panelInicio != null && panelAudio != null)
        {
            panelReglas.SetActive(false);
            panelAudio.SetActive(false);
            panelInicio.SetActive(true); // Activamos el panel inicial
        }
    }
    
    // Función para volver a la escena de inicio
    public void Volver()
    {
        SceneManager.LoadScene("Inicio");
    }

    public void MostrarAudio()
    {
        if (panelAudio != null && panelInicio != null && panelReglas != null)
        {
            panelAudio.SetActive(true);
            panelInicio.SetActive(false); // Ocultamos el panel inicial
            panelReglas.SetActive(false);
        }
    }
}


