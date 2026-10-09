using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameState - SOLO ALMACENA DATOS
/// Sin lógica de juego, solo propiedades públicas y getters útiles
/// Serializable para Photon
/// </summary>
public class GameState
{
    // ===== DATOS PRIVADOS =====
    private List<Player> players;
    private int currentPlayerIndex;
    private int dealerIndex;
    private Deck mainDeck;
    private DiscardPile discardPile;
    private int handPot;
    private int sabaccPot;
    private GamePhase currentPhase;
    private int currentRound;
    private bool isRoundActive;
    private int currentHighestBet;
    private int callerIndex = -1;
    private int playersStood;
    private bool deckIsKnown;

    // ===== PROPIEDADES PÚBLICAS (SOLO LECTURA) =====

    public List<Player> Players
    {
        get { return players; }
    }

    public int CurrentPlayerIndex
    {
        get { return currentPlayerIndex; }
        set { currentPlayerIndex = value; }
    }

    public Player CurrentPlayer
    {
        get { return players[currentPlayerIndex]; }
    }

    public int DealerIndex
    {
        get { return dealerIndex; }
        set { dealerIndex = value; }
    }

    public Player Dealer
    {
        get { return players[dealerIndex]; }
    }

    public Deck MainDeck
    {
        get { return mainDeck; }
        set { mainDeck = value; }
    }

    public DiscardPile DiscardPile
    {
        get { return discardPile; }
        set { discardPile = value; }
    }

    public int HandPot
    {
        get { return handPot; }
        set { handPot = value; }
    }

    public int SabaccPot
    {
        get { return sabaccPot; }
        set { sabaccPot = value; }
    }

    public GamePhase CurrentPhase
    {
        get { return currentPhase; }
        set { currentPhase = value; }
    }

    public int CurrentRound
    {
        get { return currentRound; }
        set { currentRound = value; }
    }

    public bool IsRoundActive
    {
        get { return isRoundActive; }
        set { isRoundActive = value; }
    }

    /// <summary>Apuesta más alta de la ronda de apuestas actual (lo que hay que igualar)</summary>
    public int CurrentHighestBet
    {
        get { return currentHighestBet; }
        set { currentHighestBet = value; }
    }

    /// <summary>Asiento del jugador que hizo CALL en esta fase, o -1 si nadie</summary>
    public int CallerIndex
    {
        get { return callerIndex; }
        set { callerIndex = value; }
    }

    public bool SomeoneCalled => callerIndex >= 0;

    /// <summary>Jugadores que ya se han plantado en la fase de robo actual</summary>
    public int PlayersStood
    {
        get { return playersStood; }
        set { playersStood = value; }
    }

    /// <summary>
    /// true solo en la autoridad (el Master): es quien tiene el mazo real.
    /// Los clientes no conocen el orden del mazo (información oculta), así que
    /// el reducer crea las cartas a partir de su ID en lugar de sacarlas del mazo.
    /// </summary>
    public bool DeckIsKnown
    {
        get { return deckIsKnown; }
        set { deckIsKnown = value; }
    }

    /// <summary>Cuánto le falta a un jugador para igualar la apuesta más alta</summary>
    public int AmountToCall(Player player)
    {
        return currentHighestBet - player.CurrentBet;
    }

    // ===== CONSTRUCTOR =====

    public GameState(IList<Seat> seats, int startingCredits)
    {
        players = new List<Player>();
        foreach (Seat seat in seats)
        {
            if (IndexOf(seat.Id) >= 0)
                throw new ArgumentException($"Asiento duplicado para {seat.Id}");
            players.Add(new Player(seat.Id, seat.Name, startingCredits));
        }

        dealerIndex = 0;
        currentPlayerIndex = 0;
        currentRound = 0;
        isRoundActive = false;
        handPot = 0;
        sabaccPot = 0;
        currentPhase = GamePhase.InitialBetting;

        mainDeck = new Deck();
        discardPile = new DiscardPile();
    }

    // ===== IDENTIDAD =====

    /// <summary>Índice (asiento) del jugador con ese id, o -1 si no está en la partida</summary>
    public int IndexOf(PlayerId id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
                return i;
        }
        return -1;
    }

    /// <summary>Jugador con ese id, o null si no está en la partida</summary>
    public Player GetPlayer(PlayerId id)
    {
        int index = IndexOf(id);
        return index >= 0 ? players[index] : null;
    }

    // ===== GETTERS ÚTILES (SIN LÓGICA) =====

    public List<Player> GetActivePlayers()
    {
        return players.Where(p => p.State == PlayerState.Active).ToList();
    }

    public List<Player> GetPlayersInGame()
    {
        return players.Where(p => p.State != PlayerState.Folded).ToList();
    }

    public int GetActivePlayerCount()
    {
        return players.Count(p => p.State == PlayerState.Active);
    }

    public int GetFoldedPlayerCount()
    {
        return players.Count(p => p.State == PlayerState.Folded);
    }

    public int GetBombedOutPlayerCount()
    {
        return players.Count(p => p.State == PlayerState.BombedOut);
    }

    public bool IsValidGameState()
    {
        if (players == null || players.Count == 0)
            return false;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count)
            return false;
        if (dealerIndex < 0 || dealerIndex >= players.Count)
            return false;
        if (handPot < 0 || sabaccPot < 0)
            return false;
        return true;
    }

    public bool CanPlayerAct(Player player)
    {
        if (player == null)
            return false;
        if (player.State != PlayerState.Active)
            return false;
        if (player.Credits <= 0)
            return false;
        return true;
    }

    // ===== PARA DEBUGGING =====

    public string GetDebugSnapshot()
    {
        string snapshot = $@"
=== GAME STATE SNAPSHOT ===
Ronda: {currentRound}
Fase: {currentPhase}
Jugadores Activos: {GetActivePlayerCount()}/{players.Count}
Jugador Actual: {CurrentPlayer?.Name}
Dealer: {Dealer?.Name}
Bote Mano: {handPot}
Bote Sabacc: {sabaccPot}
Mazo Restante: {mainDeck?.GetCount() ?? 0}
===========================";
        return snapshot;
    }

    public string SerializeToString()
    {
        string state = $"Round:{currentRound}|Phase:{currentPhase}|HandPot:{handPot}|SabaccPot:{sabaccPot}";
        return state;
    }
}

/// <summary>
/// GamePhase enum - Define todas las fases del juego
/// </summary>
public enum GamePhase
{
    InitialBetting,     // Apuesta inicial al Hand Pot y Sabacc Pot
    Dealing,            // Reparto de cartas
    FirstBetting,       // Primera ronda de apuestas (después del reparto)
    FirstShift,         // Primer shift
    Calling,            // Fase de calling (pueden hacer call para forzar revelación)
    Drawing,            // Fase de robo/descarte
    SecondBetting,      // Segunda ronda de apuestas
    SecondShift,        // Segundo shift
    Reveal,             // Revelación de manos
    Tiebreaker          // Desempate (robar carta extra)
}