// Diferentes estados del Jugador
public enum PlayerState
{
    Active,   // Está jugando
    Folded,   // Se ha retirado
    BombedOut // Explotó
}

// Clase que representa a un jugador
public class Player
{
    public readonly PlayerId Id; // Identidad estable (ActorNumber en red)
    public string Name;       // Nombre del jugador
    public int Credits;       // Créditos iniciales
    public Hand Hand;         // Mano de cartas
    public PlayerState State; // Estado del jugador
    
    // Sistema de apuestas
    public int CurrentBet;    // Apuesta actual en esta ronda de apuestas
    public int TotalBetThisRound; // Total apostado en la ronda actual
    public bool HasActedThisBettingRound; // Si ya actuó en esta ronda de apuestas
    public bool HasDiscardedThisTurn;     // Si ya descartó en su turno de robo (tras descartar solo puede plantarse)

    // Constructor de la clase Jugador
    public Player(PlayerId id, string name, int credits)
    {
        Id = id;
        Name = name;
        Credits = credits;
        Hand = new Hand();
        State = PlayerState.Active;
        CurrentBet = 0;
        TotalBetThisRound = 0;
        HasActedThisBettingRound = false;
    }
  
    // Indica si puede apostar
    public bool CanAfford(int amount)
    {
        return Credits >= amount;
    }

    // Restar créditos
    public void DeductCredits(int amount)
    {
        Credits -= amount;
    }
  
    // Sumar créditos
    public void AddCredits(int amount)
    {
        Credits += amount;
    }
  
    // Cambiar el estado a retirado
    public void Fold()
    {
        State = PlayerState.Folded;
    }
  
    // Cambiar el estado a explotado
    public void MarkAsBombedOut()
    {
        State = PlayerState.BombedOut;
    }
  
    // Limpia la mano y marca al jugador otra vez como activo
    public void ResetForRound()
    {
        Hand.Clear();
        State = PlayerState.Active;
        CurrentBet = 0;
        TotalBetThisRound = 0;
        HasActedThisBettingRound = false;
        HasDiscardedThisTurn = false;
    }
    
    // Resetea solo las apuestas para nueva ronda de apuestas
    public void ResetBettingRound()
    {
        CurrentBet = 0;
        HasActedThisBettingRound = false;
    }
}
