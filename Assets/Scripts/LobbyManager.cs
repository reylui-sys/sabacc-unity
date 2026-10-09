using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;      // Para acceder a salas y jugadores
using TMPro;                // Para usar TextMeshPro
//using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Gestiona la interfaz del lobby: la sala de espera (lobby), permite crear/unirse a salas y muestra la lista de jugadores. 
public class LobbyManager : MonoBehaviourPunCallbacks
{
    public TMP_InputField roomInputField; // Campo para escribir el nombre de la sala
    public GameObject lobbyPanel;         // Panel principal del lobby
    public GameObject roomPanel;          // Panel que se muestra cuando estás en una sala
    public TMP_Text roomName;             // Texto que muestra el nombre de la sala actual

    public RoomItem roomItemPrefab;       // Prefab para mostrar una sala en la lista
    List<RoomItem> roomItemsList = new List<RoomItem>(); // Lista para gestionar los items de sala
    public Transform contentObject;    // Contenedor donde se instancian las salas

    public float timeBetweenUpdates = 1.5f; // Intervalo para actualizar la lista de salas
    float nextUpdateTime;                   // Controla cuándo se actualiza

    public PlayerItem playerItemPrefab;    // Prefab para mostrar un jugador en la lista
    List<PlayerItem> playerItemsList = new List<PlayerItem>(); // Lista de jugadores en UI
    public Transform playerItemParent;     // Contenedor donde se instancian los jugadores

    public GameObject playButton;          // Botón para iniciar el juego

    // Al iniciar, nos unimos al lobby (lista de salas públicas)
    private void Start()
    {
        PhotonNetwork.JoinLobby();
    }

    /*
    private PhotonView photonView; 

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            Debug.LogError("LobbyManager requiere un PhotonView en el mismo GameObject.");
        }
    }
    */

    // Crea una nueva sala con el nombre introducido
    public void OnClickCreate()
    {
        if (roomInputField.text.Length >= 1)
        {
            // Creamos una sala con nombre, máximo 4 jugadores, y sincronizamos propiedades
            PhotonNetwork.CreateRoom(roomInputField.text, new RoomOptions() { MaxPlayers = 4, BroadcastPropsChangeToAll = true });
        }
    }

    // Se llama cuando el jugador entra a una sala
    public override void OnJoinedRoom()
    {
        lobbyPanel.SetActive(false);    // Ocultamos el lobby
        roomPanel.SetActive(true);      // Mostramos el panel de la sala
        roomName.text = PhotonNetwork.CurrentRoom.Name; // Actualizamos el nombre de la sala en UI
        UpdatePlayerList();             // Actualizamos la lista de jugadores
    }

    // Se llama cuando la lista de salas públicas cambia
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // Actualizamos la lista con un pequeño retraso para evitar sobrecargar la UI
        if (Time.time >= nextUpdateTime)
        {
            UpdateRoomList(roomList);
            nextUpdateTime = Time.time + timeBetweenUpdates;
        }
    }

    // Actualiza la lista visual de salas en el lobby
    void UpdateRoomList(List<RoomInfo> list)
    {
        // Destruimos los items anteriores
        foreach (RoomItem item in roomItemsList)
        {
            Destroy(item.gameObject);
        }
        roomItemsList.Clear();

        // Creamos un nuevo item por cada sala visible
        foreach (RoomInfo room in list)
        {
            if (room.RemovedFromList)
                continue; // Ignoramos salas eliminadas
            
            RoomItem newRoom = Instantiate(roomItemPrefab, contentObject);
            newRoom.SetRoomName(room.Name);
            roomItemsList.Add(newRoom);
        }
    }

    // Unirse a una sala por nombre
    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    // Salir de la sala actual
    public void OnClickLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    // Se llama cuando el jugador sale de una sala
    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    // Si nos desconectamos y volvemos al servidor maestro, volvemos al lobby
    public override void OnConnectedToMaster()
    {
       PhotonNetwork.JoinLobby(); 
    }

    /*
    // No ordena los jugadores, por host y despues por orden de llegada por usar Dictionary en CurrentRoom.Players
    // Actualiza la lista de jugadores en la UI de la sala
    void UpdatePlayerList()
    {
        // Limpiamos la lista anterior
        foreach (PlayerItem item in playerItemsList)
        {
            Destroy(item.gameObject);
        }
        playerItemsList.Clear();

        if (PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        // Creamos un item por cada jugador en la sala
        foreach (KeyValuePair<int, Photon.Realtime.Player> player in PhotonNetwork.CurrentRoom.Players)
        {
            PlayerItem newPlayerItem = Instantiate(playerItemPrefab, playerItemParent);
            newPlayerItem.SetPlayerInfo(player.Value);
                        
            // Si este jugador es el local, aplicamos estilo especial (flechas, color)
            if (player.Value == PhotonNetwork.LocalPlayer)
            {
                newPlayerItem.ApplyLocalChanges();
            }

            playerItemsList.Add(newPlayerItem);
        }
    }*/

    /// Actualiza la lista visual de jugadores en el lobby.
    /// Esto asegura que todos los jugadores en la sala aparezcan en la UI,
    /// siempre en el mismo orden para todos los participantes (primero el host, luego los demás por orden de entrada).
    void UpdatePlayerList()
    {
        // Eliminamos los ítems anteriores de la lista para evitar duplicados
        // Recorremos todos los "PlayerItem" que ya están en la lista
        foreach (PlayerItem item in playerItemsList)
        {
            // Destruimos su GameObject en la escena
            Destroy(item.gameObject);
        }
        // Luego vaciamos la lista interna (para empezar desde cero)
        playerItemsList.Clear();

        // Verificamos que realmente estamos en una sala
        // Si no estamos en ninguna sala (por ejemplo, si salimos), no hay nada que mostrar
        if (PhotonNetwork.CurrentRoom == null) 
        {
            return; // Salimos de la función
        }

        // Recorremos todos los jugadores de la sala, en orden garantizado
        // PhotonNetwork.PlayerList es una lista que Photon nos da ya ordenada:
        // - El primer jugador es siempre el que creó la sala (el "host" o "Master Client")
        // - Los siguientes están en el orden en que entraron a la sala
        // Esto asegura que todos los jugadores vean la misma lista en el mismo orden
        foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        {
            // Creamos un nuevo ítem visual para este jugador
            PlayerItem newPlayerItem = Instantiate(playerItemPrefab, playerItemParent);
            // Configuramos este ítem con la información del jugador real (nombre, avatar, etc.)
            newPlayerItem.SetPlayerInfo(player); 

            // Si este jugador es el local, aplicamos estilo especial
            if (player == PhotonNetwork.LocalPlayer)
            {
                newPlayerItem.ApplyLocalChanges();
            }
        
            // Finalmente, guardo este ítem en mi lista interna para poder destruirlo después si es necesario
            playerItemsList.Add(newPlayerItem);
        }
    }

    // Se llama cuando un jugador entra a la sala
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdatePlayerList();
    }

    // Se llama cuando un jugador sale de la sala
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        UpdatePlayerList();
    }

    // Cada frame: si eres el anfitrión y hay al menos 2 jugadores, muestra el botón "Jugar"
    private void Update()
    {
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            playButton.SetActive(true);
        }
        else
        {
            playButton.SetActive(false);
        }
    }

    /*
    // Inicia el juego: todos los jugadores cargan la escena principal
    public void OnClickPlayButton()
    {
        PhotonNetwork.LoadLevel("MainScene");
    }
    */

    private bool _startingGame = false; // Evita lanzar la partida dos veces con un doble clic

    public void OnClickPlayButton()
    {
        if (_startingGame) return;

        if (PhotonNetwork.IsMasterClient && photonView != null)
        {
            _startingGame = true;

            // Cerrar la sala: nadie puede entrar ni verla una vez repartidos los asientos.
            // Si entrase alguien a mitad de partida no tendría asiento en el GameState.
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;

            // Notificar a todos que comience el fade y luego la carga
            photonView.RPC(nameof(RPC_StartGameTransition), RpcTarget.All);
        }
    }

    [PunRPC]
    void RPC_StartGameTransition()
    {
        // Todos los jugadores hacen fade out
        AudioManager.Instance.FadeOutMusic(0.8f);

        // Solo el host carga la escena después del fade
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(LoadMainSceneAfterFade());
        }
    }

    IEnumerator LoadMainSceneAfterFade()
    {
        yield return new WaitForSeconds(0.8f);
        PhotonNetwork.LoadLevel("MainScene");
    }

    public void VolverConnect()
    {
        // Si estamos en una sala, primero salimos
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            // Esperamos a que se complete OnLeftRoom antes de desconectar o cargar otra escena
            // Pero como queremos ir directamente a ConnectToServer, podemos desconectar completamente
        }

        // Desconectamos del servidor maestro para limpiar el estado
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        // Cargamos la escena de conexión
        SceneManager.LoadScene("ConnectToServer");
    }

    /*
    public void OnClickPlayButton()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (photonView != null)
            {
                // Notificamos a todos que empieza la carga
                photonView.RPC("StartLoadingScene", RpcTarget.All);
            }
            else
            {
                Debug.LogError("photonView no está asignado en LobbyManager.");
            }
        }
    }

    [PunRPC]
    void StartLoadingScene()
    {
        Debug.Log($"[RPC] {PhotonNetwork.NickName} recibió StartLoadingScene");
        LoadingManager.Instance.ShowLoading();
        StartCoroutine(LoadMainSceneAsync());
    }

    IEnumerator LoadMainSceneAsync()
    {
        Debug.Log($"[Carga] {PhotonNetwork.NickName} iniciando carga asíncrona...");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainScene");
        asyncLoad.allowSceneActivation = true; // o false si quieres controlar el "activate" manualmente

        yield return asyncLoad;

        Debug.Log($"[Carga] {PhotonNetwork.NickName} terminó de cargar MainScene");
    }
    */
}