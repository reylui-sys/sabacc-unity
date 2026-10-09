using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditosManager : MonoBehaviour
{
    public GameObject panelSonidos;
    public GameObject panelDecoracion;
    public GameObject panelPersonaje;

    void Start()
    {
        MostrarSonidos();
    }
    
    // Función para mostrar el panel de sonidos
    public void MostrarSonidos()
    {
        if (panelSonidos != null)
        {
            panelSonidos.SetActive(true);
            panelDecoracion.SetActive(false); 
            panelPersonaje.SetActive(false); 
        }
    }
    
    // Función para mostrar el panel de decoracion
    public void MostrarDecoracion()
    {
        if (panelDecoracion != null)
        {
            panelSonidos.SetActive(false);
            panelDecoracion.SetActive(true); 
            panelPersonaje.SetActive(false); 
        }
    }
    
    // Función para mostrar el panel de personaje
    public void MostrarPersonaje()
    {
        if (panelPersonaje != null)
        {
            panelSonidos.SetActive(false);
            panelDecoracion.SetActive(false); 
            panelPersonaje.SetActive(true); 
        }
    }

    // Función para volver a la escena de configuracion
    public void Volver()
    {
        SceneManager.LoadScene("Inicio");
    }
}
