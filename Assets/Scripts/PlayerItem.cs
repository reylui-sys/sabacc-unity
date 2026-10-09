using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon; // Para usar Hashtable (propiedades personalizadas)

// Representa visualmente a un jugador en la lista (UI) del lobby (con nombre, avatar y flechas para cambiarlo). 
public class PlayerItem : MonoBehaviourPunCallbacks 
{
    public TMP_Text playerName;         // Muestra el nombre del jugador
    Image backgroundImage;              // Fondo del item (para resaltar al jugador local)
    public Color highlightColor;        // Color para resaltar al jugador local
    public GameObject leftArrowButton;  // Botón para cambiar avatar a la izquierda
    public GameObject rightArrowButton; // Botón para cambiar avatar a la derecha

    // Propiedades personalizadas del jugador (sincronizadas con Photon)
    ExitGames.Client.Photon.Hashtable playerProperties = new ExitGames.Client.Photon.Hashtable();
    public Image playerAvatar;          // Imagen que muestra el avatar
    public Sprite[] avatars;            // Lista de avatares disponibles

    Photon.Realtime.Player player;      // Referencia al jugador de Photon

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
    }

    // Configura este UI con la información de un jugador real
    public void SetPlayerInfo(Photon.Realtime.Player _player)
    {
        playerName.text = _player.NickName;
        player = _player;
        UpdatePlayerItem(player);
    }

    // Aplica estilo especial al jugador local (flechas visibles, fondo coloreado)
    public void ApplyLocalChanges()
    {
        backgroundImage.color = highlightColor;
        leftArrowButton.SetActive(true);
        rightArrowButton.SetActive(true);
    }
    
    // Cambia el avatar hacia la izquierda (con wrap-around)
    public void OnClickLeftArrow()
    {
        if ((int)playerProperties["playerAvatar"] == 0)
        {
            playerProperties["playerAvatar"] = avatars.Length - 1;
        }
        else
        {
            playerProperties["playerAvatar"] = (int)playerProperties["playerAvatar"] - 1;
        }
        PhotonNetwork.SetPlayerCustomProperties(playerProperties);
    }

    // Cambia el avatar hacia la derecha (con wrap-around)
    public void OnClickRightArrow() 
    {
        if ((int)playerProperties["playerAvatar"] == avatars.Length - 1)
        {
            playerProperties["playerAvatar"] = 0;
        }
        else
        {
            playerProperties["playerAvatar"] = (int)playerProperties["playerAvatar"] + 1;
        }
        PhotonNetwork.SetPlayerCustomProperties(playerProperties);
    }

    // Se llama cuando las propiedades de un jugador cambian (ej: avatar)
    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (player == targetPlayer) 
        {
            UpdatePlayerItem(targetPlayer);
        }
    }

    // Actualiza el avatar visual según las propiedades del jugador
    void UpdatePlayerItem(Photon.Realtime.Player player)
    {
        if (player.CustomProperties.ContainsKey("playerAvatar"))
        {
            //playerAvatar.sprite = avatars[(int)player.CustomProperties["playerAvatar"]];
            //playerProperties["playerAvatar"] = (int)player.CustomProperties["playerAvatar"];
            int avatarIndex = (int)player.CustomProperties["playerAvatar"];
            playerAvatar.sprite = avatars[avatarIndex];
            playerProperties["playerAvatar"] = avatarIndex;
        } else
        {
            // Si no tiene avatar asignado, le damos el 0 y lo guardamos en Photon
            playerProperties["playerAvatar"] = 0;
            //  Esto garantiza que cada jugador, incluido el local, siempre tenga la 
            //  propiedad "playerAvatar" configurada en Photon, incluso si nunca tocó las flechas. 
            PhotonNetwork.SetPlayerCustomProperties(playerProperties);
            playerAvatar.sprite = avatars[0];
        }
    }
}
