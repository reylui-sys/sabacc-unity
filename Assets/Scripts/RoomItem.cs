using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Representa una sala en la lista del lobby. 
public class RoomItem : MonoBehaviour
{
    public TMP_Text roomName;       // Muestra el nombre de la sala
    LobbyManager manager;       // Referencia al gestor del lobby
        
    private void Start()
    {
        // Buscamos el LobbyManager en la escena (solo una vez)
        manager = FindObjectOfType<LobbyManager>();
    }

    // Configura el nombre mostrado de la sala
    public void SetRoomName(string _roomName)
    {
        roomName.text = _roomName;
    }

    // Al hacer clic, intentamos unirnos a esta sala
    public void OnClickItem()
    {
        manager.JoinRoom(roomName.text);
    }
}