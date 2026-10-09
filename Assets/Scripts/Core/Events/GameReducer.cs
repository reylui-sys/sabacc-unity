using System;
using System.Collections.Generic;

/// <summary>
/// EL ÚNICO SITIO QUE MODIFICA EL ESTADO DE LA PARTIDA.
///
/// Master y clientes reciben la misma lista de eventos y la aplican con esta
/// misma función, así que su estado público no puede divergir. Antes cada
/// RPC cambiaba el estado a su manera y el Master a veces lo cambiaba por
/// otro camino (bote duplicado, penalizaciones que solo veía el Master...).
///
/// Reglas para quien toque este archivo:
///  - Apply NO valida ni decide: eso ya lo hizo el Master (GameEngine).
///  - Apply NO usa aleatoriedad ni lee nada fuera del estado y del evento,
///    para que el resultado sea idéntico en todos los equipos.
///  - La única diferencia permitida es el mazo: solo la autoridad lo conoce
///    (GameState.DeckIsKnown) y los clientes crean las cartas por su ID.
/// </summary>
public static class GameReducer
{
    public static void ApplyAll(GameState state, IEnumerable<GameEvent> events)
    {
        foreach (GameEvent e in events)
            Apply(state, e);
    }

    public static void Apply(GameState state, GameEvent e)
    {
        switch (e)
        {
            case RoundStarted x: ApplyRoundStarted(state, x); break;
            case PhaseChanged x: ApplyPhaseChanged(state, x); break;
            case TurnChanged x: state.CurrentPlayerIndex = x.PlayerIndex; break;

            case PlayerChecked x:
                state.Players[x.PlayerIndex].HasActedThisBettingRound = true;
                break;
            case BetPlaced x: ApplyBetPlaced(state, x); break;
            case BetMatched x: ApplyBetMatched(state, x); break;
            case PlayerCalled x: state.CallerIndex = x.PlayerIndex; break;
            case PlayerFolded x:
                PayToSabaccPot(state, x.PlayerIndex, x.Penalty);
                state.Players[x.PlayerIndex].Fold();
                break;

            case CardDrawn x:
                state.Players[x.PlayerIndex].Hand.AddCard(TakeCard(state, x.CardId));
                break;
            case PlayerStood x:
                state.PlayersStood++;
                state.CurrentPlayerIndex = x.NextPlayerIndex;
                break;
            case CardDiscarded x: ApplyCardDiscarded(state, x); break;
            case CardProtectionChanged x:
                state.Players[x.PlayerIndex].Hand.GetCards()[x.CardIndex].SetProtected(x.IsProtected);
                break;
            case CardsShifted x: ApplyCardsShifted(state, x); break;

            case PenaltyPaid x: PayToSabaccPot(state, x.PlayerIndex, x.Amount); break;
            case PlayerBombedOut x: state.Players[x.PlayerIndex].MarkAsBombedOut(); break;
            case PotAwarded x:
                state.Players[x.PlayerIndex].AddCredits(x.FromHandPot + x.FromSabaccPot);
                state.HandPot -= x.FromHandPot;
                state.SabaccPot -= x.FromSabaccPot;
                break;
            case RoundEnded _:
                // No cambia la fase: si la ronda acaba porque todos se retiran, las
                // manos de los demás siguen ocultas (no se pasa por la revelación)
                state.IsRoundActive = false;
                break;

            case PlayerLeft x: state.Players[x.PlayerIndex].Fold(); break;
            case GameOver _: state.IsRoundActive = false; break;
            case GameRestarted x: ApplyGameRestarted(state, x); break;

            default:
                throw new ArgumentException($"El reducer no sabe aplicar {e.Type}");
        }
    }

    // ===== RONDA Y FASES =====

    private static void ApplyRoundStarted(GameState state, RoundStarted e)
    {
        state.CurrentRound = e.Round;
        state.DealerIndex = e.DealerIndex;
        state.CurrentPlayerIndex = e.CurrentPlayerIndex;
        state.HandPot = e.HandPot;
        state.SabaccPot = e.SabaccPot;
        state.CurrentPhase = GamePhase.Dealing;
        state.IsRoundActive = true;
        state.CurrentHighestBet = 0;
        state.CallerIndex = -1;
        state.PlayersStood = 0;
        state.DiscardPile = new DiscardPile();

        for (int i = 0; i < state.Players.Count; i++)
        {
            Player player = state.Players[i];
            player.ResetForRound();
            player.Credits = e.Credits[i];
            player.State = e.States[i];

            foreach (string cardId in e.Hands[i])
                player.Hand.AddCard(TakeCard(state, cardId));
        }
    }

    private static void ApplyPhaseChanged(GameState state, PhaseChanged e)
    {
        state.CurrentPhase = e.Phase;

        bool isBetting = CommandValidator.IsBettingPhase(e.Phase);
        bool isDrawing = e.Phase == GamePhase.Drawing;

        if (isBetting || isDrawing)
        {
            foreach (Player player in state.Players)
            {
                player.ResetBettingRound();
                if (isDrawing)
                    player.HasDiscardedThisTurn = false;
            }
            state.CurrentHighestBet = 0;
        }

        if (isBetting)
            state.CallerIndex = -1;
        if (isDrawing)
            state.PlayersStood = 0;

        if (e.FirstPlayerIndex >= 0)
            state.CurrentPlayerIndex = e.FirstPlayerIndex;
    }

    // ===== APUESTAS =====

    private static void ApplyBetPlaced(GameState state, BetPlaced e)
    {
        Player player = state.Players[e.PlayerIndex];
        int total = state.AmountToCall(player) + e.RaiseAmount;

        player.DeductCredits(total);
        player.CurrentBet = state.CurrentHighestBet + e.RaiseAmount;
        player.TotalBetThisRound += total;
        player.HasActedThisBettingRound = true;
        state.HandPot += total;
        state.CurrentHighestBet = player.CurrentBet;

        // Tras una subida, los demás tienen que volver a actuar
        foreach (Player other in state.Players)
        {
            if (other != player && other.State == PlayerState.Active)
                other.HasActedThisBettingRound = false;
        }
    }

    private static void ApplyBetMatched(GameState state, BetMatched e)
    {
        Player player = state.Players[e.PlayerIndex];
        player.DeductCredits(e.Amount);
        player.CurrentBet = state.CurrentHighestBet;
        player.TotalBetThisRound += e.Amount;
        player.HasActedThisBettingRound = true;
        state.HandPot += e.Amount;
    }

    // ===== CARTAS =====

    private static void ApplyCardDiscarded(GameState state, CardDiscarded e)
    {
        Player player = state.Players[e.PlayerIndex];
        SabaccCard card = player.Hand.RemoveCardAt(e.CardIndex);
        if (card.GetCardId() != e.CardId)
            CoreLog.Warning($"[GameReducer] Descarte desincronizado: se esperaba {e.CardId} y había {card.GetCardId()}");

        state.DiscardPile.Discard(card);
        player.HasDiscardedThisTurn = true;
    }

    private static void ApplyCardsShifted(GameState state, CardsShifted e)
    {
        foreach (CardShift shift in e.Shifts)
        {
            Hand hand = state.Players[shift.PlayerIndex].Hand;
            SabaccCard oldCard = hand.GetCards()[shift.CardIndex];
            SabaccCard newCard = TakeCard(state, shift.NewCardId);

            hand.ReplaceCardAt(shift.CardIndex, newCard);

            // La carta que sale de la mano vuelve al fondo del mazo (solo quien conoce el mazo)
            if (state.DeckIsKnown)
                state.MainDeck.AddCardToBottom(oldCard);
        }
    }

    // ===== PARTIDA =====

    private static void ApplyGameRestarted(GameState state, GameRestarted e)
    {
        foreach (Player player in state.Players)
        {
            player.Credits = e.StartingCredits;
            player.ResetForRound();
        }
        state.HandPot = 0;
        state.SabaccPot = 0;
        state.CurrentRound = 0;
        state.DealerIndex = 0;
        state.CurrentPlayerIndex = 0;
        state.CurrentHighestBet = 0;
        state.CallerIndex = -1;
        state.PlayersStood = 0;
        state.IsRoundActive = false;
    }

    // ===== AUXILIARES =====

    private static void PayToSabaccPot(GameState state, int playerIndex, int amount)
    {
        if (amount <= 0) return;
        state.Players[playerIndex].DeductCredits(amount);
        state.SabaccPot += amount;
    }

    /// <summary>
    /// La autoridad saca la carta real del mazo; los clientes, que no conocen
    /// el mazo, crean una carta equivalente a partir del ID.
    /// </summary>
    private static SabaccCard TakeCard(GameState state, string cardId)
    {
        if (state.DeckIsKnown)
        {
            SabaccCard fromDeck = state.MainDeck.TakeById(cardId);
            if (fromDeck != null)
                return fromDeck;
            CoreLog.Error($"[GameReducer] La carta {cardId} no está en el mazo de la autoridad");
        }
        return SabaccCardDefinitions.GetCardById(cardId);
    }
}
