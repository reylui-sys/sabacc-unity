using System.Collections;
using UnityEngine;
using Photon.Pun;

// PlayerSpawner para Sabacc.
// Spawnea avatares 3D de los jugadores y notifica al NetworkGameController.
public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Prefabs de jugadores")]
    public GameObject[] playerPrefabs;  // Lista de prefabs de jugadores (por avatar seleccionado)
    
    [Header("Spawn Points")]
    public Transform[] spawnPoints;     // Puntos donde spawnean los jugadores
    
    [Header("Cámaras de jugadores")]
    public Camera[] playerCameras;      // Cámaras preconfiguradas (una por jugador)
    
    [Header("Cámaras extra")]
    public Camera startCamera;          // Cámara inicial (vista del mazo)
    public Camera endCamera;            // Cámara final (revelación)
    
    [Header("Referencias")]
    public NetworkGameController gameController;  // Referencia al controlador del juego

    private void Start()
    {
        StartCoroutine(SpawnPlayerDelayed());
    }

    IEnumerator SpawnPlayerDelayed()
    {
        // Esperamos un frame para asegurar que Photon esté listo
        yield return new WaitForEndOfFrame();

        // Validar spawn points
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] ¡Faltan spawn points!");
            yield break;
        }

        // Validar cámaras
        if (playerCameras == null || playerCameras.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] ¡Faltan cámaras de jugadores!");
            yield break;
        }

        // Validar prefabs
        if (playerPrefabs == null || playerPrefabs.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] ¡Faltan prefabs de jugadores!");
            yield break;
        }

        // Buscar GameController si no está asignado
        if (gameController == null)
        {
            gameController = FindObjectOfType<NetworkGameController>();
            if (gameController == null)
            {
                Debug.LogError("[PlayerSpawner] ¡No se encontró NetworkGameController!");
                yield break;
            }
        }

        // Calcular el asiento del jugador local: su posición en PlayerList (ordenada por ActorNumber).
        // Es el mismo orden con el que NetworkGameController crea los asientos del GameState,
        // así que spawn point, cámara y área de mano coinciden. Antes se usaba "ActorNumber - 1",
        // que se descuadra si alguien entró y salió de la sala antes de empezar.
        int seatIndex = System.Array.FindIndex(PhotonNetwork.PlayerList,
            p => p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
        if (seatIndex < 0)
        {
            Debug.LogError("[PlayerSpawner] El jugador local no aparece en PlayerList");
            seatIndex = 0;
        }
        int spawnIndex = seatIndex % spawnPoints.Length;
        
        Debug.Log($"[PlayerSpawner] Jugador local: {PhotonNetwork.LocalPlayer.NickName}");
        Debug.Log($"[PlayerSpawner] ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
        Debug.Log($"[PlayerSpawner] SpawnIndex: {spawnIndex}");
        Debug.Log($"[PlayerSpawner] IsMasterClient: {PhotonNetwork.IsMasterClient}");

        // Desactivar todas las cámaras de jugadores
        foreach (Camera cam in playerCameras)
        {
            if (cam != null)
                cam.enabled = false;
        }
        
        if (startCamera != null)
            startCamera.enabled = false;
        if (endCamera != null)
            endCamera.enabled = false;

        // Activar la cámara inicial (startCamera) para ver el reparto
        if (startCamera != null)
        {
            startCamera.enabled = true;
            Debug.Log("[PlayerSpawner] Activada startCamera");
        }

        // Obtener el índice del avatar seleccionado (si existe)
        int avatarIndex = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("playerAvatar"))
        {
            avatarIndex = (int)PhotonNetwork.LocalPlayer.CustomProperties["playerAvatar"];
        }
        
        // Asegurar que el índice del avatar es válido
        avatarIndex = Mathf.Clamp(avatarIndex, 0, playerPrefabs.Length - 1);

        // Spawnear el avatar del jugador
        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject playerPrefab = playerPrefabs[avatarIndex];

        if (playerPrefab != null && spawnPoint != null)
        {
            Debug.Log($"[PlayerSpawner] Spawneando prefab '{playerPrefab.name}' en posición {spawnIndex}");
            PhotonNetwork.Instantiate(playerPrefab.name, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] Prefab o SpawnPoint es null, no se spawneó avatar");
        }

        // Esperar un momento antes de notificar al GameController
        yield return new WaitForSeconds(0.5f);
        
        // Notificar al GameController que estamos listos
        Debug.Log("[PlayerSpawner] Notificando a GameController...");
        gameController.OnPlayerReady();
    }
}
