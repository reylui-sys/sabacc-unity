/// <summary>
/// Parámetros de reglas que necesitan el validador y el motor. Se construye desde
/// los campos del inspector de NetworkGameController; en los tests se crea a mano.
/// </summary>
public sealed class RulesConfig
{
    /// <summary>Máximo de cartas en la mano (no se puede robar más)</summary>
    public int MaxCardsInHand = 9;

    /// <summary>Máximo de cartas en el campo de interferencia</summary>
    public int MaxProtectedCards = 2;

    /// <summary>Cartas sin proteger que deben quedar en la mano tras proteger una</summary>
    public int MinUnprotectedCards = 2;

    /// <summary>Cartas mínimas en la mano para poder descartar (tras descartar quedan 2)</summary>
    public int MinCardsToDiscard = 3;

    /// <summary>Penalización por retirarse en la primera ronda de apuestas (va al bote de Sabacc)</summary>
    public int FoldPenaltyFirstBetting = 10;

    /// <summary>Créditos con los que empieza (o reempieza) cada jugador</summary>
    public int StartingCredits = 100;

    /// <summary>Apuesta inicial a CADA bote al empezar la ronda (se paga el doble en total)</summary>
    public int InitialBet = 10;

    /// <summary>Penalización por explotar (bomb out) en la revelación</summary>
    public int BombedOutPenalty = 50;

    /// <summary>Penalización por hacer CALL y no ganar la ronda</summary>
    public int CallPenalty = 10;
}
