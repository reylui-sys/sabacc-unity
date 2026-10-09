using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.SceneManagement;

public class NetworkGameController : MonoBehaviourPunCallbacks
{
    [Header("UI - Textos")]
    public TMP_Text handPotText;
    public TMP_Text sabaccPotText;
    public TMP_Text gameStateText;
    public TMP_Text phaseText;
    public TMP_Text currentPlayerText;

    [Header("Camaras de jugadores")]
    public Camera player1Camera;
    public Camera player2Camera;
    public Camera player3Camera;
    public Camera player4Camera;

    [Header("Camaras extra")]
    public Camera startCamera;
    public Camera endCamera;
    public Camera interferenceFieldCamera;

    [Header("UI - areas de mano")]
    public Transform player1HandArea;
    public Transform player2HandArea;
    public Transform player3HandArea;
    public Transform player4HandArea;

    [Header("UI - areas del juego")]
    public Transform deckArea;
    public Transform discardArea;

    [Header("UI - Textos")]
    public TMP_Text player1CreditsText;
    public TMP_Text player2CreditsText;
    public TMP_Text player3CreditsText;
    public TMP_Text player4CreditsText;
    public TMP_Text player1HandValueText;
    public TMP_Text player2HandValueText;
    public TMP_Text player3HandValueText;
    public TMP_Text player4HandValueText;

    [Header("UI - Botones")]
    public UnityEngine.UI.Button drawButton;
    public UnityEngine.UI.Button standButton;
    public UnityEngine.UI.Button discardButton;
    public UnityEngine.UI.Button protectButton;
    public UnityEngine.UI.Button unprotectButton;

    [Header("UI - Sistema de Apuestas")]
    public UnityEngine.UI.Button betButton;
    public UnityEngine.UI.Button checkButton;
    public UnityEngine.UI.Button callButton;
    public UnityEngine.UI.Button foldButton;
    public TMP_InputField betInputField;
    public TMP_Text currentBetText;
    public GameObject bettingPanel;

    [Header("UI - Campo de Interferencia")]
    public Transform player1InterferenceField;
    public Transform player2InterferenceField;
    public Transform player3InterferenceField;
    public Transform player4InterferenceField;
    public int maxProtectedCards = 2;

    [Header("UI - Transicion")]
    public GameObject transitionPanel;
    //public TMP_Text transitionText;
    public UnityEngine.UI.Button continueButton;

    [Header("UI")]
    public GameObject mainUIPanel;

    [Header("Configuracion")]
    public CardPrefabMap cardPrefabMap;
    public int startingCredits = 100;
    public int initialBet = 10;
    public float handCardSpacing = 0.8f;
    
    [Header("Configuracion - Apuestas")]
    public int callPenalty = 10;
    public int bombedOutPenalty = 50;
    public int foldPenaltyFirstBetting = 10;
    
    [Header("Configuracion - Limites")]
    public int maxCardsInHand = 9;

    [Header("Paneles")] 
    public GameObject panelReglas;
    public GameObject panelMenu;
    public GameObject panelAudio;
    public GameObject panelFinal;
    public TMP_Text panelFinalText; 

    private PhotonView photonView;
    private int localPlayerIndex = -1;
    private GameState gameState;
    private GameLogic gameLogic;

    private List<Transform> _handAreas = new List<Transform>();
    private List<TMP_Text> _creditsTexts = new List<TMP_Text>();
    private List<TMP_Text> _handValueTexts = new List<TMP_Text>();
    private List<List<GameObject>> _cardInstances = new List<List<GameObject>>();
    private Dictionary<GameObject, Vector3> _originalCardPositions = new Dictionary<GameObject, Vector3>();
    private List<Camera> _playerCameras = new List<Camera>();
    private List<GameObject> _deckCardInstances = new List<GameObject>();
    private List<GameObject> _discardPileInstances = new List<GameObject>();

    private HandPositionCalculator _positionCalculator;
    private int _selectedCardIndex = -1;

    private List<Transform> _interferenceFields = new List<Transform>();
    private List<List<GameObject>> _protectedCardInstances = new List<List<GameObject>>();
    private int _playersWhoStood = 0;
    private bool _isAnimating = false;
    private readonly HashSet<int> _readyActors = new HashSet<int>(); // ActorNumbers que han avisado de que estan listos (sin duplicados)
    private bool _roundStarted = false;
    
    private int _bettingRoundStarter = 0;

    // Comandos: reglas que valida CommandValidator y si tenemos un comando "en vuelo"
    private RulesConfig _rules;
    private bool _awaitingCommandResult = false; // true desde que enviamos un comando hasta que el Master lo acepta o rechaza

    private const int START_CAMERA_INDEX = 4;
    private const int END_CAMERA_INDEX = 5;
    private const int INTERFERENCE_CAMERA_INDEX = 6;

    void Start()
    {
        photonView = GetComponent<PhotonView>(); // Obtener componente PhotonView

        // Verificar que el PhotonView este asignado
        if (photonView == null)
        {
            Debug.LogError("Necesitas PhotonView component!");
            return;
        }

        _rules = new RulesConfig
        {
            MaxCardsInHand = maxCardsInHand,
            MaxProtectedCards = maxProtectedCards,
            FoldPenaltyFirstBetting = foldPenaltyFirstBetting
        };

        SetupReferences(); // Configurar referencias a UI y otros objetos
        ValidateReferences(); // Validar referencias asignadas
        SetupBettingButtons(); // Configurar botones de apuestas
        DisableAllButtons(); // Deshabilitar botones inicialmente

        // Iniciar musica de fondo
        if (AudioManager.Instance != null)
        {
            //AudioManager.Instance.PlayBackgroundMusic();
            AudioManager.Instance.FadeInMusic(AudioManager.Instance.backgroundMusic, 0.8f);
        }

        // Verificar que estamos en una sala de Photon
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("[NetworkGameController] No estamos en una sala de Photon!");
            gameStateText.text = "Error: No conectado a sala";
            return;
        }
        
        // El indice (asiento) del jugador local NO se calcula aqui: se resuelve
        // por PlayerId cuando se crea el GameState (InitializeGame / RPC_GameInitialized).
        // Antes se usaba "ActorNumber - 1", que falla si alguien entro y salio de la sala.

        gameStateText.text = "Cargando juego..."; // Mensaje inicial

        // Activa la camara inicial
        SwitchToCamera(START_CAMERA_INDEX);
    }
    
    void SetupBettingButtons()
    {
        // Conecta los botones de apuestas a sus metodos
        if (betButton != null)
            betButton.onClick.AddListener(OnBetButton);
        if (checkButton != null)
            checkButton.onClick.AddListener(OnCheckButton);
        if (callButton != null)
            callButton.onClick.AddListener(OnCallButton);
        if (foldButton != null)
            foldButton.onClick.AddListener(OnFoldButton);
            
        // Oculta el panel de apuestas inicialmente
        if (bettingPanel != null)
            bettingPanel.SetActive(false);
    }

    // Llamado por PlayerSpawner cuando el jugador esta listo.
    public void OnPlayerReady()
    {        
        // Todos los jugadores notifican al Master que estan listos (identificados por su ActorNumber)
        photonView.RPC("RPC_PlayerReadyToStart", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    // RPC llamado en el Master cuando un jugador esta listo
    [PunRPC]
    void RPC_PlayerReadyToStart(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Solo el Master procesa esto

        // Un HashSet evita contar dos veces al mismo jugador si avisa mas de una vez
        _readyActors.Add(actorNumber);

        // Inicializar el juego si no esta inicializado
        if (gameState == null)
        {
            InitializeGame();
        }

        // Verificar si todos los jugadores estan listos
        if (_readyActors.Count >= PhotonNetwork.CurrentRoom.PlayerCount && !_roundStarted)
        {
            _roundStarted = true; // Marcar que la ronda ha comenzado

            // Enviar inicializacion a todos: nombres + ActorNumbers en orden de asiento
            string[] playerNames = new string[gameState.Players.Count];
            int[] actorNumbers = new int[gameState.Players.Count];
            for (int i = 0; i < gameState.Players.Count; i++)
            {
                playerNames[i] = gameState.Players[i].Name;
                actorNumbers[i] = gameState.Players[i].Id.Value;
            }

            photonView.RPC("RPC_GameInitialized", RpcTarget.All, playerNames, actorNumbers, startingCredits); // Enviar inicializacion

            Invoke(nameof(StartNewRound), 1.5f); // Iniciar nueva ronda despues de un delay
        }
    }

    // Llamado automaticamente por Photon cuando un jugador se une a la sala
    public void OnPhotonJoinedRoom()
    {
        OnPlayerReady(); // Notificar que el jugador esta listo
    }

    // Configurar referencias a UI y otros objetos
    void SetupReferences()
    {
        _handAreas.Add(player1HandArea);
        _handAreas.Add(player2HandArea);
        _handAreas.Add(player3HandArea);
        _handAreas.Add(player4HandArea);

        _creditsTexts.Add(player1CreditsText);
        _creditsTexts.Add(player2CreditsText);
        _creditsTexts.Add(player3CreditsText);
        _creditsTexts.Add(player4CreditsText);

        _handValueTexts.Add(player1HandValueText);
        _handValueTexts.Add(player2HandValueText);
        _handValueTexts.Add(player3HandValueText);
        _handValueTexts.Add(player4HandValueText);

        _playerCameras.Add(player1Camera);
        _playerCameras.Add(player2Camera);
        _playerCameras.Add(player3Camera);
        _playerCameras.Add(player4Camera);
        _playerCameras.Add(startCamera);
        _playerCameras.Add(endCamera);
        _playerCameras.Add(interferenceFieldCamera);

        // Inicializar listas de cartas en mano
        for (int i = 0; i < 4; i++)
            _cardInstances.Add(new List<GameObject>());

        _interferenceFields.Add(player1InterferenceField);
        _interferenceFields.Add(player2InterferenceField);
        _interferenceFields.Add(player3InterferenceField);
        _interferenceFields.Add(player4InterferenceField);

        // Inicializar listas de cartas protegidas
        for (int i = 0; i < 4; i++)
            _protectedCardInstances.Add(new List<GameObject>());

        _positionCalculator = new LinearHandPositionCalculator(handCardSpacing); // Configurar calculadora de posiciones de mano

        drawButton.onClick.AddListener(OnDrawCard);
        standButton.onClick.AddListener(OnStand);
        discardButton.onClick.AddListener(OnDiscardCard);
        continueButton.onClick.AddListener(OnContinueFromTransition);

        // Conectar botones de proteccion si existen
        if (protectButton != null)
            protectButton.onClick.AddListener(OnProtectCard);
        if (unprotectButton != null)
            unprotectButton.onClick.AddListener(OnUnprotectCard);

        // Deshabilitar botones inicialmente
        discardButton.interactable = false;
        if (protectButton != null)
            protectButton.interactable = false;
        if (unprotectButton != null)
            unprotectButton.interactable = false;
        transitionPanel.SetActive(false);
    }

    // Validar que todas las referencias estan asignadas
    void ValidateReferences()
    {
        bool hasErrors = false; // Bandera para errores

        // Validar areas de mano
        for (int i = 0; i < _handAreas.Count; i++)
        {
            // Verificar si el area de mano esta asignada
            if (_handAreas[i] == null)
            {
                hasErrors = true; // Marcar error
            }
        }

        // Validar areas del juego
        if (deckArea == null)
        {
            hasErrors = true;
        }

        // Validar textos de creditos
        if (cardPrefabMap == null)
        {
            hasErrors = true;
        }

        // Validar camaras de jugadores
        for (int i = 0; i < _playerCameras.Count; i++)
        {
            // Verificar si la camara esta asignada
            if (_playerCameras[i] == null)
            {
                Debug.LogWarning($"[NetworkGameController] Camara {i} no esta asignada"); // Advertencia
            }
        }

        // Validar AnimationManager
        if (AnimationManager.Instance == null)
        {
            hasErrors = true;
        }
    }

    // RPC llamado en el Master cuando un cliente indica que esta listo
    // (actualmente no se invoca desde ningun sitio; se conserva para una futura reconexion)
    [PunRPC]
    void RPC_ClientReady(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Solo el Master procesa esto

        // Enviar estado del juego al cliente que se ha unido
        if (gameState != null)
        {
            string[] playerNames = new string[gameState.Players.Count];
            int[] actorNumbers = new int[gameState.Players.Count];
            for (int i = 0; i < gameState.Players.Count; i++)
            {
                playerNames[i] = gameState.Players[i].Name;
                actorNumbers[i] = gameState.Players[i].Id.Value;
            }
            
            // Buscar al jugador por su ActorNumber (no por indice)
            Photon.Realtime.Player targetPlayer = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

            // Enviar estado del juego al jugador objetivo
            if (targetPlayer != null)
            {
                photonView.RPC("RPC_GameInitialized", targetPlayer, playerNames, actorNumbers, startingCredits); // Enviar inicializacion
            }
            
            if (gameState.CurrentRound > 0 || gameState.Players[0].Hand.GetCount() > 0)
            {
                string[] cardIds = SerializeAllHands(); // Serializar manos de todos los jugadores

                if (targetPlayer != null)
                {
                    // Enviar estado del juego actual al jugador objetivo
                    photonView.RPC("RPC_SyncGameState", targetPlayer, 
                        gameState.HandPot, gameState.SabaccPot, cardIds, 
                        gameState.CurrentPlayerIndex, (int)gameState.CurrentPhase);
                }
            }
            else if (PhotonNetwork.CurrentRoom.PlayerCount >= 2 && gameState.CurrentRound == 0)
            {
                CancelInvoke(nameof(StartNewRound)); // Cancelar invocacion previa
                Invoke(nameof(StartNewRound), 1.5f); // Iniciar nueva ronda despues de un delay
            }
        }
    }

    // Serializar las manos de todos los jugadores en un array de IDs de cartas
    [PunRPC]
    void RPC_SyncGameState(int handPot, int sabaccPot, string[] cardIds, int currentPlayerIdx, int currentPhase)
    {
        // Asegurar que gameState existe
        if (gameState == null)
        {
            Debug.LogError("[RPC_SyncGameState] gameState es NULL!");
            return;
        }
        
        gameState.HandPot = handPot; // Actualizar bote de mano
        gameState.SabaccPot = sabaccPot; // Actualizar bote de sabacc
        gameState.CurrentPlayerIndex = currentPlayerIdx; // Actualizar indice del jugador actual
        gameState.CurrentPhase = (GamePhase)currentPhase; // Actualizar fase actual

        foreach (var player in gameState.Players)
        {
            player.Hand.Clear(); // Limpiar manos de todos los jugadores
        }
        
        int playerIdx = 0; // Indice del jugador actual
        foreach (string cardId in cardIds)
        {
            if (cardId == "|") // Separador de jugadores
            {
                playerIdx++; // Incrementar indice del jugador
                continue;
            }
            
            if (playerIdx < gameState.Players.Count)
            {
                var card = SabaccCardDefinitions.GetCardById(cardId); // Obtener carta por ID
                if (card != null) // Verificar que la carta existe
                {
                    gameState.Players[playerIdx].Hand.AddCard(card); // Agregar carta a la mano del jugador
                }
            }
        }
        
        UpdateUI(); // Actualizar UI
        StartCoroutine(SyncVisualsAndContinue()); // Sincronizar visuales y continuar
    }

    // Serializar las manos de todos los jugadores en un array de IDs de cartas
    IEnumerator SyncVisualsAndContinue()
    {
        _isAnimating = true; // Marcar que se esta animando
        CleanupAllCards(); // Limpiar todas las cartas visuales

        if (gameState.CurrentPhase == GamePhase.Reveal) // Si estamos en fase de revelacion
        {
            SwitchToCamera(END_CAMERA_INDEX); // Cambiar a camara final
        }
        else
        {
            SwitchToCamera(START_CAMERA_INDEX); // Cambiar a camara inicial
        }
        
        CreateVisualDeck(); // Crear deck visual

        yield return new WaitForSeconds(0.5f); // Esperar un momento

        for (int i = 0; i < gameState.Players.Count; i++) // Para cada jugador
        {
            var player = gameState.Players[i]; // Obtener jugador actual
            for (int j = 0; j < player.Hand.GetCount(); j++) // Para cada carta en la mano del jugador
            {
                SabaccCard card = player.Hand.GetCards()[j]; // Obtener carta actual
                Vector3 targetPos = _positionCalculator.CalculatePosition(_handAreas[i], j, player.Hand.GetCount()); // Calcular posicion objetivo

                GameObject prefab = cardPrefabMap.GetPrefab(card.GetCardId()); // Obtener prefab de la carta
                if (prefab != null) 
                {
                    GameObject instance = Instantiate(prefab, targetPos, _handAreas[i].rotation); // Instanciar carta
                    instance.transform.SetParent(_handAreas[i], true); // Establecer padre
                    instance.transform.localScale = Vector3.one; // Asegurar escala correcta

                    var cardView = instance.GetComponent<CardView>() ?? instance.AddComponent<CardView>(); // Obtener o agregar componente CardView
                    cardView.SetCard(card); // Establecer datos de la carta 
                    cardView.SetFaceUp(false); // Inicialmente boca abajo

                    _cardInstances[i].Add(instance); // Agregar a la lista de cartas del jugador
                }
            }
        }
        
        yield return new WaitForSeconds(0.3f); // Esperar un momento
        SwitchToMyCamera(); // Cambiar a la camara del jugador local

        if (localPlayerIndex >= 0 && localPlayerIndex < _cardInstances.Count) // Verificar indice del jugador local
        {
            yield return AnimationManager.Instance.RevealPlayerHand(_cardInstances[localPlayerIndex]); // Revelar mano del jugador local
        }
        
        _isAnimating = false; // Marcar que la animacion ha terminado
        UpdateUI(); // Actualizar UI

        if (gameState.CurrentPhase == GamePhase.Drawing) 
        {
            ShowTransitionScreen(); // Mostrar pantalla de transicion
        }
        else
        {
            gameStateText.text = "Fase: " + gameState.CurrentPhase;
        }
    }

    // Inicializar el estado del juego
    void InitializeGame()
    { 
        if (!PhotonNetwork.IsMasterClient) // Solo el Master inicializa el juego
        {
            return;
        }
        
        if (gameState != null) // Verificar si el juego ya esta inicializado
        {
            return;
        }
        
        // Un asiento por jugador, en el orden de PhotonNetwork.PlayerList (ordenada por ActorNumber).
        // Ese orden es el mismo que usa PlayerSpawner para elegir spawn point y camara.
        List<Seat> seats = new List<Seat>();
        foreach (var player in PhotonNetwork.PlayerList) // Para cada jugador en la sala
        {
            string nickname = string.IsNullOrEmpty(player.NickName) ? $"Jugador {player.ActorNumber}" : player.NickName; // Obtener nickname o asignar nombre por defecto
            seats.Add(new Seat(new PlayerId(player.ActorNumber), nickname));
        }

        gameState = new GameState(seats, startingCredits); // Crear nuevo estado del juego
        gameLogic = new GameLogic(); // Crear nueva logica del juego
        localPlayerIndex = ResolveLocalPlayerIndex(); // El Master ignora RPC_GameInitialized, asi que lo resuelve aqui

        HideUnusedPlayerUI(seats.Count); // Ocultar UI de jugadores no activos
    }

    // Busca el asiento del jugador local por su PlayerId (ActorNumber)
    int ResolveLocalPlayerIndex()
    {
        int index = gameState.IndexOf(new PlayerId(PhotonNetwork.LocalPlayer.ActorNumber));
        if (index < 0)
        {
            Debug.LogError($"[NetworkGameController] El jugador local (Actor {PhotonNetwork.LocalPlayer.ActorNumber}) no tiene asiento en la partida");
        }
        return index;
    }

    // Indice (asiento) de un jugador de Photon, o -1 si no esta en la partida
    int SeatIndexOf(Photon.Realtime.Player photonPlayer)
    {
        if (gameState == null || photonPlayer == null) return -1;
        return gameState.IndexOf(new PlayerId(photonPlayer.ActorNumber));
    }

    void HideUnusedPlayerUI(int activePlayerCount)
    {
        for (int i = activePlayerCount; i < 4; i++)
        {
            if (i < _handAreas.Count && _handAreas[i] != null)
                _handAreas[i].gameObject.SetActive(false);
            
            if (i < _creditsTexts.Count && _creditsTexts[i] != null)
                _creditsTexts[i].gameObject.SetActive(false);
            
            if (i < _handValueTexts.Count && _handValueTexts[i] != null)
                _handValueTexts[i].gameObject.SetActive(false);
        }
    }

    [PunRPC]
    void RPC_GameInitialized(string[] playerNames, int[] actorNumbers, int credits)
    {
        if (gameState != null)
        {
            return;
        }
        
        // Reconstruir los mismos asientos que el Master, en el mismo orden
        List<Seat> seats = new List<Seat>();
        for (int i = 0; i < playerNames.Length && i < actorNumbers.Length; i++)
        {
            seats.Add(new Seat(new PlayerId(actorNumbers[i]), playerNames[i]));
        }

        gameState = new GameState(seats, credits);
        gameLogic = new GameLogic();
        localPlayerIndex = ResolveLocalPlayerIndex();
        
        HideUnusedPlayerUI(seats.Count);
        
        UpdateUI();
        gameStateText.text = "Juego inicializado. Esperando inicio de ronda...";
    }

    void StartNewRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (panelFinal != null)
            panelFinal.SetActive(false);

        _selectedCardIndex = -1;
        _playersWhoStood = 0;
        gameState.CallerIndex = -1;
        gameState.CurrentHighestBet = 0;

        gameLogic.InitializeNewRound(gameState);

        //
        int activePlayersWithCredits = 0;
        int lastActivePlayerIndex = -1;
        List<int> playersWithoutCredits = new List<int>();
        
        // Identificar quién puede pagar y quién no
        for (int i = 0; i < gameState.Players.Count; i++)
        {
            var player = gameState.Players[i];
            
            if (player.Credits >= initialBet * 2)
            {
                // Jugador puede pagar
                player.DeductCredits(initialBet * 2);
                gameState.HandPot += initialBet;
                gameState.SabaccPot += initialBet;
                activePlayersWithCredits++;
                lastActivePlayerIndex = i;
            }
            else
            {
                // Jugador no puede pagar - marcarlo como retirado
                gameLogic.PlayerFold(player);
                playersWithoutCredits.Add(i);
                Debug.Log($"[StartNewRound] {player.Name} no tiene créditos suficientes (tiene {player.Credits}, necesita {initialBet * 2})");
            }
        }
        
        // Si solo queda 1 o ningún jugador con créditos, terminar la partida
        if (activePlayersWithCredits <= 1)
        {   
            if (activePlayersWithCredits == 1 && lastActivePlayerIndex >= 0)
            {
                // Un solo ganador
                Player winner = gameState.Players[lastActivePlayerIndex];
                int totalWon = gameState.HandPot + gameState.SabaccPot;
                
                photonView.RPC("RPC_GameOverByCredits", RpcTarget.All, lastActivePlayerIndex, totalWon);
            }
            else
            {
                // Nadie tiene créditos suficientes
                photonView.RPC("RPC_GameOverNoWinner", RpcTarget.All);
            }
            return;
        }

        // La partida continúa normalmente
        gameState.CurrentPhase = GamePhase.Dealing;
        gameLogic.DealInitialCards(gameState);

        int[] playerCredits = new int[gameState.Players.Count];
        int[] playerStates = new int[gameState.Players.Count];
        for (int i = 0; i < gameState.Players.Count; i++)
        {
            playerCredits[i] = gameState.Players[i].Credits;
            playerStates[i] = (int)gameState.Players[i].State;
        }

        string[] cardIds = SerializeInitialDeal();
        
        photonView.RPC("RPC_StartNewRound", RpcTarget.All,
            gameState.HandPot, gameState.SabaccPot, cardIds, gameState.DealerIndex, gameState.CurrentPlayerIndex, playerCredits, playerStates);
    }
    
    //
    [PunRPC]
    void RPC_GameOverByCredits(int winnerIndex, int totalWon)
    {
        if (gameState == null || winnerIndex < 0 || winnerIndex >= gameState.Players.Count)
        {
            Debug.LogError($"[RPC_GameOverByCredits] Estado inválido! gameState={gameState}, winnerIndex={winnerIndex}");
            StartCoroutine(SalirYLimpiar());
            return;
        }

        Player winner = gameState.Players[winnerIndex];
        winner.AddCredits(totalWon);
        gameState.HandPot = 0;
        gameState.SabaccPot = 0;
        
        if (AudioManager.Instance != null)
        {
            if (winnerIndex == localPlayerIndex)
                AudioManager.Instance.PlayWin();
            else
                AudioManager.Instance.PlayLose();
            
            AudioManager.Instance.PlayGameOver();
        }
        
        /*
        gameStateText.text = $"{winner.Name} GANA LA PARTIDA!\n" +
                            $"Los demas jugadores no tienen creditos suficientes.\n" +
                            $"Gana {totalWon} creditos totales.\n\n" +
                            "FIN DE LA PARTIDA!";
        */

        string mensaje = $"{winner.Name} GANA LA PARTIDA!\n" +
                     "Los demas jugadores no tienen creditos suficientes.\n" +
                     $"Gana {totalWon} creditos totales.\n" +
                     "FIN DE LA PARTIDA!";

        MostrarPanelFinal(mensaje);
             
        DisableAllButtons();
        ShowBettingControls(false);
        
        StartCoroutine(ReturnToLobbyAfterDelay(8f));
    }
    
    [PunRPC]
    void RPC_GameOverNoWinner()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameOver();
        
        /*
        gameStateText.text = "PARTIDA TERMINADA!\n" +
                            "Ningun jugador tiene creditos suficientes para continuar.";
        */

        string mensaje = "PARTIDA TERMINADA!\n" +
                     "Ningun jugador tiene creditos suficientes para continuar.";

        MostrarPanelFinal(mensaje);
            
        DisableAllButtons();
        ShowBettingControls(false);
        
        StartCoroutine(ReturnToLobbyAfterDelay(8f));
        
    }
    
    IEnumerator ReturnToLobbyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.LeaveRoom();
        
        // Esperar a que se salga de la sala
        while (PhotonNetwork.InRoom)
        {
            yield return null;
        }
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Inicio");
    }

    string[] SerializeInitialDeal()
    {
        List<string> allCards = new List<string>();
        foreach (var player in gameState.Players)
        {
            foreach (var card in player.Hand.GetCards())
            {
                allCards.Add(card.GetCardId());
            }
        }
        return allCards.ToArray();
    }

    [PunRPC]
    void RPC_StartNewRound(int handPot, int sabaccPot, string[] cardIds, int dealerIndex, int currentPlayerIndex, int[] playerCredits, int[] playerStates)
    {
        Debug.Log($"[RPC_StartNewRound] Recibido. HandPot: {handPot}, SabaccPot: {sabaccPot}, Cards: {cardIds.Length}");
        
        // Asegurar que gameState existe
        if (gameState == null)
        {
            Debug.LogWarning("[RPC_StartNewRound] gameState era NULL, intentando inicializar...");
            // Intentar esperar a que se inicialice
            StartCoroutine(WaitForGameStateAndStartRound(handPot, sabaccPot, cardIds, dealerIndex, currentPlayerIndex, playerCredits, playerStates));
            return;
        }

        DoStartNewRound(handPot, sabaccPot, cardIds, dealerIndex, currentPlayerIndex, playerCredits, playerStates);
    }
    
    IEnumerator WaitForGameStateAndStartRound(int handPot, int sabaccPot, string[] cardIds, int dealerIndex, int currentPlayerIndex, int[] playerCredits, int[] playerStates)
    {
        float timeout = 5f;
        float elapsed = 0f;
        
        while (gameState == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (gameState != null)
        {
            Debug.Log("[WaitForGameStateAndStartRound] gameState inicializado, continuando...");
            DoStartNewRound(handPot, sabaccPot, cardIds, dealerIndex, currentPlayerIndex, playerCredits, playerStates);
        }
        else
        {
            Debug.LogError("[WaitForGameStateAndStartRound] Timeout esperando gameState!");
        }
    }
    
    void DoStartNewRound(int handPot, int sabaccPot, string[] cardIds, int dealerIndex, int currentPlayerIndex, int[] playerCredits, int[] playerStates)
    {
        Debug.Log($"[DoStartNewRound] Iniciando ronda. LocalPlayer: {localPlayerIndex}");
        _awaitingCommandResult = false;
        
        _selectedCardIndex = -1;
        _playersWhoStood = 0;
        _isAnimating = false;
        gameState.CallerIndex = -1;
        gameState.CurrentHighestBet = 0;

        if (panelFinal != null)
            panelFinal.SetActive(false);
        
        if (transitionPanel != null)
            transitionPanel.SetActive(false);
        ShowBettingControls(false);

        gameState.HandPot = handPot;
        gameState.SabaccPot = sabaccPot;
        gameState.DealerIndex = dealerIndex;
        gameState.CurrentPlayerIndex = currentPlayerIndex;
        gameState.CurrentPhase = GamePhase.Dealing;
        
        //
        for (int i = 0; i < gameState.Players.Count && i < playerCredits.Length; i++)
        {
            gameState.Players[i].Credits = playerCredits[i];
            
            // Aplicar estado del jugador (activo/retirado)
            if (i < playerStates.Length)
            {
                gameState.Players[i].State = (PlayerState)playerStates[i];
            }
            
            // Solo resetear si esta activo
            if (gameState.Players[i].State == PlayerState.Active)
            {
                gameState.Players[i].Hand.Clear();
            }
        }
        
        // Repartir cartas solo a jugadores activos
        int cardIndex = 0;
        foreach (var player in gameState.Players)
        {
            if (player.State == PlayerState.Active)
            {
                player.Hand.Clear();
                
                for (int i = 0; i < 2; i++)
                {
                    if (cardIndex < cardIds.Length)
                    {
                        var card = SabaccCardDefinitions.GetCardById(cardIds[cardIndex]);
                        if (card != null)
                        {
                            player.Hand.AddCard(card);
                        }
                        cardIndex++;
                    }
                }
            }
        }

        CleanupAllCards();

        UpdateUI();
        StartCoroutine(ShowDeckAndDeal());
    }

    void CleanupAllCards()
    {
        for (int i = 0; i < _cardInstances.Count; i++)
        {
            foreach (var card in _cardInstances[i])
                if (card != null) Destroy(card);
            _cardInstances[i].Clear();
        }
        
        foreach (var card in _deckCardInstances)
            if (card != null) Destroy(card);
        _deckCardInstances.Clear();
        
        foreach (var card in _discardPileInstances)
            if (card != null) Destroy(card);
        _discardPileInstances.Clear();
        
        CleanupInterferenceFields();
        
        _originalCardPositions.Clear();
    }

    IEnumerator ShowDeckAndDeal()
    {
        Debug.Log($"[ShowDeckAndDeal] Iniciando. LocalPlayer: {localPlayerIndex}, IsMaster: {PhotonNetwork.IsMasterClient}");
        
        _isAnimating = true;
        DisableAllButtons();
        
        SwitchToCamera(START_CAMERA_INDEX);
        
        gameStateText.text = "Barajando...";
        CreateVisualDeck();
        
        // Sonido de barajar
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCardShuffle();
        
        Debug.Log($"[ShowDeckAndDeal] Deck creado. Cards en deck visual: {_deckCardInstances.Count}");
        
        yield return new WaitForSeconds(1f);
        
        gameStateText.text = "Repartiendo...";
        
        // Sonido de inicio de ronda
        if (AudioManager.Instance != null) AudioManager.Instance.PlayRoundStart();

        Debug.Log("[ShowDeckAndDeal] Llamando AnimateDealCards...");
        yield return AnimateDealCards();
        Debug.Log("[ShowDeckAndDeal] AnimateDealCards completado.");
        
        SaveOriginalCardPositions();
        
        yield return new WaitForSeconds(0.5f);
        SwitchToMyCamera();
        
        yield return new WaitForSeconds(0.3f);
        if (localPlayerIndex >= 0 && localPlayerIndex < _cardInstances.Count)
        {
            Debug.Log($"[ShowDeckAndDeal] Revelando mano del jugador local {localPlayerIndex}");
            
            // Sonido de voltear cartas
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCardFlip();
            
            yield return AnimationManager.Instance.RevealPlayerHand(_cardInstances[localPlayerIndex]);
        }
        
        _isAnimating = false;
        
        // Despues del reparto viene la PRIMERA RONDA DE APUESTAS
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[ShowDeckAndDeal] Master invocando StartFirstBettingPhase...");
            Invoke(nameof(StartFirstBettingPhase), 1f);
        }
    }
    
    void StartFirstBettingPhase()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPC_StartFirstBettingPhase", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_StartFirstBettingPhase()
    {
        Debug.Log("[RPC_StartFirstBettingPhase] Iniciando primera ronda de apuestas");
        gameState.CurrentPhase = GamePhase.FirstBetting;
        gameState.CallerIndex = -1;
        
        foreach (var player in gameState.Players)
        {
            player.ResetBettingRound();
        }
        gameState.CurrentHighestBet = 0;
        _bettingRoundStarter = 0;
        gameState.CurrentPlayerIndex = 0;
        
        // Buscar primer jugador activo
        while (gameState.CurrentPlayerIndex < gameState.Players.Count && 
               gameState.Players[gameState.CurrentPlayerIndex].State != PlayerState.Active)
        {
            gameState.CurrentPlayerIndex++;
        }
        
        if (gameState.CurrentPlayerIndex >= gameState.Players.Count)
            gameState.CurrentPlayerIndex = 0;
        
        gameStateText.text = "Primera ronda de apuestas";
        UpdateUI();
        StartBettingRound();
    }

    //  SHIFTING 
    
    [Header("Configuracion de Shifting")]
    [Range(0.2f, 0.33f)]
    public float shiftProbability = 0.25f; // 25% de probabilidad por defecto

    void TriggerFirstShift()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Aplicar shifting y obtener los cambios
        List<GameLogic.ShiftResult> changes = gameLogic.ApplyShifting(gameState, shiftProbability);
        
        // Serializar los cambios para enviar por RPC
        int[] playerIndices = new int[changes.Count];
        int[] cardIndices = new int[changes.Count];
        string[] oldCardIds = new string[changes.Count];
        string[] newCardIds = new string[changes.Count];
        
        for (int i = 0; i < changes.Count; i++)
        {
            playerIndices[i] = changes[i].PlayerIndex;
            cardIndices[i] = changes[i].CardIndex;
            oldCardIds[i] = changes[i].OldCardId;
            newCardIds[i] = changes[i].NewCardId;
        }
        
        // Enviar a todos los jugadores
        photonView.RPC("RPC_ApplyShifting", RpcTarget.All, 
            playerIndices, cardIndices, oldCardIds, newCardIds, true); // true = isFirstShift
    }

    void TriggerSecondShift()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        // Aplicar shifting y obtener los cambios
        List<GameLogic.ShiftResult> changes = gameLogic.ApplyShifting(gameState, shiftProbability);
        
        // Serializar los cambios para enviar por RPC
        int[] playerIndices = new int[changes.Count];
        int[] cardIndices = new int[changes.Count];
        string[] oldCardIds = new string[changes.Count];
        string[] newCardIds = new string[changes.Count];
        
        for (int i = 0; i < changes.Count; i++)
        {
            playerIndices[i] = changes[i].PlayerIndex;
            cardIndices[i] = changes[i].CardIndex;
            oldCardIds[i] = changes[i].OldCardId;
            newCardIds[i] = changes[i].NewCardId;
        }
        
        // Enviar a todos los jugadores
        photonView.RPC("RPC_ApplyShifting", RpcTarget.All, 
            playerIndices, cardIndices, oldCardIds, newCardIds, false); // false = isSecondShift
    }

    [PunRPC]
    void RPC_ApplyShifting(int[] playerIndices, int[] cardIndices, string[] oldCardIds, string[] newCardIds, bool isFirstShift)
    {
        string shiftName = isFirstShift ? "FIRST SHIFT" : "SECOND SHIFT";

        StartCoroutine(AnimateShifting(playerIndices, cardIndices, oldCardIds, newCardIds, isFirstShift));
    }

    IEnumerator AnimateShifting(int[] playerIndices, int[] cardIndices, string[] oldCardIds, string[] newCardIds, bool isFirstShift)
    {
        _isAnimating = true;
        
        string shiftName = isFirstShift ? "PRIMER SHIFT" : "SEGUNDO SHIFT";
        
        // Sonido de shift
        if (AudioManager.Instance != null) AudioManager.Instance.PlayShift();
        
        if (playerIndices.Length == 0)
        {
            gameStateText.text = $" {shiftName} \nNinguna carta cambio!";
            yield return new WaitForSeconds(2f);
        }
        else
        {
            gameStateText.text = $" {shiftName} \nLas cartas estan cambiando!";
            yield return new WaitForSeconds(1f);
            
            // Animar cada carta que cambio
            for (int i = 0; i < playerIndices.Length; i++)
            {
                int playerIdx = playerIndices[i];
                int cardIdx = cardIndices[i];
                string oldCardId = oldCardIds[i];
                string newCardId = newCardIds[i];
                bool isMyCard = (playerIdx == localPlayerIndex);
                
                // Verificar que los indices son validos
                if (playerIdx >= _cardInstances.Count)
                {
                    Debug.LogError($"[AnimateShifting] playerIdx {playerIdx} fuera de rango (max: {_cardInstances.Count})");
                    continue;
                }
                
                if (cardIdx >= _cardInstances[playerIdx].Count)
                {
                    Debug.LogError($"[AnimateShifting] cardIdx {cardIdx} fuera de rango para jugador {playerIdx} (max: {_cardInstances[playerIdx].Count})");
                    continue;
                }
                
                // Esto es necesario para que la revelacion muestre los valores correctos
                if (!PhotonNetwork.IsMasterClient)
                {
                    SabaccCard newCard = SabaccCardDefinitions.GetCardById(newCardId);
                    if (newCard != null && playerIdx < gameState.Players.Count)
                    {
                        gameState.Players[playerIdx].Hand.ReplaceCardAt(cardIdx, newCard);
                    }
                }
                
                // Animar el cambio visual
                GameObject cardObject = _cardInstances[playerIdx][cardIdx];
                
                if (cardObject != null)
                {
                    // Siempre reemplazar la carta visual (AnimateCardShift maneja si mostrar boca arriba o abajo)
                    yield return StartCoroutine(AnimateCardShift(cardObject, playerIdx, cardIdx, newCardId));
                }
                else
                {
                    Debug.LogError($"[AnimateShifting] cardObject es NULL para jugador {playerIdx}, carta {cardIdx}");
                    // Intentar crear la carta desde cero si no existe
                    yield return StartCoroutine(CreateMissingCard(playerIdx, cardIdx, newCardId));
                }
            }
            
            // Mostrar resumen
            int myChangedCards = 0;
            for (int i = 0; i < playerIndices.Length; i++)
            {
                if (playerIndices[i] == localPlayerIndex)
                    myChangedCards++;
            }
            
            if (myChangedCards > 0)
            {
                gameStateText.text = $" {shiftName} \n{myChangedCards} de tus cartas cambiaron!";
            }
            else
            {
                gameStateText.text = $" {shiftName} \nTus cartas no cambiaron!";
            }
            
            yield return new WaitForSeconds(1.5f);
        }
        
        // Actualizar valor de mano despues del shift
        if (localPlayerIndex >= 0 && localPlayerIndex < gameState.Players.Count)
        {
            UpdateCurrentPlayerHandValue();
        }
        
        _isAnimating = false;
        
        // Continuar con la siguiente fase
        if (PhotonNetwork.IsMasterClient)
        {
            if (isFirstShift)
            {
                photonView.RPC("RPC_AdvanceToDrawing", RpcTarget.All);
            }
            else
            {
                photonView.RPC("RPC_StartRevealPhase", RpcTarget.All);
            }
        }
    }

    IEnumerator AnimateCardShift(GameObject cardObject, int playerIdx, int cardIdx, string newCardId)
    {
        // Obtener el prefab de la nueva carta
        GameObject newPrefab = cardPrefabMap.GetPrefab(newCardId);
        if (newPrefab == null)
        {
            Debug.LogError($"[AnimateCardShift] No se encontro prefab para {newCardId}");
            yield break;
        }
        
        if (cardObject == null)
        {
            Debug.LogError($"[AnimateCardShift] cardObject es NULL para jugador {playerIdx}, carta {cardIdx}");
            yield break;
        }
        
        // Guardar posicion y rotacion ACTUALES de la carta
        Vector3 cardPosition = cardObject.transform.position;
        Quaternion cardRotation = cardObject.transform.rotation;
        Transform parent = cardObject.transform.parent;
        
        // Efecto de "temblor" 
        float shakeDuration = 0.5f;
        float shakeAmount = 0.05f;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            if (cardObject == null) 
            {
                Debug.LogError($"[AnimateCardShift] cardObject destruido durante shake");
                yield break;
            }
            Vector3 shakeOffset = new Vector3(
                UnityEngine.Random.Range(-shakeAmount, shakeAmount),
                UnityEngine.Random.Range(-shakeAmount, shakeAmount),
                0
            );
            cardObject.transform.position = cardPosition + shakeOffset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Restaurar posicion antes de destruir
        if (cardObject != null)
        {
            cardObject.transform.position = cardPosition;
        }
        
        // Limpiar de _originalCardPositions si existe
        if (_originalCardPositions.ContainsKey(cardObject))
        {
            _originalCardPositions.Remove(cardObject);
        }
        
        // Destruir la carta vieja
        Destroy(cardObject);
        
        bool isMyCard = (playerIdx == localPlayerIndex);
        
        Quaternion correctRotation;
        if (isMyCard)
        {
            correctRotation = cardRotation;
        }
        else
        {
            correctRotation = cardRotation;
        }
        
        GameObject newCardObject = Instantiate(newPrefab, cardPosition, correctRotation);
        newCardObject.transform.SetParent(parent, true);
        newCardObject.transform.position = cardPosition;
        newCardObject.transform.rotation = correctRotation;
        newCardObject.transform.localScale = Vector3.one;
        
        // Configurar la nueva carta
        SabaccCard newCard = SabaccCardDefinitions.GetCardById(newCardId);
        if (newCard == null)
        {
            Debug.LogError($"[AnimateCardShift] No se pudo obtener SabaccCard para {newCardId}");
            Destroy(newCardObject);
            yield break;
        }
        
        CardView newCardView = newCardObject.GetComponent<CardView>();
        if (newCardView == null)
            newCardView = newCardObject.AddComponent<CardView>();
        newCardView.SetCard(newCard);
        newCardView.SetFaceUp(isMyCard);
        
        // Restaurar la rotacion despues de SetFaceUp por si la modifica
        newCardObject.transform.rotation = correctRotation;
        
        // Actualizar en la lista de instancias
        if (playerIdx < _cardInstances.Count && cardIdx < _cardInstances[playerIdx].Count)
        {
            _cardInstances[playerIdx][cardIdx] = newCardObject;
            _originalCardPositions[newCardObject] = cardPosition;
        }
        else
        {
            Debug.LogError($"[AnimateCardShift] a indices invalidos: player={playerIdx}, card={cardIdx}");
            Destroy(newCardObject);
        }
    }

    // Animacion de solo temblor para cartas de otros jugadores (no revela la carta)
    IEnumerator AnimateCardShake(GameObject cardObject)
    {
        if (cardObject == null) yield break;
        
        Vector3 originalPos = cardObject.transform.position;
        
        // Efecto de "temblor"
        float shakeDuration = 0.5f;
        float shakeAmount = 0.05f;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            Vector3 shakeOffset = new Vector3(
                UnityEngine.Random.Range(-shakeAmount, shakeAmount),
                UnityEngine.Random.Range(-shakeAmount, shakeAmount),
                0
            );
            cardObject.transform.position = originalPos + shakeOffset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Restaurar posicion original
        cardObject.transform.position = originalPos;
    }

    // Crea una carta que falta en la posicion correcta
    IEnumerator CreateMissingCard(int playerIdx, int cardIdx, string cardId)
    {
        GameObject prefab = cardPrefabMap.GetPrefab(cardId);
        if (prefab == null)
        {
            Debug.LogError($"[CreateMissingCard] No se encontro prefab para {cardId}");
            yield break;
        }

        // Calcular posicion
        int totalCards = _cardInstances[playerIdx].Count;
        Vector3 position = _positionCalculator.CalculatePosition(_handAreas[playerIdx], cardIdx, totalCards);
        
        bool isMyCard = (playerIdx == localPlayerIndex);
        Quaternion rotation = isMyCard ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.Euler(180f, 0f, 0f);

        GameObject newCard = Instantiate(prefab, position, rotation);
        newCard.transform.SetParent(_handAreas[playerIdx], true);
        newCard.transform.localScale = Vector3.one;

        SabaccCard card = SabaccCardDefinitions.GetCardById(cardId);
        CardView cardView = newCard.GetComponent<CardView>();
        if (cardView == null)
            cardView = newCard.AddComponent<CardView>();
        cardView.SetCard(card);
        cardView.SetFaceUp(isMyCard);

        // Insertar en la posicion correcta
        while (_cardInstances[playerIdx].Count <= cardIdx)
        {
            _cardInstances[playerIdx].Add(null);
        }
        _cardInstances[playerIdx][cardIdx] = newCard;
        _originalCardPositions[newCard] = position;
    }

    void SaveOriginalCardPositions()
    {
        _originalCardPositions.Clear();
        
        for (int i = 0; i < _cardInstances.Count; i++)
        {
            foreach (var cardInstance in _cardInstances[i])
            {
                if (cardInstance != null)
                {
                    _originalCardPositions[cardInstance] = cardInstance.transform.position;
                }
            }
        }
    }

    IEnumerator AnimateDealCards()
    {
        // Validar referencias antes de animar
        if (AnimationManager.Instance == null)
        {
            Debug.LogError("[AnimateDealCards] AnimationManager.Instance es NULL!");
            yield break;
        }

        if (_handAreas == null || _handAreas.Count == 0)
        {
            Debug.LogError("[AnimateDealCards] _handAreas es NULL o vacio!");
            yield break;
        }

        if (deckArea == null)
        {
            Debug.LogError("[AnimateDealCards] deckArea es NULL!");
            yield break;
        }

        int playerCount = gameState.Players.Count;

        // Solo pasar las areas y listas de los jugadores activos
        Transform[] handAreasArray = new Transform[playerCount];
        List<GameObject>[] cardInstancesArray = new List<GameObject>[playerCount];
        
        for (int i = 0; i < playerCount; i++)
        {
            handAreasArray[i] = _handAreas[i];
            cardInstancesArray[i] = _cardInstances[i];
        }

        yield return AnimationManager.Instance.DealCardsToPlayers(
            cardInstancesArray, 
            handAreasArray, 
            deckArea,
            cardPrefabMap, 
            gameState.Players, 
            _positionCalculator, 
            null
        );
    }

    void CreateVisualDeck()
    {
        foreach (var card in _deckCardInstances)
            if (card != null) Destroy(card);
        _deckCardInstances.Clear();

        if (deckArea == null)
        {
            Debug.LogError("[CreateVisualDeck] deckArea es NULL!");
            return;
        }

        if (cardPrefabMap == null)
        {
            Debug.LogError("[CreateVisualDeck] cardPrefabMap es NULL!");
            return;
        }

        List<SabaccCard> deckCards = SabaccCardDefinitions.CreateFullDeck();
        int cardsCreated = 0;

        for (int i = 0; i < Mathf.Min(deckCards.Count, 76); i++)
        {
            var card = deckCards[i];
            GameObject prefab = cardPrefabMap.GetPrefab(card.GetCardId());
            
            if (prefab == null) 
            {
                Debug.LogWarning($"[CreateVisualDeck] Prefab no encontrado para: {card.GetCardId()}");
                continue;
            }

            Vector3 offset = Vector3.up * (i * 0.002f);
            GameObject instance = Instantiate(prefab, deckArea.position + offset, deckArea.rotation);
            instance.transform.SetParent(deckArea, true);
            instance.transform.localPosition = offset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var cardView = instance.GetComponent<CardView>() ?? instance.AddComponent<CardView>();
            cardView.SetCard(card);
            cardView.SetFaceUp(false);
            _deckCardInstances.Add(instance);
            cardsCreated++;
        }
    }

    [PunRPC]
    void RPC_StartCallingPhase()
    {
        Debug.Log("[RPC_StartCallingPhase] Iniciando fase de Calling");
        gameState.CurrentPhase = GamePhase.Calling;
        gameState.CallerIndex = -1;
        
        // Resetear apuestas de todos los jugadores para esta ronda
        foreach (var player in gameState.Players)
        {
            player.ResetBettingRound();
        }
        gameState.CurrentHighestBet = 0;
        
        gameState.CurrentPlayerIndex = 0;
        
        // Buscar primer jugador activo desde el inicio
        while (gameState.CurrentPlayerIndex < gameState.Players.Count && 
               gameState.Players[gameState.CurrentPlayerIndex].State != PlayerState.Active)
        {
            gameState.CurrentPlayerIndex++;
        }
        
        if (gameState.CurrentPlayerIndex >= gameState.Players.Count)
            gameState.CurrentPlayerIndex = 0;
        
        _bettingRoundStarter = gameState.CurrentPlayerIndex;
        
        gameStateText.text = "Fase de Calling";
        UpdateUI();
        StartBettingRound();
    }

    //  COMANDOS: el cliente pide, el Master valida y ejecuta 
    //
    //  1. El boton llama a SubmitCommand: se valida en local (feedback inmediato)
    //     y se envia al Master. Mientras esperamos, los controles quedan bloqueados.
    //  2. El Master (RPC_SubmitCommand) vuelve a validar con el MISMO CommandValidator
    //     sobre su estado, identificando al jugador por el remitente real del mensaje.
    //  3. Si es valido, ExecuteCommand emite el evento a todos (RPC_PlayerBet, ...).
    //     Si no, RPC_CommandRejected solo al remitente, que recupera sus controles.

    PlayerId LocalPlayerId => new PlayerId(PhotonNetwork.LocalPlayer.ActorNumber);

    bool LocalHasDiscarded =>
        gameState != null && localPlayerIndex >= 0 && localPlayerIndex < gameState.Players.Count &&
        gameState.Players[localPlayerIndex].HasDiscardedThisTurn;

    // Valida en local y envia el comando al Master. Devuelve true si se ha enviado.
    bool SubmitCommand(CommandType type, int arg = 0)
    {
        if (gameState == null || localPlayerIndex < 0) return false;
        if (_awaitingCommandResult) return false; // ya hay un comando en vuelo: se ignora el doble clic

        GameCommand command = new GameCommand(type, LocalPlayerId, arg);
        string error = CommandValidator.Validate(gameState, _rules, command);
        if (error != null)
        {
            gameStateText.text = error;
            return false;
        }

        _awaitingCommandResult = true;
        LockControlsWhileWaiting(type);
        photonView.RPC(nameof(RPC_SubmitCommand), RpcTarget.MasterClient, (byte)type, arg);
        return true;
    }

    void LockControlsWhileWaiting(CommandType type)
    {
        switch (type)
        {
            case CommandType.Check:
            case CommandType.Bet:
            case CommandType.Match:
            case CommandType.Call:
            case CommandType.Fold:
                ShowBettingControls(false);
                break;
            case CommandType.Draw:
            case CommandType.Stand:
            case CommandType.Discard:
                DisableAllButtons();
                break;
            // Proteger/desproteger no bloquea botones: _awaitingCommandResult ya evita el doble envio
        }
    }

    // Se llama al recibir el evento resultante de un comando
    void OnCommandResolved(int playerIndex)
    {
        if (playerIndex == localPlayerIndex)
            _awaitingCommandResult = false;
    }

    // Solo el Master emite eventos de partida: un cliente modificado no puede enviarlos
    bool IsFromMaster(PhotonMessageInfo info) => info.Sender != null && info.Sender.IsMasterClient;

    [PunRPC]
    void RPC_SubmitCommand(byte type, int arg, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || gameState == null || info.Sender == null) return;

        // La identidad sale del remitente real del mensaje, no de un indice que diga el cliente
        PlayerId sender = new PlayerId(info.Sender.ActorNumber);
        GameCommand command = new GameCommand((CommandType)type, sender, arg);

        string error = CommandValidator.Validate(gameState, _rules, command);
        if (error == null)
        {
            error = ExecuteCommand(gameState.IndexOf(sender), command);
        }

        if (error != null)
        {
            Debug.Log($"[Master] Comando rechazado: {command} -> {error}");
            photonView.RPC(nameof(RPC_CommandRejected), info.Sender, error);
        }
    }

    // Ejecuta en el Master un comando ya validado y emite el evento a todos.
    // Devuelve null, o un motivo de rechazo que solo el Master puede conocer (p. ej. mazo vacio).
    string ExecuteCommand(int seat, GameCommand command)
    {
        Player player = gameState.Players[seat];

        switch (command.Type)
        {
            case CommandType.Draw:
                return ExecuteDraw(seat);
            case CommandType.Stand:
                ExecuteStand(seat);
                return null;
            case CommandType.Discard:
                ExecuteDiscard(seat, command.Arg);
                return null;
            case CommandType.Protect:
                ExecuteSetProtected(seat, command.Arg, true);
                return null;
            case CommandType.Unprotect:
                ExecuteSetProtected(seat, command.Arg, false);
                return null;
            case CommandType.Check:
                photonView.RPC(nameof(RPC_PlayerChecked), RpcTarget.All, seat);
                return null;
            case CommandType.Bet:
                photonView.RPC(nameof(RPC_PlayerBet), RpcTarget.All, seat, command.Arg);
                return null;
            case CommandType.Match:
                photonView.RPC(nameof(RPC_PlayerMatchedBet), RpcTarget.All, seat, gameState.AmountToCall(player));
                return null;
            case CommandType.Call:
                photonView.RPC(nameof(RPC_PlayerCalled), RpcTarget.All, seat);
                return null;
            case CommandType.Fold:
                int penalty = CommandValidator.FoldPenalty(gameState, _rules, player);
                photonView.RPC(nameof(RPC_PlayerFolded), RpcTarget.All, seat, penalty);
                return null;
        }
        return "Accion desconocida.";
    }

    [PunRPC]
    void RPC_CommandRejected(string reason, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return;

        _awaitingCommandResult = false;
        gameStateText.text = reason;
        RestoreControlsForCurrentTurn();
    }

    // Devuelve los controles al jugador local si sigue siendo su turno
    void RestoreControlsForCurrentTurn()
    {
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;

        if (CommandValidator.IsBettingPhase(gameState.CurrentPhase))
            ShowBettingControls(true);
        else if (gameState.CurrentPhase == GamePhase.Drawing)
            RefreshDrawingButtons();
    }

    void RefreshDrawingButtons()
    {
        Player me = gameState.Players[localPlayerIndex];
        drawButton.interactable = !me.HasDiscardedThisTurn && me.Hand.GetCount() < maxCardsInHand;
        standButton.interactable = true;
        discardButton.interactable = false; // se activa al seleccionar una carta
    }

    //  SISTEMA DE APUESTAS 
    
    void StartBettingRound()
    {
        Debug.Log($"[StartBettingRound] Fase: {gameState.CurrentPhase}, Jugador actual: {gameState.CurrentPlayerIndex}, Local: {localPlayerIndex}");
        
        // Solo mostrar UI de betting si estamos en fase de apuestas
        if (gameState.CurrentPhase != GamePhase.FirstBetting && 
            gameState.CurrentPhase != GamePhase.Calling && 
            gameState.CurrentPhase != GamePhase.SecondBetting)
        {
            ShowBettingControls(false);
            return;
        }
        
        // Verificar que el jugador actual esta activo
        Player currentPlayer = gameState.CurrentPlayer;
        if (currentPlayer.State != PlayerState.Active)
        {
            // Saltar a siguiente jugador activo
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(ProcessNextBettingTurn());
            }
            return;
        }
        
        UpdateBettingUI();
        
        //   Mostrar mensaje correcto para TODOS los jugadores 
        if (gameState.CurrentPlayerIndex == localPlayerIndex)
        {
            ShowBettingControls(true);
            gameStateText.text = GetBettingInstructions();
            if (currentPlayerText != null)
                currentPlayerText.text = "Tu turno";
        }
        else
        {
            ShowBettingControls(false);
            gameStateText.text = $"Esperando a {currentPlayer.Name}...";
            if (currentPlayerText != null)
                currentPlayerText.text = $"Turno de {currentPlayer.Name}";
        }
        
        // Actualizar UI de fase para todos
        if (phaseText != null)
        {
            phaseText.text = $"Fase: {GetPhaseDisplayName(gameState.CurrentPhase)}";
        }
    }
    
    //  HELPER: Nombre legible de la fase 
    string GetPhaseDisplayName(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.InitialBetting: return "Apuesta Inicial";
            case GamePhase.Dealing: return "Reparto";
            case GamePhase.FirstBetting: return "Primera Apuesta";
            case GamePhase.FirstShift: return "Primer Shift";
            case GamePhase.Calling: return "Calling";
            case GamePhase.Drawing: return "Robo/Descarte";
            case GamePhase.SecondBetting: return "Segunda Apuesta";
            case GamePhase.SecondShift: return "Segundo Shift";
            case GamePhase.Reveal: return "Revelacion";
            default: return phase.ToString();
        }
    }
    
    string GetBettingInstructions()
    {
        Player currentPlayer = gameState.CurrentPlayer;
        int amountToCall = gameState.CurrentHighestBet - currentPlayer.CurrentBet;
        
        if (gameState.CurrentPhase == GamePhase.Calling)
        {
            // En Calling no se puede apostar/subir
            if (amountToCall > 0)
                return $"Tu turno - Iguala ({amountToCall}), CALL (revelar) o Fold";
            else
                return "Tu turno - Check, CALL (revelar) o Fold";
        }
        else if (gameState.CurrentPhase == GamePhase.FirstBetting)
        {
            // En FirstBetting se puede apostar/subir pero no hacer CALL
            if (amountToCall > 0)
                return $"Tu turno - Iguala ({amountToCall}), Sube o Fold";
            else
                return "Tu turno - Check, Sube o Fold";
        }
        else // SecondBetting
        {
            if (amountToCall > 0)
                return $"Tu turno - Iguala ({amountToCall}), Sube, CALL o Fold";
            else
                return "Tu turno - Check, Sube, CALL o Fold";
        }
    }
    
    void ShowBettingControls(bool show)
    {
        if (bettingPanel != null)
            bettingPanel.SetActive(show);
            
        if (show)
        {
            Player currentPlayer = gameState.CurrentPlayer;
            int amountToCall = gameState.CurrentHighestBet - currentPlayer.CurrentBet;
            
            // Check solo si no hay que igualar
            if (checkButton != null)
                checkButton.interactable = (amountToCall == 0);
            
            // En fase Calling no se puede apostar/subir, solo en SecondBetting
            if (betButton != null)
            {
                if (gameState.CurrentPhase == GamePhase.Calling)
                {
                    betButton.interactable = false; // No se puede subir en Calling
                }
                else
                {
                    betButton.interactable = currentPlayer.Credits > amountToCall;
                }
            }
            
            // Call button
            if (callButton != null)
            {
                bool canForceReveal = (gameState.CurrentPhase == GamePhase.Calling || 
                                       gameState.CurrentPhase == GamePhase.SecondBetting);
                
                if (canForceReveal)
                {
                    callButton.interactable = true;
                    if (amountToCall > 0)
                    {
                        callButton.GetComponentInChildren<TMP_Text>().text = $"Igualar ({amountToCall})";
                    }
                    else
                    {
                        callButton.GetComponentInChildren<TMP_Text>().text = "CALL (Revelar)";
                    }
                }
                else
                {
                    callButton.interactable = amountToCall > 0 && currentPlayer.CanAfford(amountToCall);
                    callButton.GetComponentInChildren<TMP_Text>().text = amountToCall > 0 ? $"Igualar ({amountToCall})" : "Igualar";
                }
            }
            
            // Fold siempre disponible
            if (foldButton != null)
                foldButton.interactable = true;
                
            // Limpiar input
            if (betInputField != null)
                betInputField.text = "";
        }
    }
    
    void UpdateBettingUI()
    {
        if (currentBetText != null)
            currentBetText.text = $"Apuesta actual: {gameState.CurrentHighestBet}";
        UpdateUI();
    }
    
    public void OnCheckButton()
    {
        SubmitCommand(CommandType.Check);
    }
    
    [PunRPC]
    void RPC_PlayerChecked(int playerIndex, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        Player player = gameState.Players[playerIndex];
        player.HasActedThisBettingRound = true;
        
        // Sonido de check
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCheck();
        
        gameStateText.text = $"{player.Name} paso.";
        
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(ProcessNextBettingTurn());
        }
    }
    
    public void OnBetButton()
    {
        if (gameState == null) return;

        // Errores de formato del campo de texto: son de la UI, no de las reglas
        if (betInputField == null || string.IsNullOrEmpty(betInputField.text))
        {
            gameStateText.text = "Introduce una cantidad para apostar.";
            return;
        }
        
        if (!int.TryParse(betInputField.text, out int betAmount) || betAmount <= 0)
        {
            gameStateText.text = "Cantidad invalida.";
            return;
        }

        SubmitCommand(CommandType.Bet, betAmount);
    }
    
    [PunRPC]
    void RPC_PlayerBet(int playerIndex, int betAmount, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        Player player = gameState.Players[playerIndex];
        int amountToCall = gameState.CurrentHighestBet - player.CurrentBet;
        int totalBet = amountToCall + betAmount;
        
        // Sonido de subir apuesta
        if (AudioManager.Instance != null) AudioManager.Instance.PlayRaise();
        
        // Descontar creditos
        player.DeductCredits(totalBet);
        player.CurrentBet = gameState.CurrentHighestBet + betAmount;
        player.TotalBetThisRound += totalBet;
        player.HasActedThisBettingRound = true;
        
        // Anadir al bote
        gameState.HandPot += totalBet;
        
        // Actualizar apuesta mas alta
        gameState.CurrentHighestBet = player.CurrentBet;
        
        foreach (var p in gameState.Players)
        {
            if (p != player && p.State == PlayerState.Active)
            {
                p.HasActedThisBettingRound = false;
            }
        }
        
        gameStateText.text = $"{player.Name} subio a {gameState.CurrentHighestBet}.";
        UpdateBettingUI();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(ProcessNextBettingTurn());
        }
    }

    public void OnCallButton()
    {
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;

        // Si hay algo que igualar, el boton iguala; si no, es un CALL para forzar la revelacion
        int amountToCall = gameState.AmountToCall(gameState.CurrentPlayer);
        SubmitCommand(amountToCall > 0 ? CommandType.Match : CommandType.Call);
    }
    
    [PunRPC]
    void RPC_PlayerMatchedBet(int playerIndex, int amount, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        Player player = gameState.Players[playerIndex];
        
        // Sonido de fichas
        if (AudioManager.Instance != null) AudioManager.Instance.PlayChipsPlace();
        
        player.DeductCredits(amount);
        player.CurrentBet = gameState.CurrentHighestBet;
        player.TotalBetThisRound += amount;
        player.HasActedThisBettingRound = true;
        
        gameState.HandPot += amount;
        
        gameStateText.text = $"{player.Name} igualo ({amount}).";
        UpdateBettingUI();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(ProcessNextBettingTurn());
        }
    }
    
    [PunRPC]
    void RPC_PlayerCalled(int playerIndex, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        gameState.CallerIndex = playerIndex;
        
        // Sonido de call
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCall();
        
        Player player = gameState.Players[playerIndex];
        gameStateText.text = $"{player.Name} hizo CALL! Se forzara la revelacion.";
        
        ShowBettingControls(false);
        
        if (PhotonNetwork.IsMasterClient)
        {
            // Hacer shift y revelar
            StartCoroutine(HandleCallSequence());
        }
    }
    
    IEnumerator HandleCallSequence()
    {
        yield return new WaitForSeconds(1.5f);
        
        // Hacer un shift
        TriggerShiftAfterCall();
    }
    
    void TriggerShiftAfterCall()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        var changes = gameLogic.ApplyShifting(gameState, shiftProbability);
        
        int[] playerIndices = new int[changes.Count];
        int[] cardIndices = new int[changes.Count];
        string[] oldCardIds = new string[changes.Count];
        string[] newCardIds = new string[changes.Count];
        
        for (int i = 0; i < changes.Count; i++)
        {
            playerIndices[i] = changes[i].PlayerIndex;
            cardIndices[i] = changes[i].CardIndex;
            oldCardIds[i] = changes[i].OldCardId;
            newCardIds[i] = changes[i].NewCardId;
        }
        
        photonView.RPC("RPC_ApplyShiftAfterCall", RpcTarget.All, playerIndices, cardIndices, oldCardIds, newCardIds);
    }
    
    [PunRPC]
    void RPC_ApplyShiftAfterCall(int[] playerIndices, int[] cardIndices, string[] oldCardIds, string[] newCardIds)
    {
        StartCoroutine(AnimateShiftAndReveal(playerIndices, cardIndices, oldCardIds, newCardIds));
    }
    
    IEnumerator AnimateShiftAndReveal(int[] playerIndices, int[] cardIndices, string[] oldCardIds, string[] newCardIds)
    {
        gameStateText.text = "SHIFT DESPUES DEL CALL";
        yield return new WaitForSeconds(1f);
        
        // Animar cambios del shift
        for (int i = 0; i < playerIndices.Length; i++)
        {
            int playerIdx = playerIndices[i];
            int cardIdx = cardIndices[i];
            string newCardId = newCardIds[i];
            
            if (playerIdx < _cardInstances.Count && cardIdx < _cardInstances[playerIdx].Count)
            {
                GameObject cardObject = _cardInstances[playerIdx][cardIdx];
                if (cardObject != null)
                {
                    yield return StartCoroutine(AnimateCardShift(cardObject, playerIdx, cardIdx, newCardId));
                }
            }
            
            // Actualizar modelo en los clientes (el Master ya lo cambio en ApplyShifting).
            // Antes solo se actualizaba la mano local y en la revelacion los demas valores salian mal.
            if (!PhotonNetwork.IsMasterClient && playerIdx < gameState.Players.Count)
            {
                SabaccCard newCard = SabaccCardDefinitions.GetCardById(newCardId);
                if (newCard != null)
                {
                    gameState.Players[playerIdx].Hand.ReplaceCardAt(cardIdx, newCard);
                }
            }
        }
        
        yield return new WaitForSeconds(1f);
        
        // Ir a revelacion
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_StartRevealPhase", RpcTarget.All);
        }
    }
    
    public void OnFoldButton()
    {
        // La penalizacion la calcula el Master (CommandValidator.FoldPenalty)
        SubmitCommand(CommandType.Fold);
    }
    
    [PunRPC]
    void RPC_PlayerFolded(int playerIndex, int penalty, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        Player player = gameState.Players[playerIndex];
        
        // Sonido de fold
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFold();
        
        if (penalty > 0)
        {
            player.DeductCredits(penalty);
            gameState.SabaccPot += penalty;
        }
        
        player.Fold();
        
        gameStateText.text = penalty > 0 
            ? $"{player.Name} se retiro y pago {penalty} al Sabacc Pot."
            : $"{player.Name} se retiro.";
        
        UpdateBettingUI();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            int activePlayers = gameState.GetActivePlayerCount();
            if (activePlayers <= 1)
            {
                StartCoroutine(HandleLastPlayerStanding());
            }
            else
            {
                StartCoroutine(ProcessNextBettingTurn());
            }
        }
    }
    
    IEnumerator HandleLastPlayerStanding()
    {
        yield return new WaitForSeconds(1f);
        
        // Encontrar el ultimo jugador activo
        int winnerIndex = -1;
        for (int i = 0; i < gameState.Players.Count; i++)
        {
            if (gameState.Players[i].State == PlayerState.Active)
            {
                winnerIndex = i;
                break;
            }
        }
        
        if (winnerIndex >= 0)
        {
            // Los creditos se suman en RPC_ShowLastManStanding (en todos, incluido el Master).
            // Antes tambien se sumaban aqui, y el Master acababa con el bote duplicado.
            int totalWon = gameState.HandPot;
            
            photonView.RPC("RPC_ShowLastManStanding", RpcTarget.All, winnerIndex, totalWon);
        }
    }
    
    [PunRPC]
    void RPC_ShowLastManStanding(int winnerIndex, int amountWon)
    {
        Player winner = gameState.Players[winnerIndex];
        
        // Actualizar creditos del ganador
        winner.AddCredits(amountWon);
        gameState.HandPot = 0;
        
        //gameStateText.text = $"{winner.Name} gana {amountWon} creditos!\nTodos los demas se retiraron.";

        string mensaje = $"{winner.Name} gana {amountWon} creditos!\n" +
                     "Todos los demas se retiraron.";

        MostrarPanelFinal(mensaje);
        
        ShowBettingControls(false);
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            //StartCoroutine(PrepareNextRoundAfterDelay());
            StartCoroutine(DelayedPrepareNextRound(8f)); // Se ponen 8 segundos para que alcance a leer el mensaje
        }
    }
    
    /*
    IEnumerator PrepareNextRoundAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        PrepareNextRound();
    }
    */
    
    IEnumerator ProcessNextBettingTurn()
    {
        yield return new WaitForSeconds(1f);
        
        // Verificar cuantos jugadores activos quedan
        int activePlayers = gameState.GetActivePlayerCount();
        if (activePlayers <= 1)
        {
            // Solo queda un jugador, terminar ronda
            StartCoroutine(HandleLastPlayerStanding());
            yield break;
        }
        
        // Verificar si la ronda de apuestas ha terminado
        bool roundComplete = true;
        foreach (var player in gameState.Players)
        {
            if (player.State == PlayerState.Active && !player.HasActedThisBettingRound)
            {
                roundComplete = false;
                break;
            }
            // Tambien verificar que todos los activos hayan igualado
            if (player.State == PlayerState.Active && player.CurrentBet < gameState.CurrentHighestBet)
            {
                roundComplete = false;
                break;
            }
        }
        
        if (roundComplete)
        {
            Debug.Log("[ProcessNextBettingTurn] Ronda de apuestas completa");
            EndBettingRound();
        }
        else
        {
            // Buscar siguiente jugador activo
            int nextIndex = GetNextActivePlayer(gameState.CurrentPlayerIndex);
            Debug.Log($"[ProcessNextBettingTurn] Siguiente jugador: {nextIndex}");
            gameState.CurrentPlayerIndex = nextIndex;
            photonView.RPC("RPC_BettingTurnChanged", RpcTarget.All, nextIndex);
        }
    }
    
    int GetNextActivePlayer(int currentIndex)
    {
        int count = gameState.Players.Count;
        for (int i = 1; i <= count; i++)
        {
            int nextIndex = (currentIndex + i) % count;
            if (gameState.Players[nextIndex].State == PlayerState.Active)
            {
                return nextIndex;
            }
        }
        return currentIndex;
    }
    
    [PunRPC]
    void RPC_BettingTurnChanged(int newPlayerIndex)
    {
        gameState.CurrentPlayerIndex = newPlayerIndex;
        StartBettingRound();
    }
    
    void EndBettingRound()
    {
        Debug.Log($"[EndBettingRound] Fase actual: {gameState.CurrentPhase}");
        
        ShowBettingControls(false);
        
        switch (gameState.CurrentPhase)
        {
            case GamePhase.FirstBetting:
                // NUEVO ORDEN: Despues de primera apuesta viene Calling
                Debug.Log("[EndBettingRound] FirstBetting terminado, avanzando a Calling");
                photonView.RPC("RPC_StartCallingPhase", RpcTarget.All);
                break;
                
            case GamePhase.Calling:
                // Despues de Calling viene el First Shift y luego Drawing
                Debug.Log("[EndBettingRound] Calling terminado, avanzando a FirstShift");
                photonView.RPC("RPC_AdvanceToFirstShift", RpcTarget.All);
                break;
                
            case GamePhase.SecondBetting:
                // Despues de segunda apuesta viene el Second Shift y luego Reveal
                Debug.Log("[EndBettingRound] SecondBetting terminado, avanzando a SecondShift");
                photonView.RPC("RPC_AdvanceToSecondShift", RpcTarget.All);
                break;
                
            default:
                Debug.LogWarning($"[EndBettingRound] Fase inesperada: {gameState.CurrentPhase}");
                break;
        }
    }
    
    [PunRPC]
    void RPC_AdvanceToFirstShift()
    {
        Debug.Log("[RPC_AdvanceToFirstShift] Iniciando First Shift");
        gameState.CurrentPhase = GamePhase.FirstShift;
        gameStateText.text = " PRIMER SHIFT ";
        UpdateUI();
        
        // Solo el Master ejecuta la logica del shift
        if (PhotonNetwork.IsMasterClient)
        {
            TriggerFirstShift();
        }
    }
    
    [PunRPC]
    void RPC_AdvanceToDrawing()
    {
        Debug.Log("[RPC_AdvanceToDrawing] Iniciando fase de Drawing");
        gameState.CurrentPhase = GamePhase.Drawing;
        _playersWhoStood = 0;
        
        // Resetear para la ronda de apuestas de Drawing
        foreach (var player in gameState.Players)
        {
            player.ResetBettingRound();
            player.HasDiscardedThisTurn = false;
        }
        gameState.CurrentHighestBet = 0;
        
        //  empezar desde el jugador 1 (indice 0)
        gameState.CurrentPlayerIndex = 0;
        
        // Buscar primer jugador activo desde el inicio
        while (gameState.CurrentPlayerIndex < gameState.Players.Count && 
               gameState.Players[gameState.CurrentPlayerIndex].State != PlayerState.Active)
        {
            gameState.CurrentPlayerIndex++;
        }
        
        if (gameState.CurrentPlayerIndex >= gameState.Players.Count)
            gameState.CurrentPlayerIndex = 0;
        
        // Ocultar el panel de betting durante Drawing
        ShowBettingControls(false);
        
        UpdateUI();
        ShowTransitionScreen();
    }
    
    [PunRPC]
    void RPC_AdvanceToSecondShift()
    {
        gameState.CurrentPhase = GamePhase.SecondShift;
        gameStateText.text = " SEGUNDO SHIFT ";
        UpdateUI();
        
        // Solo el Master ejecuta la logica del shift
        if (PhotonNetwork.IsMasterClient)
        {
            TriggerSecondShift();
        }
    }
    
    //  FIN SISTEMA DE APUESTAS 

    void AdvanceToDrawingPhase()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        gameState.CurrentPhase = GamePhase.Drawing;
        photonView.RPC("RPC_StartDrawingPhase", RpcTarget.All);
    }

    [PunRPC]
    void RPC_StartDrawingPhase()
    {
        _playersWhoStood = 0;
        ShowTransitionScreen();
    }

    void ShowTransitionScreen()
    {
        Player currentPlayer = gameState.CurrentPlayer;
        int currentTurnPlayerIndex = gameState.CurrentPlayerIndex;
        
        // Ocultar panel de betting durante transiciones
        ShowBettingControls(false);
        
        // Cada jugador  ve desde SU propia camara
        SwitchToMyCamera();
        
        // Solo mostrar panel de transicion si es MI turno
        if (currentTurnPlayerIndex == localPlayerIndex)
        {
            transitionPanel.SetActive(true);
            //transitionText.text = $"Es tu turno!\n\nPresiona Continuar";
        }
        else
        {
            transitionPanel.SetActive(false);
            gameStateText.text = $"Esperando a {currentPlayer.Name}...";
        }
        
        DisableAllButtons();
    }

    void OnContinueFromTransition()
    {
        transitionPanel.SetActive(false);
        
        // Asegurar que el betting panel esta oculto durante Drawing
        ShowBettingControls(false);
        
        int currentTurnPlayerIndex = gameState.CurrentPlayerIndex;
        Player currentPlayer = gameState.CurrentPlayer;

        // Solo el jugador cuyo turno es puede continuar
        if (currentTurnPlayerIndex == localPlayerIndex && currentPlayer.State == PlayerState.Active)
        {
            gameStateText.text = "Tu turno - Roba o Plantate";
            currentPlayerText.text = $"Tu turno";
            UpdateUI();
            UpdateCurrentPlayerHandValue();
            EnableButtonsForCurrentPlayer();
        }
        else
        {
            gameStateText.text = $"Esperando a {currentPlayer.Name}...";
            DisableAllButtons();
        }
    }

    public void OnDrawCard()
    {
        if (_isAnimating) return;
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;
        
        // Deseleccionar cualquier carta seleccionada antes de robar
        if (_selectedCardIndex >= 0)
        {
            DeselectCurrentCard(localPlayerIndex);
            _selectedCardIndex = -1;
        }
        
        SubmitCommand(CommandType.Draw);
    }

    // Master: roba del mazo real (solo el Master lo tiene) y avisa a todos
    string ExecuteDraw(int seat)
    {
        if (gameState.MainDeck.GetCount() <= 0)
            return "No quedan cartas en el mazo.";

        SabaccCard drawnCard = gameState.MainDeck.Draw();
        gameState.Players[seat].Hand.AddCard(drawnCard);
        
        photonView.RPC(nameof(RPC_PlayerDrewCard), RpcTarget.All, seat, drawnCard.GetCardId());
        return null;
    }

    [PunRPC]
    void RPC_PlayerDrewCard(int playerIndex, string cardId, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        // Sonido de robar carta
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCardDeal();
        
        // Solo los clientes (no el Master) necesitan anadir la carta al modelo
        // porque el Master ya la anadio en ExecuteDraw
        if (!PhotonNetwork.IsMasterClient)
        {
            var card = SabaccCardDefinitions.GetCardById(cardId);
            if (card != null)
            {
                gameState.Players[playerIndex].Hand.AddCard(card);
            }
        }
        
        StartCoroutine(AnimateDrawCardNetwork(playerIndex, cardId));
    }

    IEnumerator AnimateDrawCardNetwork(int playerIndex, string cardId)
    {
        _isAnimating = true;
        DisableAllButtons();

        var card = SabaccCardDefinitions.GetCardById(cardId);
        if (card == null)
        {
            Debug.LogError($"[AnimateDrawCardNetwork] No se pudo crear carta con ID: {cardId}");
            _isAnimating = false;
            yield break;
        }

        int newCardIndex = gameState.Players[playerIndex].Hand.GetCount() - 1;

        // Validar referencias
        if (playerIndex >= _handAreas.Count || _handAreas[playerIndex] == null)
        {
            _isAnimating = false;
            yield break;
        }

        Vector3 targetPosition = _positionCalculator.CalculatePosition(
            _handAreas[playerIndex], 
            newCardIndex, 
            gameState.Players[playerIndex].Hand.GetCount()
        );

        yield return AnimationManager.Instance.DealSingleCard(
            card, 
            deckArea,
            _handAreas[playerIndex], 
            cardPrefabMap, 
            _cardInstances[playerIndex],
            targetPosition, 
            faceUp: false  //  boca abajo inicialmente
        );

        // Reorganizar solo las cartas no protegidas en la mano
        yield return ReorganizeHandWithProtected(playerIndex);

        //   Solo revelar la ultima carta si es el jugador LOCAL 
        if (_cardInstances[playerIndex].Count > 0 && playerIndex == localPlayerIndex)
        {
            var lastCard = _cardInstances[playerIndex][_cardInstances[playerIndex].Count - 1];
            if (lastCard != null)
            {
                yield return AnimationManager.Instance.FlipCard(lastCard, true);
            }
        }

        _isAnimating = false;
        
        if (playerIndex == localPlayerIndex)
        {
            UpdateCurrentPlayerHandValue();
            
            int cardCount = gameState.Players[playerIndex].Hand.GetCount();
            
            if (LocalHasDiscarded)
            {
                gameStateText.text = $"Carta robada (tienes {cardCount}). Debes plantarte.";
                standButton.interactable = true;
                drawButton.interactable = false;
                discardButton.interactable = false;
            }
            //   Verificar limite de cartas 
            else if (cardCount >= maxCardsInHand)
            {
                gameStateText.text = $"Limite alcanzado! ({cardCount} cartas). Descarta o plantate.";
                drawButton.interactable = false;
                standButton.interactable = true;
                discardButton.interactable = cardCount >= 3;
            }
            else if (cardCount >= 3)
            {
                gameStateText.text = $"Carta robada (tienes {cardCount}). Puedes robar mas, descartar, o plantarte.";
                EnableButtonsForCurrentPlayer();
            }
            else
            {
                gameStateText.text = $"Carta robada (tienes {cardCount}). Puedes robar mas o plantarte.";
                drawButton.interactable = true;
                standButton.interactable = true;
                discardButton.interactable = false;
            }
        }
    }

    public void OnStand()
    {
        if (_isAnimating) return;
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;
        
        // Deseleccionar cualquier carta seleccionada
        if (_selectedCardIndex >= 0)
        {
            DeselectCurrentCard(localPlayerIndex);
            _selectedCardIndex = -1;
        }
        
        SubmitCommand(CommandType.Stand);
    }

    // Master: pasa el turno al siguiente jugador activo y avisa a todos
    void ExecuteStand(int seat)
    {
        gameLogic.NextPlayer(gameState);
        _playersWhoStood++;
        
        photonView.RPC(nameof(RPC_PlayerStood), RpcTarget.All, seat, _playersWhoStood, gameState.CurrentPlayerIndex);
    }

    [PunRPC]
    void RPC_PlayerStood(int playerWhoStood, int totalStood, int nextPlayerIndex, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerWhoStood);

        _playersWhoStood = totalStood;
        gameState.CurrentPlayerIndex = nextPlayerIndex; // Sincronizar indice del jugador actual
        
        gameStateText.text = $"{gameState.Players[playerWhoStood].Name} se planta.";
        DisableAllButtons();

        if (AllPlayersStood())
        {
            if (PhotonNetwork.IsMasterClient)
                Invoke(nameof(AdvanceAfterDrawing), 1.5f);
        }
        else
        {
            Invoke(nameof(ShowTransitionScreen), 1.0f);
        }
    }

    bool AllPlayersStood() => _playersWhoStood >= gameState.GetActivePlayerCount();

    void AdvanceAfterDrawing()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RPC_StartSecondBettingPhase", RpcTarget.All);
    }
    
    [PunRPC]
    void RPC_StartSecondBettingPhase()
    {
        Debug.Log("[RPC_StartSecondBettingPhase] Iniciando segunda ronda de apuestas");
        gameState.CurrentPhase = GamePhase.SecondBetting;
        gameState.CallerIndex = -1;
        
        // Resetear apuestas de todos los jugadores
        foreach (var player in gameState.Players)
        {
            player.ResetBettingRound();
        }
        gameState.CurrentHighestBet = 0;
        _bettingRoundStarter = 0;
        
        gameState.CurrentPlayerIndex = 0;
        
        // Buscar primer jugador activo
        while (gameState.CurrentPlayerIndex < gameState.Players.Count && 
               gameState.Players[gameState.CurrentPlayerIndex].State != PlayerState.Active)
        {
            gameState.CurrentPlayerIndex++;
        }
        
        if (gameState.CurrentPlayerIndex >= gameState.Players.Count)
            gameState.CurrentPlayerIndex = 0;
        
        gameStateText.text = "Segunda ronda de apuestas";
        UpdateUI();
        StartBettingRound();
    }

    string[] SerializeAllHands()
    {
        List<string> cards = new List<string>();
        foreach (var player in gameState.Players)
        {
            foreach (var card in player.Hand.GetCards())
            {
                cards.Add(card.GetCardId());
            }
            // Anadir separador para saber donde termina la mano de cada jugador
            cards.Add("|"); 
        }
        return cards.ToArray();
    }

    [PunRPC]
    void RPC_StartRevealPhase()
    {
        gameState.CurrentPhase = GamePhase.Reveal;
        StartCoroutine(RevealAllHands());
    }

    IEnumerator RevealAllHands()
    {
        // Cambiar a camara final (endCamera) para ver todas las manos
        SwitchToCamera(END_CAMERA_INDEX);

        yield return AnimationManager.Instance.RevealAllHands(_cardInstances.ToArray(), () =>
        {
            UpdateAllPlayersHandValues();
            if (PhotonNetwork.IsMasterClient)
                ProcessRoundResult();
        });
    }

    void ProcessRoundResult()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Aplicar penalizacion por bombed out
        int[] handValues = new int[gameState.Players.Count];
        bool[] bombedOut = new bool[gameState.Players.Count];
        
        for (int i = 0; i < gameState.Players.Count; i++)
        {
            Player player = gameState.Players[i];
            int total = player.Hand.GetTotal();
            handValues[i] = total;
            bombedOut[i] = (total > 23 || total < -23 || total == 0);
            
            // Penalizacion por bombed out
            if (bombedOut[i] && player.State == PlayerState.Active)
            {
                int penalty = Mathf.Min(bombedOutPenalty, player.Credits);
                player.DeductCredits(penalty);
                gameState.SabaccPot += penalty;
                player.MarkAsBombedOut();
            }
        }

        Player winner = gameLogic.GetDefinitiveWinner(gameState, out string handType);
        
        // Penalizacion por call fallido
        if (gameState.SomeoneCalled)
        {
            Player callerPlayer = gameState.Players[gameState.CallerIndex];
            bool callerWon = (winner != null && gameState.Players.IndexOf(winner) == gameState.CallerIndex);
            
            if (!callerWon && callerPlayer.State != PlayerState.Folded)
            {
                int penalty = Mathf.Min(callPenalty, callerPlayer.Credits);
                callerPlayer.DeductCredits(penalty);
                gameState.SabaccPot += penalty;
            }
        }
        
        if (winner != null)
        {
            // Victoria definitiva: Sabacc Puro o Mano del Idiota
            int winnerIndex = gameState.Players.IndexOf(winner);
            int totalWon = gameState.HandPot + gameState.SabaccPot;
            int winnerHandValue = winner.Hand.GetTotal();
            
            // gameLogic.AwardHandPot(gameState, winner);
            // gameLogic.AwardSabaccPot(gameState, winner);
            
            photonView.RPC("RPC_ShowWinner", RpcTarget.All,
                winnerIndex, handType, totalWon, winnerHandValue, handValues, bombedOut);
        }
        else
        {
            // No hay victoria definitiva - buscar mejor mano
            Player bestHand = gameLogic.GetBestHandForRound(gameState);
            if (bestHand != null)
            {
                // Victoria de ronda normal: solo Hand Pot
                // El Sabacc Pot persiste
                int bestHandIndex = gameState.Players.IndexOf(bestHand);
                int handPotAmount = gameState.HandPot;
                int bestHandValue = bestHand.Hand.GetTotal();
                
                // no dar premios aqui - se hace en el RPC para sincronizar
                // gameLogic.AwardHandPot(gameState, bestHand);
                
                photonView.RPC("RPC_ShowBestHand", RpcTarget.All,
                    bestHandIndex, handPotAmount, gameState.SabaccPot, bestHandValue, handValues, bombedOut);
            }
            else
            {
                // Todos bombed out - los botes persisten
                photonView.RPC("RPC_ShowAllBombedOut", RpcTarget.All, handValues, bombedOut);
            }
        }
    }

    [PunRPC]
    void RPC_ShowWinner(int winnerIndex, string handType, int totalWon, int winnerHandValue, int[] handValues, bool[] bombedOut)
    {
        Player winner = gameState.Players[winnerIndex];
        string winnerName = winner.Name;
        
        // Construir mensaje con todas las manos
        string allHands = BuildAllHandsString(handValues, bombedOut);
        
        // Sabacc puro o Mano del Idiota terminan la partida
        bool isGameWin = handType.ToLower().Contains("sabacc") || handType.ToLower().Contains("idiota");
        
        // Actualizar creditos del ganador en todos los clientes
        if (isGameWin)
        {
            winner.AddCredits(totalWon); // totalWon = HandPot + SabaccPot
            gameState.HandPot = 0;
            gameState.SabaccPot = 0;
        }
        else
        {
            // Victoria de ronda normal: solo Hand Pot
            winner.AddCredits(gameState.HandPot);
            int sabaccPotRemaining = gameState.SabaccPot; // Persiste
            gameState.HandPot = 0;
        }
        
        // Sonido segun resultado
        if (AudioManager.Instance != null)
        {
            if (isGameWin)
            {
                AudioManager.Instance.PlaySabacc();
            }
            else if (winnerIndex == localPlayerIndex)
            {
                AudioManager.Instance.PlayWin();
            }
            else
            {
                AudioManager.Instance.PlayLose();
            }
        }
        
        /*
        if (isGameWin)
        {
            gameStateText.text = $"{winnerName} GANA LA PARTIDA!\n" +
                                $"Con {handType} ({winnerHandValue} puntos)\n" +
                                $"Gana {totalWon} creditos!\n\n" +
                                allHands +
                                "\n\nFIN DE LA PARTIDA!";
        }
        else
        {
            int handPotWon = totalWon - gameState.SabaccPot;
            gameStateText.text = $"{winnerName} GANA LA RONDA!\n" +
                                $"Mejor mano: {winnerHandValue} puntos\n" +
                                $"Gana bote de mano: {handPotWon} creditos\n" +
                                $"Bote Sabacc ({gameState.SabaccPot}) persiste\n\n" +
                                allHands +
                                "\n\nSiguiente ronda...";
        }
        */

        string mensaje = isGameWin
        ? $"{winnerName} GANA LA PARTIDA!\nCon {handType} ({winnerHandValue} puntos)\nGana {totalWon} creditos!\n{allHands}\nFIN DE LA PARTIDA!"
        : $"{winnerName} GANA LA RONDA!\nMejor mano: {winnerHandValue} puntos\nGana bote de mano: {totalWon - gameState.SabaccPot} creditos\nBote Sabacc ({gameState.SabaccPot}) persiste\n{allHands}\nSiguiente ronda...";

        MostrarPanelFinal(mensaje);
        
        UpdateAllPlayersHandValues();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            if (isGameWin)
            {
                StartCoroutine(DelayedRestartGame(10f));
            }
            else
            {
                StartCoroutine(DelayedPrepareNextRound(8f));
            }
        }
    }
    
    IEnumerator DelayedPrepareNextRound(float delay)
    {
        yield return new WaitForSeconds(delay);
        PrepareNextRound();
    }
    
    IEnumerator DelayedRestartGame(float delay)
    {
        yield return new WaitForSeconds(delay);
        RestartGame();
    }
    
    void RestartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
         // Reiniciar creditos de todos los jugadores
        foreach (var player in gameState.Players)
        {
            player.Credits = startingCredits;
            player.ResetForRound();
        }
        
        gameState.HandPot = 0;
        gameState.SabaccPot = 0;
        gameState.CurrentRound = 0;
        gameState.DealerIndex = 0;
        
        // Serializar creditos
        int[] playerCredits = new int[gameState.Players.Count];
        for (int i = 0; i < gameState.Players.Count; i++)
        {
            playerCredits[i] = startingCredits;
        }
        
        photonView.RPC("RPC_RestartGame", RpcTarget.All, playerCredits);
    }
    
    [PunRPC]
    void RPC_RestartGame(int[] playerCredits)
    {
        // Resetear estado
        for (int i = 0; i < gameState.Players.Count && i < playerCredits.Length; i++)
        {
            gameState.Players[i].Credits = playerCredits[i];
            gameState.Players[i].ResetForRound();
        }
        
        gameState.HandPot = 0;
        gameState.SabaccPot = 0;
        gameState.CallerIndex = -1;
        gameState.CurrentHighestBet = 0;
        
        CleanupAllCards();
        
        gameStateText.text = "Nueva partida comenzando...";
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DelayedStartNewRound(2f));
        }
    }
    
    IEnumerator DelayedStartNewRound(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNewRound();
    }

    [PunRPC]
    void RPC_ShowBestHand(int winnerIndex, int handPotWon, int sabaccPotRemaining, int winnerHandValue, int[] handValues, bool[] bombedOut)
    {
        Debug.Log($"[RPC_ShowBestHand] Ganador: {winnerIndex}, Pot: {handPotWon}, IsMaster: {PhotonNetwork.IsMasterClient}");
        
        Player winner = gameState.Players[winnerIndex];
        string winnerName = winner.Name;
        
        // Sonido segun si gane o perdi
        if (AudioManager.Instance != null)
        {
            if (winnerIndex == localPlayerIndex)
            {
                AudioManager.Instance.PlayWin();
                AudioManager.Instance.PlayChipsCollect();
            }
            else
            {
                AudioManager.Instance.PlayLose();
            }
        }
        
        // Actualizar creditos del ganador en todos los clientes
        winner.AddCredits(handPotWon);
        gameState.HandPot = 0;
        gameState.SabaccPot = sabaccPotRemaining;
        
        string allHands = BuildAllHandsString(handValues, bombedOut);
        
        /*
        gameStateText.text = $" {winnerName} gana la ronda\n" +
                            $"Mejor mano: {winnerHandValue} puntos\n" +
                            $"Gana bote de mano: {handPotWon} creditos\n" +
                            $"Bote Sabacc ({sabaccPotRemaining}) persiste\n\n" +
                            allHands;
        */

        string mensaje = $" {winnerName} gana la ronda\n" +
                     $"Mejor mano: {winnerHandValue} puntos\n" +
                     $"Gana bote de mano: {handPotWon} creditos\n" +
                     $"Bote Sabacc ({sabaccPotRemaining}) persiste\n" +
                     allHands;

        MostrarPanelFinal(mensaje);

        UpdateAllPlayersHandValues();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[RPC_ShowBestHand] Master programando PrepareNextRound en 8 segundos");
            StartCoroutine(DelayedPrepareNextRound(8f));
        }
    }

    [PunRPC]
    void RPC_ShowAllBombedOut(int[] handValues, bool[] bombedOut)
    {
        Debug.Log($"[RPC_ShowAllBombedOut] IsMaster: {PhotonNetwork.IsMasterClient}");
        
        // Sonido de bombed out
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBombedOut();
        
        string allHands = BuildAllHandsString(handValues, bombedOut);
        
        /*
        gameStateText.text = $"Todos Bombed Out!\n" +
                            $"Los botes persisten para la siguiente ronda\n\n" +
                            allHands;
        */

        string mensaje = $"Todos Bombed Out!\n" +
                     $"Los botes persisten para la siguiente ronda\n" +
                     allHands;

        MostrarPanelFinal(mensaje);

        UpdateAllPlayersHandValues();
        UpdateUI();
        
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[RPC_ShowAllBombedOut] Master programando PrepareNextRound en 8 segundos");
            StartCoroutine(DelayedPrepareNextRound(8f));
        }
    }

    string BuildAllHandsString(int[] handValues, bool[] bombedOut)
    {
        string result = "--- Manos ---\n";
        
        for (int i = 0; i < gameState.Players.Count && i < handValues.Length; i++)
        {
            string playerName = gameState.Players[i].Name;
            int value = handValues[i];
            
            if (bombedOut[i])
            {
                result += $"{playerName}: {value} (BOMBED OUT)\n";
            }
            else
            {
                result += $"{playerName}: {value} puntos\n";
            }
        }
        
        return result;
    }

    void PrepareNextRound()
    {
        Debug.Log($"[PrepareNextRound] Llamado. IsMaster: {PhotonNetwork.IsMasterClient}");
        if (!PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("[PrepareNextRound] Enviando RPC_CleanupRound");
        photonView.RPC("RPC_CleanupRound", RpcTarget.All);
    }

    [PunRPC]
    void RPC_CleanupRound()
    {
        Debug.Log($"[RPC_CleanupRound] Limpiando. IsMaster: {PhotonNetwork.IsMasterClient}");
        CleanupAllCards();

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[RPC_CleanupRound] Rotando dealer y empezando nueva ronda");
            gameLogic.RotateDealer(gameState);
            StartNewRound();
        }
    }

    void UpdateUI()
    {
        if (gameState == null) return;

        handPotText.text = $"Bote Mano: {gameState.HandPot}";
        sabaccPotText.text = $"Bote Sabacc: {gameState.SabaccPot}";
        
        // Actualizar creditos de todos los jugadores
        for (int i = 0; i < gameState.Players.Count && i < _creditsTexts.Count; i++)
        {
            if (_creditsTexts[i] != null)
            {
                Player player = gameState.Players[i];
                string statusSuffix = "";
                if (player.State == PlayerState.Folded) statusSuffix = " (Retirado)";
                else if (player.State == PlayerState.BombedOut) statusSuffix = " (Bombed Out)";
                
                _creditsTexts[i].text = $"{player.Name}: {player.Credits}{statusSuffix}";
            }
        }
        
        // Solo mostrar el valor de mano del jugador local (excepto en revelacion)
        UpdateLocalPlayerHandValue();
        
        if (phaseText != null)
            phaseText.text = $"Fase: {gameState.CurrentPhase}";
    }
    
    void UpdateLocalPlayerHandValue()
    {
        if (gameState == null) return;
        
        // Ocultar valores de mano de otros jugadores (excepto en Reveal)
        for (int i = 0; i < _handValueTexts.Count; i++)
        {
            if (_handValueTexts[i] == null) continue;
            
            // Verificar que el indice es valido para la lista de jugadores
            if (i >= gameState.Players.Count)
            {
                _handValueTexts[i].text = "";
                continue;
            }
            
            if (i == localPlayerIndex)
            {
                // Mostrar mi propio valor de mano
                int handValue = gameState.Players[i].Hand.GetTotal();
                string status = GetHandValueStatus(handValue);
                _handValueTexts[i].text = $"Tu mano: {handValue}{status}";
            }
            else if (gameState.CurrentPhase == GamePhase.Reveal)
            {
                // En revelacion, mostrar todos los valores
                int handValue = gameState.Players[i].Hand.GetTotal();
                string status = GetHandValueStatus(handValue);
                _handValueTexts[i].text = $"Mano: {handValue}{status}";
            }
            else
            {
                // Ocultar valores de otros jugadores
                _handValueTexts[i].text = "";
            }
        }
    }
    
    string GetHandValueStatus(int handValue)
    {
        if (handValue > 23 || handValue < -23)
            return " (BOMBED OUT!)";
        else if (handValue == 0)
            return " (0 = Bombed!)";
        else if (handValue > 20 || handValue < -20)
            return " (Limite!)";
        else if (handValue == 23 || handValue == -23)
            return " (SABACC!)";
        return "";
    }

    void UpdateCurrentPlayerHandValue()
    {
        UpdateLocalPlayerHandValue();
    }

    void UpdateAllPlayersHandValues()
    {
        if (gameState == null) return;
        
        for (int i = 0; i < gameState.Players.Count && i < _handValueTexts.Count; i++)
        {
            if (_handValueTexts[i] != null)
            {
                int handValue = gameState.Players[i].Hand.GetTotal();
                string playerName = gameState.Players[i].Name;
                
                // Indicar si esta bombed out
                if (handValue > 23 || handValue < -23 || handValue == 0)
                {
                    _handValueTexts[i].text = $"{playerName}: {handValue} (BOMBED)";
                }
                else
                {
                    _handValueTexts[i].text = $"{playerName}: {handValue}";
                }
            }
        }
    }

    void EnableButtonsForCurrentPlayer()
    {
        if (gameState.CurrentPlayerIndex == localPlayerIndex && !_isAnimating)
        {
            Player currentPlayer = gameState.CurrentPlayer;
            
            // Verificar limite antes de habilitar robar 
            drawButton.interactable = currentPlayer.Hand.GetCount() < maxCardsInHand;
            standButton.interactable = true;
            discardButton.interactable = false;
        }
    }

    void DisableAllButtons()
    {
        drawButton.interactable = false;
        standButton.interactable = false;
        discardButton.interactable = false;
    }

    void SwitchToCamera(int cameraIndex)
    {
        for (int i = 0; i < _playerCameras.Count; i++)
        {
            if (_playerCameras[i] != null)
            {
                _playerCameras[i].enabled = (i == cameraIndex);
            }
        }

        // Mostrar UI principal solo en cámaras de jugador
        if (mainUIPanel != null)
        {
            // Mostrar UI solo en cámaras de jugador (índices 0 a 3)
            bool showMainUI = cameraIndex >= 0 && cameraIndex <= 3; 
            mainUIPanel.SetActive(showMainUI);
        }
    }

    // Cambia a la camara del jugador LOCAL (cada cliente ve desde su perspectiva)
    void SwitchToMyCamera()
    {
        if (localPlayerIndex >= 0 && localPlayerIndex < 4)
        {
            SwitchToCamera(localPlayerIndex);
        }
        else
        {
            Debug.LogWarning($"[SwitchToMyCamera] localPlayerIndex invalido: {localPlayerIndex}, usando camara 0");
            SwitchToCamera(0);
        }
    }

    //  Selección de cartas con teclas 1-9 y shift para cambio de camara
    void Update()
    {
        // --- Gestión de Shift + cambio de cámara ---
        if (gameState != null)
        {
            // Solo permitir en fases donde el jugador controla su cámara
            bool canUseInterferenceView = gameState.CurrentPhase switch
            {
                GamePhase.FirstBetting => true,
                GamePhase.Calling => true,
                GamePhase.Drawing => true,
                GamePhase.SecondBetting => true,
                _ => false
            };

            if (canUseInterferenceView)
            {
                // Bloquear Shift si hay transición activa
                if (transitionPanel == null || !transitionPanel.activeSelf)
                {
                    bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                    if (isShiftPressed)
                    {
                        // Activar la cámara de campo de interferencia
                        SwitchToCamera(INTERFERENCE_CAMERA_INDEX);
                    }
                    else
                    {
                        // Volver a la cámara del jugador local
                        SwitchToMyCamera();
                    }
                }
            }
        }

        // --- Selección de cartas ---
        // Solo procesar input si es mi turno y no estoy animando
        if (gameState == null) return;
        if (gameState.CurrentPlayerIndex != localPlayerIndex) return;
        if (_isAnimating) return;
        
        // No procesar teclas si el input de apuestas esta activo
        if (betInputField != null && betInputField.isFocused) return;
        
        Player currentPlayer = gameState.CurrentPlayer;
        if (currentPlayer == null) return;

        int handCount = currentPlayer.Hand.GetCount();

        // Detectar teclas 1-9 para seleccionar cartas por indice en la mano
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                int cardIndex = i - 1;
                if (cardIndex < handCount)
                {
                    SelectCard(cardIndex);
                }
            }
        }
    }

    void SelectCard(int index)
    {
        Player currentPlayer = gameState.CurrentPlayer;
        int playerIndex = localPlayerIndex;

        // Si seleccionamos la misma carta, deseleccionar
        if (_selectedCardIndex == index)
        {
            // Sonido de deseleccionar
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCardDeselect();
            
            DeselectCurrentCard(playerIndex);
            _selectedCardIndex = -1;
            discardButton.interactable = false;
            UpdateProtectButtons();
            gameStateText.text = "Carta deseleccionada.";
            return;
        }

        // Sonido de seleccionar
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCardSelect();

        // Deseleccionar la carta anterior si habia una
        DeselectCurrentCard(playerIndex);
        _selectedCardIndex = index;

        // Obtener la carta del modelo
        SabaccCard card = currentPlayer.Hand.GetCards()[index];
        
        // Elevar la carta visual correspondiente
        GameObject cardInstance = FindCardInstance(playerIndex, card.GetCardId());
        if (cardInstance != null)
        {
            // Elevar 0.3 unidades
            cardInstance.transform.position += Vector3.up * 0.3f;
        }

        string protectedText = card.IsProtected() ? " [PROTEGIDA - en campo de interferencia]" : "";
        string keyText = card.IsProtected() ? "" : " Presiona Descartar para descartarla.";
        gameStateText.text = $"Carta {index + 1} seleccionada: {card.Name}{protectedText}.{keyText}";
        
        // Solo se puede descartar si no esta protegida y hay 3+ cartas
        discardButton.interactable = !card.IsProtected() && !LocalHasDiscarded && currentPlayer.Hand.GetCount() >= 3;
        UpdateProtectButtons();
    }

    // Busca la instancia visual de una carta por su ID
    GameObject FindCardInstance(int playerIndex, string cardId)
    {
        if (playerIndex < 0 || playerIndex >= _cardInstances.Count) return null;
        
        foreach (var card in _cardInstances[playerIndex])
        {
            if (card != null)
            {
                var cv = card.GetComponent<CardView>();
                if (cv != null && cv.GetCard() != null && cv.GetCard().GetCardId() == cardId)
                {
                    return card;
                }
            }
        }
        
        return null;
    }

    // Baja la carta actualmente seleccionada
    void DeselectCurrentCard(int playerIndex)
    {
        if (_selectedCardIndex < 0) return;
        if (gameState == null || playerIndex >= gameState.Players.Count) return;
        
        Player player = gameState.Players[playerIndex];
        if (_selectedCardIndex >= player.Hand.GetCount()) return;
        
        SabaccCard card = player.Hand.GetCards()[_selectedCardIndex];
        GameObject cardInstance = FindCardInstance(playerIndex, card.GetCardId());
        
        if (cardInstance != null)
        {
            // Bajar 0.3 unidades
            cardInstance.transform.position -= Vector3.up * 0.3f;
        }
    }

    void DeselectAllCards(int playerIndex)
    {
        DeselectCurrentCard(playerIndex);
    }

    //  DESCARTAR 

    public void OnDiscardCard()
    {
        if (_isAnimating) return;
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;

        SubmitCommand(CommandType.Discard, _selectedCardIndex);
    }

    // Master: quita la carta de la mano, la pone en el descarte y avisa a todos
    void ExecuteDiscard(int seat, int cardIndex)
    {
        Player player = gameState.Players[seat];
        SabaccCard discarded = player.Hand.RemoveCardAt(cardIndex);
        gameState.DiscardPile.Discard(discarded);
        
        photonView.RPC(nameof(RPC_PlayerDiscarded), RpcTarget.All, seat, cardIndex, discarded.GetCardId());
    }

    [PunRPC]
    void RPC_PlayerDiscarded(int playerIndex, int cardIndex, string cardId, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);
        // Tras descartar, ese jugador solo puede plantarse (lo valida CommandValidator)
        gameState.Players[playerIndex].HasDiscardedThisTurn = true;

        // Si no soy el Master, actualizar el modelo de datos
        if (!PhotonNetwork.IsMasterClient)
        {
            Player player = gameState.Players[playerIndex];
            if (cardIndex < player.Hand.GetCount())
            {
                player.Hand.RemoveCardAt(cardIndex);
            }
        }

        StartCoroutine(AnimateDiscardCard(playerIndex, cardIndex));
    }

    IEnumerator AnimateDiscardCard(int playerIndex, int cardIndex)
    {
        _isAnimating = true;
        DisableAllButtons();

        if (cardIndex < _cardInstances[playerIndex].Count)
        {
            var cardToDiscard = _cardInstances[playerIndex][cardIndex];
            
            // Eliminar de la lista de posiciones originales
            if (_originalCardPositions.ContainsKey(cardToDiscard))
            {
                _originalCardPositions.Remove(cardToDiscard);
            }
            
            _protectedCardInstances[playerIndex].Remove(cardToDiscard);
            
            _cardInstances[playerIndex].RemoveAt(cardIndex);

            // Animar movimiento a la pila de descarte
            yield return AnimationManager.Instance.MoveCard(
                cardToDiscard,
                discardArea.position,
                discardArea.rotation,
                useArc: true
            );

            // Anadir a la pila visual de descarte
            AddToVisualDiscardPile(cardToDiscard);

            // Reorganizar solo las cartas no protegidas en la mano
            yield return ReorganizeHandWithProtected(playerIndex);
        }

        _selectedCardIndex = -1;
        
        // Solo el jugador local actualiza su estado de descarte
        if (playerIndex == localPlayerIndex)
        {
            UpdateCurrentPlayerHandValue();
            
            //  Despues de descartar no se puede robar mas, solo plantarse
            gameStateText.text = "Carta descartada. Debes plantarte.";
            
            drawButton.interactable = false;  // no puede robar despues de descartar
            standButton.interactable = true;
            discardButton.interactable = false;
        }

        _isAnimating = false;
    }

    void AddToVisualDiscardPile(GameObject cardInstance)
    {
        int discardCount = _discardPileInstances.Count;
        Vector3 offset = Vector3.up * (discardCount * 0.002f);
        
        cardInstance.transform.SetParent(discardArea, true);
        cardInstance.transform.localPosition = offset;
        cardInstance.transform.localRotation = Quaternion.identity;
        cardInstance.transform.localScale = Vector3.one;
        
        // Voltear la carta boca arriba en el descarte
        var cardView = cardInstance.GetComponent<CardView>();
        if (cardView != null)
        {
            cardView.SetFaceUp(true);
        }
        
        _discardPileInstances.Add(cardInstance);
    }

    //  CAMPO DE INTERFERENCIA 

    public void OnProtectCard()
    {
        if (_isAnimating) return;
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;

        SubmitCommand(CommandType.Protect, _selectedCardIndex);
    }

    public void OnUnprotectCard()
    {
        if (_isAnimating) return;
        if (gameState == null || gameState.CurrentPlayerIndex != localPlayerIndex) return;

        SubmitCommand(CommandType.Unprotect, _selectedCardIndex);
    }

    // Master: marca/desmarca la carta como protegida y avisa a todos
    void ExecuteSetProtected(int seat, int cardIndex, bool isProtected)
    {
        SabaccCard card = gameState.Players[seat].Hand.GetCards()[cardIndex];
        card.SetProtected(isProtected);

        string eventName = isProtected ? nameof(RPC_CardProtected) : nameof(RPC_CardUnprotected);
        photonView.RPC(eventName, RpcTarget.All, seat, cardIndex, card.GetCardId());
    }

    int GetProtectedCardCount(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= gameState.Players.Count) return 0;
        
        int count = 0;
        foreach (var card in gameState.Players[playerIndex].Hand.GetCards())
        {
            if (card.IsProtected()) count++;
        }
        return count;
    }

    [PunRPC]
    void RPC_CardProtected(int playerIndex, int cardIndex, string cardId, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        // Actualizar modelo de datos si no soy Master
        if (!PhotonNetwork.IsMasterClient)
        {
            Player player = gameState.Players[playerIndex];
            if (cardIndex < player.Hand.GetCount())
            {
                player.Hand.GetCards()[cardIndex].SetProtected(true);
            }
        }

        // Animar carta al campo de interferencia
        StartCoroutine(AnimateProtectCard(playerIndex, cardIndex, cardId));
    }

    [PunRPC]
    void RPC_CardUnprotected(int playerIndex, int cardIndex, string cardId, PhotonMessageInfo info)
    {
        if (!IsFromMaster(info)) return; // solo el Master emite eventos de partida
        OnCommandResolved(playerIndex);

        // Actualizar modelo de datos si no soy Master
        if (!PhotonNetwork.IsMasterClient)
        {
            Player player = gameState.Players[playerIndex];
            if (cardIndex < player.Hand.GetCount())
            {
                player.Hand.GetCards()[cardIndex].SetProtected(false);
            }
        }

        // Animar carta de vuelta a la mano
        StartCoroutine(AnimateUnprotectCard(playerIndex, cardIndex, cardId));
    }

    IEnumerator AnimateProtectCard(int playerIndex, int cardIndex, string cardId)
    {
        _isAnimating = true;

        if (playerIndex >= _cardInstances.Count || cardIndex >= _cardInstances[playerIndex].Count)
        {
            Debug.LogWarning($"[AnimateProtectCard] a indices invalidos: player={playerIndex}, card={cardIndex}");
            _isAnimating = false;
            yield break;
        }

        // La carta esta en _cardInstances en el mismo indice que en Hand
        GameObject cardObject = _cardInstances[playerIndex][cardIndex];
        if (cardObject == null)
        {
            Debug.LogWarning($"[AnimateProtectCard] cardObject es null en indice {cardIndex}");
            _isAnimating = false;
            yield break;
        }

        // Verificar que el cardId coincide
        CardView cv = cardObject.GetComponent<CardView>();
        if (cv == null || cv.GetCard() == null || cv.GetCard().GetCardId() != cardId)
        {
            Debug.LogWarning($"[AnimateProtectCard] cardId no coincide. Esperado: {cardId}, Encontrado: {cv?.GetCard()?.GetCardId()}");
            _isAnimating = false;
            yield break;
        }

        // Obtener el campo de interferencia del jugador
        Transform interferenceField = _interferenceFields[playerIndex];
        if (interferenceField == null)
        {
            Debug.LogWarning($"[AnimateProtectCard] Campo de interferencia {playerIndex} no asignado");
            _isAnimating = false;
            yield break;
        }

        // Deseleccionar
        DeselectAllCards(playerIndex);
        _selectedCardIndex = -1;

        // Calcular posicion en el campo de interferencia
        int currentProtectedVisual = _protectedCardInstances[playerIndex].Count;
        Vector3 targetPos = interferenceField.position + interferenceField.right * (currentProtectedVisual * handCardSpacing);

        // Remover de posiciones originales de la mano
        if (_originalCardPositions.ContainsKey(cardObject))
        {
            _originalCardPositions.Remove(cardObject);
        }

        // Mover carta al campo de interferencia
        yield return AnimationManager.Instance.MoveCard(
            cardObject,
            targetPos,
            interferenceField.rotation,
            useArc: true
        );

        // Voltear boca arriba (todos pueden verla)
        CardView cardView = cardObject.GetComponent<CardView>();
        if (cardView != null && !cardView.IsFaceUp())
        {
            yield return AnimationManager.Instance.FlipCard(cardObject, true);
        }
        
        // Forzar la rotacion correcta del campo de interferencia (para todos los clientes)
        cardObject.transform.rotation = interferenceField.rotation;

        // Anadir a la lista visual de protegidas (para tracking)
        _protectedCardInstances[playerIndex].Add(cardObject);
        cardObject.transform.SetParent(interferenceField, true);
        
        // Guardar posicion original de la carta protegida para seleccion
        _originalCardPositions[cardObject] = cardObject.transform.position;

        // Reorganizar las cartas restantes en la mano (las no protegidas)
        yield return ReorganizeHandWithProtected(playerIndex);

        if (playerIndex == localPlayerIndex)
        {
            UpdateCurrentPlayerHandValue();
            gameStateText.text = "Carta protegida en el campo de interferencia.";
            UpdateProtectButtons();
        }

        _isAnimating = false;
    }

    // Reorganiza la mano mostrando solo las cartas no protegidas
    IEnumerator ReorganizeHandWithProtected(int playerIndex)
    {
        if (playerIndex >= _cardInstances.Count || playerIndex >= gameState.Players.Count)
            yield break;

        List<SabaccCard> cards = gameState.Players[playerIndex].Hand.GetCards();
        List<GameObject> unprotectedInstances = new List<GameObject>();

        // Recoger solo las instancias de cartas no protegidas
        for (int i = 0; i < cards.Count && i < _cardInstances[playerIndex].Count; i++)
        {
            if (!cards[i].IsProtected() && _cardInstances[playerIndex][i] != null)
            {
                unprotectedInstances.Add(_cardInstances[playerIndex][i]);
            }
        }

        // Reorganizar solo esas cartas en la mano
        if (unprotectedInstances.Count > 0)
        {
            yield return AnimationManager.Instance.ReorganizeHand(
                unprotectedInstances,
                _handAreas[playerIndex],
                _positionCalculator,
                _originalCardPositions
            );
        }
    }

    IEnumerator AnimateUnprotectCard(int playerIndex, int cardIndex, string cardId)
    {
        _isAnimating = true;

        if (playerIndex >= _cardInstances.Count || cardIndex >= _cardInstances[playerIndex].Count)
        {
            Debug.LogWarning($"[AnimateUnprotectCard] a indices invalidos: player={playerIndex}, card={cardIndex}");
            _isAnimating = false;
            yield break;
        }

        // La carta esta en _cardInstances en el mismo indice que en Hand
        GameObject cardObject = _cardInstances[playerIndex][cardIndex];
        if (cardObject == null)
        {
            Debug.LogWarning($"[AnimateUnprotectCard] cardObject es null en indice {cardIndex}");
            _isAnimating = false;
            yield break;
        }

        // Deseleccionar
        DeselectAllCards(playerIndex);
        _selectedCardIndex = -1;

        // Remover de la lista visual de protegidas
        _protectedCardInstances[playerIndex].Remove(cardObject);

        // Reparentar a la mano
        cardObject.transform.SetParent(_handAreas[playerIndex], true);

        // Voltear boca abajo si no es mi carta
        if (playerIndex != localPlayerIndex)
        {
            CardView cardView = cardObject.GetComponent<CardView>();
            if (cardView != null)
            {
                cardView.SetFaceUp(false);
            }
        }

        // Reorganizar el campo de interferencia (las que quedan)
        ReorganizeInterferenceField(playerIndex);

        // Reorganizar la mano (todas las no protegidas)
        yield return ReorganizeHandWithProtected(playerIndex);

        if (playerIndex == localPlayerIndex)
        {
            UpdateCurrentPlayerHandValue();
            gameStateText.text = "Carta retirada del campo de interferencia.";
            UpdateProtectButtons();
        }

        _isAnimating = false;
    }

    void ReorganizeInterferenceField(int playerIndex)
    {
        Transform interferenceField = _interferenceFields[playerIndex];
        if (interferenceField == null) return;

        for (int i = 0; i < _protectedCardInstances[playerIndex].Count; i++)
        {
            var card = _protectedCardInstances[playerIndex][i];
            if (card != null)
            {
                Vector3 targetPos = interferenceField.position + interferenceField.right * (i * handCardSpacing);
                card.transform.position = targetPos;
                card.transform.rotation = interferenceField.rotation; // Asegurar rotacion correcta
                
                // Guardar posicion original para la seleccion
                _originalCardPositions[card] = targetPos;
            }
        }
    }

    void UpdateProtectButtons()
    {
        if (protectButton == null || unprotectButton == null) return;
        if (gameState == null) return;
        if (gameState.CurrentPlayerIndex != localPlayerIndex) return;

        Player currentPlayer = gameState.CurrentPlayer;
        int protectedCount = GetProtectedCardCount(localPlayerIndex);
        int totalCards = currentPlayer.Hand.GetCount();

        bool canProtect = false;
        bool canUnprotect = false;

        if (_selectedCardIndex >= 0 && _selectedCardIndex < totalCards)
        {
            SabaccCard card = currentPlayer.Hand.GetCards()[_selectedCardIndex];
            
            canProtect = !card.IsProtected() && 
                         protectedCount < maxProtectedCards &&
                         (totalCards - protectedCount) > 2;
            
            canUnprotect = card.IsProtected();
        }

        protectButton.interactable = canProtect;
        unprotectButton.interactable = canUnprotect;
    }

    // Limpiar campo de interferencia al final de la ronda
    // No destruimos las cartas aqui porque estan en _cardInstances y se destruyen en CleanupAllCards
    void CleanupInterferenceFields()
    {
        for (int i = 0; i < _protectedCardInstances.Count; i++)
        {
            _protectedCardInstances[i].Clear();
        }
    }

    void MostrarPanelFinal(string message)
    {
        if (panelFinal != null && panelFinalText != null)
        {
            panelFinalText.text = message;
            panelFinal.SetActive(true);
        }
    }

    public void ActivarMenu()
    {
        panelMenu.SetActive(true);
    }

    public void Continuar()
    {
        panelMenu.SetActive(false);
    }

    // Función para mostrar el panel de reglas
    public void AbrirReglas()
    {
        if (panelReglas != null && panelMenu != null)
        {
            panelReglas.SetActive(true);
            panelMenu.SetActive(false); // Ocultamos el panel menu
        }
    }

    public void VolverAlMenu()
    {
        if (panelReglas != null && panelMenu != null && panelAudio != null)
        {
            panelReglas.SetActive(false);
            panelAudio.SetActive(false);
            panelMenu.SetActive(true); // Activamos el panel menu
        }
    }

    public void MostrarAudio()
    {
        if (panelAudio != null && panelMenu != null && panelReglas != null)
        {
            panelAudio.SetActive(true);
            panelMenu.SetActive(false); // Ocultamos el panel menu
            panelReglas.SetActive(false);
        }
    }

    // Si es Master saca a todos, si no solo sale el
    public void Salir()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Notifica a todos que deben salir
            photonView.RPC("RPC_ForzarSalidaTodos", RpcTarget.All);
        }
        else
        {
            StartCoroutine(SalirYLimpiar());
        }
    }

    [PunRPC]
    void RPC_ForzarSalidaTodos()
    {
        StartCoroutine(SalirYLimpiar()); // Todos salen
    }

    // Coroutine que limpia todo correctamente antes de salir
    IEnumerator SalirYLimpiar()
    {
        if (panelFinal != null)
            panelFinal.SetActive(false);
        
        // Parar la musica
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        // Limpiar todas las cartas visuales
        CleanupAllCards();

        // Salir de la sala de Photon
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        // Esperar a que se complete la desconexion de la sala
        while (PhotonNetwork.InRoom)
        {
            yield return null;
        }

        // Desconectarse del servidor de Photon completamente
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        // Esperar a que se complete la desconexion
        while (PhotonNetwork.IsConnected)
        {
            yield return null;
        }
        
        /*
        // Destruir el AudioManager singleton para que se recree
        if (AudioManager.Instance != null)
        {
            Destroy(AudioManager.Instance.gameObject);
        }
        */

        // Destruir el AnimationManager singleton para que se recree
        if (AnimationManager.Instance != null)
        {
            Destroy(AnimationManager.Instance.gameObject);
        }
       
        // Cargar escena de inicio
        //SceneManager.LoadScene("Inicio");
        AudioManager.Instance.LoadSceneWithMusicFade("Inicio");
    }

    // Callback de Photon cuando un jugador sale de la sala
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.Log($"[NetworkGameController] Jugador {otherPlayer.NickName} salio de la sala");

        // Si quedan menos de 2 jugadores, terminar para todos
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            gameStateText.text = "No hay suficientes jugadores. Volviendo al inicio...";
            StartCoroutine(SalirYLimpiar());
            return;
        }

        // Si el juego esta en curso, actualizar el estado
        if (gameState != null)
        {
            // Encontrar el asiento del jugador que salio (por su id, no por "ActorNumber - 1")
            int leftPlayerIndex = SeatIndexOf(otherPlayer);

            // Si ese jugador estaba en la partida, marcarlo como retirado
            if (leftPlayerIndex >= 0 && leftPlayerIndex < gameState.Players.Count)
            {
                gameState.Players[leftPlayerIndex].Fold();
                gameStateText.text = $"{otherPlayer.NickName} abandono la partida.";

                // Actualizar UI
                UpdateUI();

                // Si era el turno del jugador que salio, avanzar al siguiente
                if (gameState.CurrentPlayerIndex == leftPlayerIndex && PhotonNetwork.IsMasterClient)
                {
                    // Buscar siguiente jugador activo
                    int nextActive = GetNextActivePlayerFromIndex(leftPlayerIndex);
                    if (nextActive >= 0)
                    {
                        gameState.CurrentPlayerIndex = nextActive;
                        photonView.RPC("RPC_UpdateCurrentPlayer", RpcTarget.All, nextActive);
                    }
                }
            }
        }
    }

    // Busca el siguiente jugador activo desde un indice
    int GetNextActivePlayerFromIndex(int fromIndex)
    {
        if (gameState == null) return -1;

        int count = gameState.Players.Count;
        for (int i = 1; i <= count; i++)
        {
            int nextIndex = (fromIndex + i) % count;
            if (gameState.Players[nextIndex].State == PlayerState.Active)
            {
                return nextIndex;
            }
        }
        return -1;
    }

    [PunRPC]
    void RPC_UpdateCurrentPlayer(int newIndex)
    {
        if (gameState != null)
        {
            gameState.CurrentPlayerIndex = newIndex;
            UpdateUI();
            
            // Si es mi turno, mostrar controles
            if (newIndex == localPlayerIndex)
            {
                if (gameState.CurrentPhase == GamePhase.Drawing)
                {
                    ShowTransitionScreen();
                }
                else if (gameState.CurrentPhase == GamePhase.FirstBetting || 
                         gameState.CurrentPhase == GamePhase.SecondBetting ||
                         gameState.CurrentPhase == GamePhase.Calling)
                {
                    StartBettingRound();
                }
            }
        }
    }

    // Callback cuando cambia el Master Client (si el anterior se fue)
    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        Debug.Log($"[NetworkGameController] Nuevo Master Client: {newMasterClient.NickName}");
        
        // Si ahora soy el Master y hay una ronda en curso, continuar
        if (PhotonNetwork.IsMasterClient && gameState != null && gameState.IsRoundActive)
        {
            Debug.Log("[NetworkGameController] Soy el nuevo Master, continuando la partida...");
        }
    }
}