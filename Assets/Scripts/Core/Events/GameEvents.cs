using System.Collections.Generic;

/// <summary>
/// Un EVENTO es un hecho que ya ha ocurrido en la partida ("Ana robó el 7 de
/// Sables", "Bea subió 20"). Solo el Master los genera (GameEngine), los envía
/// a todos, y TODOS (Master incluido) los aplican con GameReducer.Apply.
///
/// Diferencia con un comando: el comando es una petición que puede rechazarse;
/// el evento es definitivo y no se valida al recibirlo, solo se aplica.
/// </summary>
public enum GameEventType : byte
{
    RoundStarted = 1,
    PhaseChanged,
    TurnChanged,
    PlayerChecked,
    BetPlaced,
    BetMatched,
    PlayerCalled,
    PlayerFolded,
    CardDrawn,
    PlayerStood,
    CardDiscarded,
    CardProtectionChanged,
    CardsShifted,
    PenaltyPaid,
    PlayerBombedOut,
    PotAwarded,
    RoundEnded,
    PlayerLeft,
    GameOver,
    GameRestarted
}

public abstract class GameEvent
{
    public abstract GameEventType Type { get; }
    public override string ToString() => Type.ToString();
}

// ===== RONDA Y FASES =====

/// <summary>Empieza una ronda: apuestas iniciales ya cobradas y cartas repartidas.</summary>
public sealed class RoundStarted : GameEvent
{
    public override GameEventType Type => GameEventType.RoundStarted;
    public int Round;
    public int DealerIndex;
    public int CurrentPlayerIndex;
    public int HandPot;
    public int SabaccPot;
    public int[] Credits;           // créditos de cada asiento tras pagar la apuesta inicial
    public PlayerState[] States;    // Active, o Folded si no podía pagar
    public string[][] Hands;        // IDs de las cartas repartidas a cada asiento
}

/// <summary>Cambia la fase. Si FirstPlayerIndex >= 0, también el jugador en turno.</summary>
public sealed class PhaseChanged : GameEvent
{
    public override GameEventType Type => GameEventType.PhaseChanged;
    public GamePhase Phase;
    public int FirstPlayerIndex = -1;
}

public sealed class TurnChanged : GameEvent
{
    public override GameEventType Type => GameEventType.TurnChanged;
    public int PlayerIndex;
}

// ===== APUESTAS =====

public sealed class PlayerChecked : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerChecked;
    public int PlayerIndex;
}

/// <summary>Iguala lo pendiente y sube RaiseAmount por encima.</summary>
public sealed class BetPlaced : GameEvent
{
    public override GameEventType Type => GameEventType.BetPlaced;
    public int PlayerIndex;
    public int RaiseAmount;
}

public sealed class BetMatched : GameEvent
{
    public override GameEventType Type => GameEventType.BetMatched;
    public int PlayerIndex;
    public int Amount;
}

public sealed class PlayerCalled : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerCalled;
    public int PlayerIndex;
}

public sealed class PlayerFolded : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerFolded;
    public int PlayerIndex;
    public int Penalty;             // va al bote de Sabacc
}

// ===== CARTAS =====

public sealed class CardDrawn : GameEvent
{
    public override GameEventType Type => GameEventType.CardDrawn;
    public int PlayerIndex;
    public string CardId;
}

public sealed class PlayerStood : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerStood;
    public int PlayerIndex;
    public int NextPlayerIndex;
}

public sealed class CardDiscarded : GameEvent
{
    public override GameEventType Type => GameEventType.CardDiscarded;
    public int PlayerIndex;
    public int CardIndex;
    public string CardId;
}

public sealed class CardProtectionChanged : GameEvent
{
    public override GameEventType Type => GameEventType.CardProtectionChanged;
    public int PlayerIndex;
    public int CardIndex;
    public bool IsProtected;
    public string CardId;
}

public enum ShiftKind : byte
{
    First,
    Second,
    AfterCall
}

public struct CardShift
{
    public int PlayerIndex;
    public int CardIndex;
    public string OldCardId;
    public string NewCardId;
}

/// <summary>Resultado de un shift. Los cambios se aplican en orden.</summary>
public sealed class CardsShifted : GameEvent
{
    public override GameEventType Type => GameEventType.CardsShifted;
    public ShiftKind Kind;
    public List<CardShift> Shifts = new List<CardShift>();
}

// ===== LIQUIDACIÓN =====

public enum PenaltyReason : byte
{
    BombedOut,
    FailedCall
}

/// <summary>Un jugador paga una penalización al bote de Sabacc.</summary>
public sealed class PenaltyPaid : GameEvent
{
    public override GameEventType Type => GameEventType.PenaltyPaid;
    public int PlayerIndex;
    public int Amount;
    public PenaltyReason Reason;
}

public sealed class PlayerBombedOut : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerBombedOut;
    public int PlayerIndex;
}

/// <summary>Un jugador cobra de los botes.</summary>
public sealed class PotAwarded : GameEvent
{
    public override GameEventType Type => GameEventType.PotAwarded;
    public int PlayerIndex;
    public int FromHandPot;
    public int FromSabaccPot;
}

public enum RoundOutcome : byte
{
    DefinitiveWin,      // Sabacc Puro o Mano del Idiota: gana ambos botes y termina la partida
    BestHand,           // gana el bote de mano; el de Sabacc se acumula
    AllBombedOut,       // nadie gana; los botes se acumulan
    LastPlayerStanding  // todos los demás se retiraron
}

/// <summary>Resumen de cómo terminó la ronda (para mostrarlo). El dinero ya se movió con PenaltyPaid/PotAwarded.</summary>
public sealed class RoundEnded : GameEvent
{
    public override GameEventType Type => GameEventType.RoundEnded;
    public RoundOutcome Outcome;
    public int WinnerIndex = -1;
    public string HandType = "";
    public int WinnerHandValue;
    public int AmountWon;
    public int[] HandValues = new int[0];
    public bool[] BombedOut = new bool[0];
}

// ===== PARTIDA =====

public sealed class PlayerLeft : GameEvent
{
    public override GameEventType Type => GameEventType.PlayerLeft;
    public int PlayerIndex;
}

/// <summary>Fin de la partida por créditos. WinnerIndex = -1 si nadie puede seguir.</summary>
public sealed class GameOver : GameEvent
{
    public override GameEventType Type => GameEventType.GameOver;
    public int WinnerIndex = -1;
    public int AmountWon;
}

public sealed class GameRestarted : GameEvent
{
    public override GameEventType Type => GameEventType.GameRestarted;
    public int StartingCredits;
}
