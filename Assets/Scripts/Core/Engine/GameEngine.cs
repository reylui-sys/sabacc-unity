using System;
using System.Collections.Generic;

/// <summary>
/// El motor de reglas de la autoridad (el Master). DECIDE qué pasa y lo
/// devuelve como eventos, pero NO modifica el estado: eso lo hace
/// GameReducer cuando el evento llega a todos, Master incluido.
///
///     comando ──► GameEngine.Handle ──► eventos ──► red ──► GameReducer.Apply (en todos)
///
/// La única excepción es PrepareDeck: el mazo es información oculta que solo
/// existe en la autoridad, así que se baraja aquí y nunca viaja por la red.
/// </summary>
public static class GameEngine
{
    /// <summary>Resultado de procesar un comando: o un error, o los eventos que provoca.</summary>
    public sealed class Result
    {
        public string Error { get; private set; }
        public List<GameEvent> Events { get; private set; }
        public bool IsValid => Error == null;

        public static Result Fail(string error) => new Result { Error = error, Events = new List<GameEvent>() };
        public static Result Ok(params GameEvent[] events) => new Result { Events = new List<GameEvent>(events) };
    }

    // ===== COMANDOS =====

    public static Result Handle(GameState state, RulesConfig rules, GameCommand command)
    {
        string error = CommandValidator.Validate(state, rules, command);
        if (error != null)
            return Result.Fail(error);

        int seat = state.IndexOf(command.Player);
        Player player = state.Players[seat];

        switch (command.Type)
        {
            case CommandType.Draw:
                string topCardId = state.DeckIsKnown ? state.MainDeck.PeekIdFromTop(0) : null;
                if (topCardId == null)
                    return Result.Fail("No quedan cartas en el mazo.");
                return Result.Ok(new CardDrawn { PlayerIndex = seat, CardId = topCardId });

            case CommandType.Stand:
                return Result.Ok(new PlayerStood { PlayerIndex = seat, NextPlayerIndex = NextActiveSeat(state, seat) });

            case CommandType.Discard:
                return Result.Ok(new CardDiscarded
                {
                    PlayerIndex = seat, CardIndex = command.Arg,
                    CardId = player.Hand.GetCards()[command.Arg].GetCardId()
                });

            case CommandType.Protect:
            case CommandType.Unprotect:
                return Result.Ok(new CardProtectionChanged
                {
                    PlayerIndex = seat, CardIndex = command.Arg,
                    IsProtected = command.Type == CommandType.Protect,
                    CardId = player.Hand.GetCards()[command.Arg].GetCardId()
                });

            case CommandType.Check:
                return Result.Ok(new PlayerChecked { PlayerIndex = seat });
            case CommandType.Bet:
                return Result.Ok(new BetPlaced { PlayerIndex = seat, RaiseAmount = command.Arg });
            case CommandType.Match:
                return Result.Ok(new BetMatched { PlayerIndex = seat, Amount = state.AmountToCall(player) });
            case CommandType.Call:
                return Result.Ok(new PlayerCalled { PlayerIndex = seat });
            case CommandType.Fold:
                return Result.Ok(new PlayerFolded { PlayerIndex = seat, Penalty = CommandValidator.FoldPenalty(state, rules, player) });
        }
        return Result.Fail("Acción desconocida.");
    }

    // ===== INICIO DE RONDA =====

    /// <summary>Solo la autoridad: mazo nuevo y barajado. Nunca se envía por la red.</summary>
    public static void PrepareDeck(GameState state, Random rng)
    {
        state.MainDeck = new Deck(SabaccCardDefinitions.CreateFullDeck());
        state.MainDeck.Shuffle(rng);
        state.DeckIsKnown = true;
    }

    /// <summary>
    /// Cobra las apuestas iniciales y reparte 2 cartas a cada jugador que pueda pagar.
    /// Si solo puede pagar uno (o ninguno), la partida termina: GameOver.
    /// Requiere un mazo preparado (PrepareDeck).
    /// </summary>
    public static List<GameEvent> PlanRoundStart(GameState state, RulesConfig rules)
    {
        int n = state.Players.Count;
        int cost = rules.InitialBet * 2; // una apuesta a cada bote
        var credits = new int[n];
        var states = new PlayerState[n];
        int handPot = state.HandPot;     // lo que quedó de la ronda anterior se acumula
        int sabaccPot = state.SabaccPot;
        int payers = 0, lastPayer = -1;

        for (int i = 0; i < n; i++)
        {
            Player player = state.Players[i];
            if (player.Credits >= cost)
            {
                credits[i] = player.Credits - cost;
                states[i] = PlayerState.Active;
                handPot += rules.InitialBet;
                sabaccPot += rules.InitialBet;
                payers++;
                lastPayer = i;
            }
            else
            {
                credits[i] = player.Credits;
                states[i] = PlayerState.Folded;
            }
        }

        // Partida terminada: el único que puede pagar se lleva lo que haya en los botes
        if (payers <= 1)
        {
            var events = new List<GameEvent>();
            if (payers == 1)
            {
                int amount = state.HandPot + state.SabaccPot;
                if (amount > 0)
                    events.Add(new PotAwarded { PlayerIndex = lastPayer, FromHandPot = state.HandPot, FromSabaccPot = state.SabaccPot });
                events.Add(new GameOver { WinnerIndex = lastPayer, AmountWon = amount });
            }
            else
            {
                events.Add(new GameOver { WinnerIndex = -1, AmountWon = 0 });
            }
            return events;
        }

        if (!state.DeckIsKnown)
            throw new InvalidOperationException("PlanRoundStart necesita el mazo de la autoridad (PrepareDeck)");

        // Reparto: dos cartas seguidas desde la cima a cada jugador activo, en orden de asiento
        var hands = new string[n][];
        int fromTop = 0;
        for (int i = 0; i < n; i++)
        {
            if (states[i] != PlayerState.Active)
            {
                hands[i] = new string[0];
                continue;
            }
            hands[i] = new[] { state.MainDeck.PeekIdFromTop(fromTop), state.MainDeck.PeekIdFromTop(fromTop + 1) };
            fromTop += 2;
        }

        bool firstRound = state.CurrentRound == 0;
        int dealer = firstRound ? 0 : (state.DealerIndex + 1) % n;

        return new List<GameEvent>
        {
            new RoundStarted
            {
                Round = state.CurrentRound + 1,
                DealerIndex = dealer,
                CurrentPlayerIndex = NextActiveSeat(states, dealer),
                HandPot = handPot,
                SabaccPot = sabaccPot,
                Credits = credits,
                States = states,
                Hands = hands
            }
        };
    }

    // ===== FASES Y TURNOS =====

    /// <summary>Empieza una fase. En apuestas y robo empieza el primer activo desde el asiento 0.</summary>
    public static PhaseChanged PlanPhaseStart(GameState state, GamePhase phase)
    {
        bool hasTurns = phase == GamePhase.Drawing || CommandValidator.IsBettingPhase(phase);
        return new PhaseChanged { Phase = phase, FirstPlayerIndex = hasTurns ? FirstActiveSeat(state) : -1 };
    }

    /// <summary>
    /// Qué pasa después de una acción de apuesta: o le toca al siguiente, o la
    /// ronda de apuestas termina y empieza la siguiente fase, o solo queda uno.
    /// </summary>
    public static List<GameEvent> PlanBettingStep(GameState state)
    {
        if (state.GetActivePlayerCount() <= 1)
            return PlanLastPlayerStanding(state);

        bool roundComplete = true;
        foreach (Player player in state.Players)
        {
            if (player.State != PlayerState.Active) continue;
            if (!player.HasActedThisBettingRound || player.CurrentBet < state.CurrentHighestBet)
            {
                roundComplete = false;
                break;
            }
        }

        if (!roundComplete)
            return new List<GameEvent> { new TurnChanged { PlayerIndex = NextActiveSeat(state, state.CurrentPlayerIndex) } };

        switch (state.CurrentPhase)
        {
            case GamePhase.FirstBetting:
                return new List<GameEvent> { PlanPhaseStart(state, GamePhase.Calling) };
            case GamePhase.Calling:
                return new List<GameEvent> { PlanPhaseStart(state, GamePhase.FirstShift) };
            case GamePhase.SecondBetting:
                return new List<GameEvent> { PlanPhaseStart(state, GamePhase.SecondShift) };
            default:
                return new List<GameEvent>();
        }
    }

    /// <summary>Todos los demás se retiraron: el último activo cobra el bote de mano.</summary>
    public static List<GameEvent> PlanLastPlayerStanding(GameState state)
    {
        int winner = FirstActiveSeat(state, requireActive: true);
        if (winner < 0)
            return new List<GameEvent>();

        return new List<GameEvent>
        {
            new PotAwarded { PlayerIndex = winner, FromHandPot = state.HandPot, FromSabaccPot = 0 },
            new RoundEnded { Outcome = RoundOutcome.LastPlayerStanding, WinnerIndex = winner, AmountWon = state.HandPot }
        };
    }

    // ===== SHIFTING =====

    /// <summary>
    /// Decide qué cartas cambian. Cada carta no protegida de cada jugador activo
    /// cambia con probabilidad 'probability' por una carta al azar del mazo; la
    /// carta vieja vuelve al mazo y puede salir en un cambio posterior.
    /// No modifica nada: simula el mazo sobre una copia.
    /// </summary>
    public static CardsShifted PlanShifting(GameState state, float probability, Random rng, ShiftKind kind)
    {
        var result = new CardsShifted { Kind = kind };
        if (!state.DeckIsKnown)
            return result;

        var pool = new List<string>();
        foreach (SabaccCard card in state.MainDeck.GetCards())
            pool.Add(card.GetCardId());

        for (int p = 0; p < state.Players.Count; p++)
        {
            Player player = state.Players[p];
            if (player.State != PlayerState.Active) continue;

            List<SabaccCard> cards = player.Hand.GetCards();
            for (int c = 0; c < cards.Count; c++)
            {
                if (cards[c].IsProtected()) continue;
                if (rng.NextDouble() >= probability) continue;
                if (pool.Count == 0) continue;

                int pick = rng.Next(pool.Count);
                string newId = pool[pick];
                string oldId = cards[c].GetCardId();
                pool.RemoveAt(pick);
                pool.Add(oldId);

                result.Shifts.Add(new CardShift { PlayerIndex = p, CardIndex = c, OldCardId = oldId, NewCardId = newId });
            }
        }
        return result;
    }

    // ===== LIQUIDACIÓN DE LA RONDA =====

    /// <summary>
    /// Revelación: penalizaciones por explotar, penalización de CALL si quien lo
    /// hizo no gana, y reparto de botes. Calcula los importes en orden (las
    /// penalizaciones engordan el bote de Sabacc antes del reparto).
    /// </summary>
    public static List<GameEvent> SettleRound(GameState state, RulesConfig rules)
    {
        var events = new List<GameEvent>();
        int n = state.Players.Count;
        var credits = new int[n];
        var handValues = new int[n];
        var bombed = new bool[n];
        int sabaccPot = state.SabaccPot;

        // 1. Bomb outs
        for (int i = 0; i < n; i++)
        {
            Player player = state.Players[i];
            credits[i] = player.Credits;
            handValues[i] = player.Hand.GetTotal();
            bombed[i] = player.Hand.IsBombOut();

            if (bombed[i] && player.State == PlayerState.Active)
            {
                int penalty = Math.Min(rules.BombedOutPenalty, Math.Max(0, credits[i]));
                if (penalty > 0)
                {
                    events.Add(new PenaltyPaid { PlayerIndex = i, Amount = penalty, Reason = PenaltyReason.BombedOut });
                    credits[i] -= penalty;
                    sabaccPot += penalty;
                }
                events.Add(new PlayerBombedOut { PlayerIndex = i });
            }
        }

        // 2. Ganador: definitivo (Idiota / Sabacc Puro) o, si no hay, la mejor mano.
        //    Las manos que explotan nunca son definitivas ni cuentan para la mejor mano.
        var logic = new GameLogic();
        Player definitive = logic.GetDefinitiveWinner(state, out string handType);
        Player bestHand = definitive == null ? logic.GetBestHandForRound(state) : null;
        Player roundWinner = definitive ?? bestHand;
        int winnerIndex = roundWinner != null ? state.Players.IndexOf(roundWinner) : -1;

        // 3. CALL fallido: paga quien forzó la revelación y no se lleva la ronda.
        //    (Antes se penalizaba aunque ganase con la mejor mano: solo se miraba el ganador definitivo.)
        if (state.SomeoneCalled)
        {
            int caller = state.CallerIndex;
            if (caller != winnerIndex && state.Players[caller].State != PlayerState.Folded)
            {
                int penalty = Math.Min(rules.CallPenalty, Math.Max(0, credits[caller]));
                if (penalty > 0)
                {
                    events.Add(new PenaltyPaid { PlayerIndex = caller, Amount = penalty, Reason = PenaltyReason.FailedCall });
                    credits[caller] -= penalty;
                    sabaccPot += penalty;
                }
            }
        }

        // 4. Reparto
        var ended = new RoundEnded { HandValues = handValues, BombedOut = bombed, WinnerIndex = winnerIndex };

        if (definitive != null)
        {
            events.Add(new PotAwarded { PlayerIndex = winnerIndex, FromHandPot = state.HandPot, FromSabaccPot = sabaccPot });
            ended.Outcome = RoundOutcome.DefinitiveWin;
            ended.HandType = handType;
            ended.AmountWon = state.HandPot + sabaccPot;
            ended.WinnerHandValue = handValues[winnerIndex];
        }
        else if (bestHand != null)
        {
            events.Add(new PotAwarded { PlayerIndex = winnerIndex, FromHandPot = state.HandPot, FromSabaccPot = 0 });
            ended.Outcome = RoundOutcome.BestHand;
            ended.AmountWon = state.HandPot;
            ended.WinnerHandValue = handValues[winnerIndex];
        }
        else
        {
            ended.Outcome = RoundOutcome.AllBombedOut;
        }

        events.Add(ended);
        return events;
    }

    // ===== AUXILIARES =====

    /// <summary>Siguiente asiento activo después de 'from' (dando la vuelta). Si no hay otro, 'from'.</summary>
    public static int NextActiveSeat(GameState state, int from)
    {
        int n = state.Players.Count;
        for (int step = 1; step <= n; step++)
        {
            int i = (from + step) % n;
            if (state.Players[i].State == PlayerState.Active)
                return i;
        }
        return from;
    }

    private static int NextActiveSeat(PlayerState[] states, int from)
    {
        int n = states.Length;
        for (int step = 1; step <= n; step++)
        {
            int i = (from + step) % n;
            if (states[i] == PlayerState.Active)
                return i;
        }
        return from;
    }

    /// <summary>Primer asiento activo desde el 0. Si no hay ninguno: 0, o -1 si requireActive.</summary>
    public static int FirstActiveSeat(GameState state, bool requireActive = false)
    {
        for (int i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].State == PlayerState.Active)
                return i;
        }
        return requireActive ? -1 : 0;
    }
}
