using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameLogic - TODA LA LÓGICA DE REGLAS DEL SABACC
/// Recibe GameState, lo modifica según las reglas
/// NO tiene referencias a UI ni animaciones
/// </summary>
public class GameLogic
{
    private System.Random _random = new System.Random();

    // ===== CLASE PARA RESULTADOS DE SHIFTING =====
    
    /// <summary>Información sobre un cambio de carta durante el shifting</summary>
    public class ShiftResult
    {
        public int PlayerIndex;
        public int CardIndex;
        public string OldCardId;
        public string NewCardId;
        
        public ShiftResult(int playerIndex, int cardIndex, string oldCardId, string newCardId)
        {
            PlayerIndex = playerIndex;
            CardIndex = cardIndex;
            OldCardId = oldCardId;
            NewCardId = newCardId;
        }
    }

    // ===== INICIALIZACIÓN =====

    /// <summary>Inicializa una nueva ronda</summary>
    public void InitializeNewRound(GameState state)
    {
        state.CurrentRound++;
        state.MainDeck = new Deck(SabaccCardDefinitions.CreateFullDeck());
        state.MainDeck.Shuffle();
        state.DeckIsKnown = true; // quien inicializa la ronda es la autoridad: conoce el mazo
        state.DiscardPile = new DiscardPile();
        state.HandPot = 0;
        // NO resetear SabaccPot - persiste entre rondas
        state.CurrentPhase = GamePhase.FirstShift;
        state.IsRoundActive = true;

        // Resetear estado de todos los jugadores
        foreach (Player player in state.Players)
        {
            player.ResetForRound();
        }
    }

    /// <summary>Reparte 2 cartas iniciales a cada jugador activo</summary>
    public void DealInitialCards(GameState state)
    {
        foreach (Player player in state.Players)
        {
            if (player.State == PlayerState.Active)
            {
                SabaccCard card1 = state.MainDeck.Draw();
                SabaccCard card2 = state.MainDeck.Draw();

                if (card1 != null) player.Hand.AddCard(card1);
                if (card2 != null) player.Hand.AddCard(card2);
            }
        }

        state.CurrentPhase = GamePhase.FirstShift;
    }

    // ===== APUESTAS =====

    /// <summary>Procesa la apuesta inicial de un jugador</summary>
    public bool PlayerBet(GameState state, Player player, int amount)
    {
        if (!player.CanAfford(amount))
            return false;

        player.DeductCredits(amount);
        state.HandPot += amount;
        state.SabaccPot += amount;
        return true;
    }

    /// <summary>Marca un jugador como fold</summary>
    public void PlayerFold(Player player)
    {
        player.Fold();
    }

    /// <summary>Añade dinero a los botes</summary>
    public void AddToPots(GameState state, int handAmount, int sabaccAmount)
    {
        state.HandPot += handAmount;
        state.SabaccPot += sabaccAmount;
    }

    // ===== CARTAS =====

    /// <summary>Roba una carta del mazo</summary>
    public SabaccCard DrawCard(GameState state)
    {
        return state.MainDeck.Draw();
    }

    /// <summary>Descarta una carta a la pila de descarte</summary>
    public void DiscardCard(GameState state, SabaccCard card)
    {
        state.DiscardPile.Discard(card);
    }

    // ===== SHIFTING (crucial para multijugador) =====

    /// <summary>
    /// Aplica un shift directamente sobre el estado (atajo para la autoridad y los tests).
    /// Usa el mismo camino que la partida en red: GameEngine.PlanShifting decide los
    /// cambios y GameReducer los aplica. Devuelve los cambios ocurridos.
    /// </summary>
    public List<ShiftResult> ApplyShifting(GameState state, float shiftProbability)
    {
        CardsShifted shifted = GameEngine.PlanShifting(state, shiftProbability, _random, ShiftKind.First);
        GameReducer.Apply(state, shifted);

        var changes = new List<ShiftResult>();
        foreach (CardShift shift in shifted.Shifts)
            changes.Add(new ShiftResult(shift.PlayerIndex, shift.CardIndex, shift.OldCardId, shift.NewCardId));

        CoreLog.Info($"[ApplyShifting] Total de cartas cambiadas: {changes.Count}");
        return changes;
    }

    // ===== TURNO DE JUGADORES =====

    /// <summary>Avanza al siguiente jugador activo</summary>
    public void NextPlayer(GameState state)
    {
        int startIndex = state.CurrentPlayerIndex;
        do
        {
            state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.Players.Count;
            if (state.CurrentPlayerIndex == startIndex)
            {
                break;
            }
        } while (state.Players[state.CurrentPlayerIndex].State != PlayerState.Active);
    }

    /// <summary>Rota el dealer al siguiente jugador</summary>
    public void RotateDealer(GameState state)
    {
        state.DealerIndex = (state.DealerIndex + 1) % state.Players.Count;
        state.CurrentPlayerIndex = GetNextActivePlayerIndex(state, state.DealerIndex);
    }

    /// <summary>Obtiene el siguiente jugador activo desde un índice</summary>
    private int GetNextActivePlayerIndex(GameState state, int fromIndex)
    {
        int nextIndex = (fromIndex + 1) % state.Players.Count;
        int iterations = 0;

        while (state.Players[nextIndex].State != PlayerState.Active && iterations < state.Players.Count)
        {
            nextIndex = (nextIndex + 1) % state.Players.Count;
            iterations++;
        }

        return nextIndex;
    }

    /// <summary>Verifica si un jugador es el turno actual</summary>
    public bool IsPlayerTurn(GameState state, Player player)
    {
        return state.Players[state.CurrentPlayerIndex] == player;
    }

    // ===== BOMB OUTS =====

    /// <summary>Procesa bomb outs (jugadores cuya mano es bomb out)</summary>
    public void ProcessBombOuts(GameState state)
    {
        foreach (Player player in state.Players)
        {
            if (player.State == PlayerState.Active && player.Hand.IsBombOut())
            {
                player.MarkAsBombedOut();

                // Pagan al bote de Sabacc la cantidad del bote de mano
                int penalty = state.HandPot;
                if (player.CanAfford(penalty))
                {
                    player.DeductCredits(penalty);
                    state.SabaccPot += penalty;
                }
            }
        }
    }

    // ===== GANADOR =====

    /// <summary>Obtiene el ganador definitivo (Idiots Array o Pure Sabacc)</summary>
    public Player GetDefinitiveWinner(GameState state, out string handType)
    {
        handType = "";

        // Busca Idiots Array (gana a todo)
        foreach (Player player in state.GetActivePlayers())
        {
            if (player.Hand.IsIdiotsArray())
            {
                handType = "Mano del Idiota";
                return player;
            }
        }

        // Busca Pure Sabacc (23 exacto)
        foreach (Player player in state.GetActivePlayers())
        {
            if (player.Hand.IsPureSabacc())
            {
                handType = "Sabacc Puro";
                return player;
            }
        }

        return null;
    }

    /// <summary>Obtiene la mejor mano sin bomb out</summary>
    public Player GetBestHandForRound(GameState state)
    {
        List<Player> validPlayers = state.GetActivePlayers()
            .Where(p => !p.Hand.IsBombOut())
            .ToList();

        if (validPlayers.Count == 0)
            return null;

        Player best = validPlayers[0];
        int bestDistance = best.Hand.GetDistanceToTarget();
        int bestTotal = best.Hand.GetTotal();

        for (int i = 1; i < validPlayers.Count; i++)
        {
            int distance = validPlayers[i].Hand.GetDistanceToTarget();
            int total = validPlayers[i].Hand.GetTotal();
            
            if (distance < bestDistance)
            {
                best = validPlayers[i];
                bestDistance = distance;
                bestTotal = total;
            }
            // En caso de empate en distancia, el positivo gana al negativo
            // (23 gana a -23, pero -16 gana a 15 porque está más cerca)
            else if (distance == bestDistance)
            {
                // Si tienen la misma distancia, el que tenga valor absoluto mayor gana
                // porque está más cerca de 23 o -23
                if (Math.Abs(total) > Math.Abs(bestTotal))
                {
                    best = validPlayers[i];
                    bestDistance = distance;
                    bestTotal = total;
                }
                // Si mismo valor absoluto, positivo gana (23 > -23)
                else if (Math.Abs(total) == Math.Abs(bestTotal) && total > bestTotal)
                {
                    best = validPlayers[i];
                    bestDistance = distance;
                    bestTotal = total;
                }
            }
        }

        return best;
    }

    // ===== RONDAS =====

    /// <summary>Termina la ronda actual</summary>
    public void EndRound(GameState state)
    {
        state.IsRoundActive = false;
        state.CurrentPhase = GamePhase.Reveal;
    }

    /// <summary>Resetea los botes</summary>
    public void ResetPots(GameState state)
    {
        state.HandPot = 0;
        state.SabaccPot = 0;
    }

    /// <summary>Distribuye el bote de mano a un ganador</summary>
    public void AwardHandPot(GameState state, Player winner)
    {
        if (winner != null && state.HandPot > 0)
        {
            winner.AddCredits(state.HandPot);
            state.HandPot = 0;
        }
    }

    /// <summary>Distribuye el bote de Sabacc a un ganador</summary>
    public void AwardSabaccPot(GameState state, Player winner)
    {
        if (winner != null && state.SabaccPot > 0)
        {
            winner.AddCredits(state.SabaccPot);
            state.SabaccPot = 0;
        }
    }

    /// <summary>Distribuye ambos botes a un ganador</summary>
    public void AwardAllPots(GameState state, Player winner)
    {
        if (winner != null)
        {
            int totalPot = state.HandPot + state.SabaccPot;
            winner.AddCredits(totalPot);
            state.HandPot = 0;
            state.SabaccPot = 0;
        }
    }

    /// <summary>Comprueba si la ronda debería terminar</summary>
    public bool ShouldEndRound(GameState state)
    {
        // Terminar si solo queda 1 jugador activo
        return state.GetActivePlayerCount() <= 1;
    }
}
