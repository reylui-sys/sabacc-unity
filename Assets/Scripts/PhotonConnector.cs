using UnityEngine;
using Photon.Pun;
using TMPro;

public class PhotonConnector : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    public TMP_Text statusText;
    public UnityEngine.UI.Button connectButton;
    
    [Header("Referencias")]
    public NetworkGameController gameController;
    
    void Start()
    {
        connectButton.onClick.AddListener(ConnectToServer);
        statusText.text = "Photon Ready - Presiona Connect";
        
        // Verifica que el GameController existe
        if (gameController == null)
        {
            gameController = FindObjectOfType<NetworkGameController>();
            
            if (gameController == null)
            {
                Debug.LogError("¡No se encontró NetworkGameController en la escena!");
            }
        }
    }
    
    void ConnectToServer()
    {
        PhotonNetwork.ConnectUsingSettings();
        statusText.text = "Conectando...";
        connectButton.interactable = false;
    }
    
    public override void OnConnectedToMaster()
    {
        statusText.text = "Conectado a Master - Uniéndose a sala...";
        
        PhotonNetwork.JoinOrCreateRoom("SabaccTest", new Photon.Realtime.RoomOptions { MaxPlayers = 4 }, Photon.Realtime.TypedLobby.Default);
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"[PhotonConnector] Unido a sala: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"[PhotonConnector] Jugadores en sala: {PhotonNetwork.CurrentRoom.PlayerCount}/4");
        
        statusText.text = $"En sala: {PhotonNetwork.CurrentRoom.Name}\n" +
                          $"Jugadores: {PhotonNetwork.CurrentRoom.PlayerCount}/4\n" +
                          $"Eres: {(PhotonNetwork.IsMasterClient ? "MASTER" : "CLIENTE")}";
        
        // Notifica al GameController
        if (gameController != null)
        {
            gameController.OnPhotonJoinedRoom();
        }
        else
        {
            Debug.LogError("[PhotonConnector] gameController es NULL!");
        }
    }
    
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"[PhotonConnector] Nuevo jugador: {newPlayer.NickName}");
        statusText.text = $"En sala: {PhotonNetwork.CurrentRoom.Name}\n" +
                          $"Jugadores: {PhotonNetwork.CurrentRoom.PlayerCount}/4\n" +
                          $"Eres: {(PhotonNetwork.IsMasterClient ? "MASTER" : "CLIENTE")}";
    }
    
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        statusText.text = $"Jugadores: {PhotonNetwork.CurrentRoom.PlayerCount}/4";
    }
}