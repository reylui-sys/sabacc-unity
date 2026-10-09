using System;
using System.Collections.Generic;

/// <summary>Pasos automáticos que da la partida sin que ningún jugador actúe.</summary>
public enum FlowAction : byte
{
    None,               // esperar a que un jugador actúe
    StartRound,         // barajar y repartir
    StartFirstBetting,
    BettingStep,        // tras una acción de apuesta: siguiente turno, siguiente fase o último en pie
    ShiftFirst,
    ShiftSecond,
    ShiftAfterCall,
    StartDrawing,
    StartSecondBetting,
    StartReveal,
    Settle,             // liquidar la ronda (penalizaciones y botes)
    AfterPlayerLeft,    // alguien abandonó: quizá le tocaba, o solo queda uno
    NextRound,
    RestartGame
}

/// <summary>
/// La máquina de estados del flujo de la partida.
///
/// Antes el orden de las fases estaba repartido por el controlador en cadenas de
/// RPC, Invoke y corrutinas con esperas calibradas a la animación del Master.
/// Ahora vive aquí, en dos funciones puras:
///
///   Next(estado, lote)  → qué paso automático toca después de ese lote de eventos
///   Plan(paso, estado)  → los eventos de ese paso (o ninguno si ya no procede)
///
/// Es una función de transición explícita (estado + evento → acción). Se eligió
/// frente a una clase por fase (patrón State clásico) porque las transiciones las
/// disparan los EVENTOS y no hay comportamiento propio de cada fase que encapsular:
/// toda la tabla se lee de un vistazo en Next.
///
/// Quién y cuándo ejecuta el paso es cosa del Master: en red, cuando todos los
/// jugadores han terminado de ver el lote (barrera de presentación).
/// </summary>
public static class GameFlow
{
    /// <summary>
    /// Qué paso automático toca tras aplicar este lote. Se mira el evento más
    /// reciente que decida algo (los demás, como TurnChanged, no deciden nada).
    /// </summary>
    public static FlowAction Next(GameState state, IReadOnlyList<GameEvent> batch)
    {
        for (int i = batch.Count - 1; i >= 0; i--)
        {
            switch (batch[i])
            {
                case GameOver _:
                    return FlowAction.None;
                case RoundEnded ended:
                    return ended.Outcome == RoundOutcome.DefinitiveWin ? FlowAction.RestartGame : FlowAction.NextRound;
                case GameRestarted _:
                    return FlowAction.StartRound;
                case RoundStarted _:
                    return FlowAction.StartFirstBetting;

                case PlayerCalled _:
                    return FlowAction.ShiftAfterCall;
                case PlayerChecked _:
                case BetPlaced _:
                case BetMatched _:
                case PlayerFolded _:
                    return FlowAction.BettingStep;

                case CardsShifted shifted:
                    return shifted.Kind == ShiftKind.First ? FlowAction.StartDrawing : FlowAction.StartReveal;

                case PhaseChanged phase:
                    switch (phase.Phase)
                    {
                        case GamePhase.FirstShift: return FlowAction.ShiftFirst;
                        case GamePhase.SecondShift: return FlowAction.ShiftSecond;
                        case GamePhase.Reveal: return FlowAction.Settle;
                        default: return FlowAction.None; // apuestas o robo: esperar al jugador
                    }

                case PlayerStood _:
                    return state.AllActivePlayersStood()
                        ? FlowAction.StartSecondBetting
                        : FlowAction.None;

                case PlayerLeft _:
                    return state.IsRoundActive ? FlowAction.AfterPlayerLeft : FlowAction.None;
            }
        }
        return FlowAction.None;
    }

    /// <summary>
    /// Eventos de un paso automático. Cada paso comprueba que sigue procediendo
    /// (guarda): si entre medias pasó otra cosa —p. ej. la ronda ya terminó porque
    /// alguien se fue— devuelve una lista vacía en vez de actuar dos veces.
    /// Solo StartRound/NextRound modifican algo: barajan el mazo de la autoridad.
    /// </summary>
    public static List<GameEvent> Plan(FlowAction action, GameState state, RulesConfig rules, Random rng, float shiftProbability)
    {
        bool active = state.IsRoundActive;
        GamePhase phase = state.CurrentPhase;

        switch (action)
        {
            case FlowAction.StartRound:
            case FlowAction.NextRound:
                if (active) break;
                GameEngine.PrepareDeck(state, rng);
                return GameEngine.PlanRoundStart(state, rules);

            case FlowAction.RestartGame:
                if (active) break;
                return One(new GameRestarted { StartingCredits = rules.StartingCredits });

            case FlowAction.StartFirstBetting:
                if (!active || phase != GamePhase.Dealing) break;
                return One(GameEngine.PlanPhaseStart(state, GamePhase.FirstBetting));

            case FlowAction.BettingStep:
                if (!active || !CommandValidator.IsBettingPhase(phase) || state.SomeoneCalled) break;
                // Solo se pasa el turno si quien lo tiene ya actuó o ya no juega. Si no,
                // es que otro paso ya lo pasó y este llega tarde: darlo saltaría a alguien.
                if (state.CurrentPlayer.State == PlayerState.Active && !state.CurrentPlayer.HasActedThisBettingRound) break;
                return GameEngine.PlanBettingStep(state);

            case FlowAction.ShiftFirst:
                if (!active || phase != GamePhase.FirstShift) break;
                return One(GameEngine.PlanShifting(state, shiftProbability, rng, ShiftKind.First));

            case FlowAction.ShiftSecond:
                if (!active || phase != GamePhase.SecondShift) break;
                return One(GameEngine.PlanShifting(state, shiftProbability, rng, ShiftKind.Second));

            case FlowAction.ShiftAfterCall:
                if (!active || !state.SomeoneCalled || !CommandValidator.IsBettingPhase(phase)) break;
                return One(GameEngine.PlanShifting(state, shiftProbability, rng, ShiftKind.AfterCall));

            case FlowAction.StartDrawing:
                if (!active || phase != GamePhase.FirstShift) break;
                return One(GameEngine.PlanPhaseStart(state, GamePhase.Drawing));

            case FlowAction.StartSecondBetting:
                if (!active || phase != GamePhase.Drawing) break;
                return One(GameEngine.PlanPhaseStart(state, GamePhase.SecondBetting));

            case FlowAction.StartReveal:
                if (!active || phase == GamePhase.Reveal) break;
                return One(GameEngine.PlanPhaseStart(state, GamePhase.Reveal));

            case FlowAction.Settle:
                if (!active || phase != GamePhase.Reveal) break;
                return GameEngine.SettleRound(state, rules);

            case FlowAction.AfterPlayerLeft:
                return PlanAfterPlayerLeft(state);
        }
        return new List<GameEvent>();
    }

    /// <summary>
    /// Alguien abandonó la sala (el reducer ya lo retiró). Si solo queda uno, gana
    /// el bote. Si era su turno, el turno pasa al siguiente o la fase termina.
    /// En cualquier otro caso no hay nada que hacer: el siguiente paso pendiente ya
    /// tiene en cuenta que ese jugador no está.
    /// </summary>
    private static List<GameEvent> PlanAfterPlayerLeft(GameState state)
    {
        if (!state.IsRoundActive)
            return new List<GameEvent>();

        if (state.GetActivePlayerCount() <= 1)
            return GameEngine.PlanLastPlayerStanding(state);

        // Solo hay que mover el turno si lo tenía quien se fue. Si lo tiene alguien
        // que se acaba de retirar, de eso ya se encarga su propio paso de apuestas.
        if (!state.CurrentPlayer.HasLeft)
            return new List<GameEvent>();

        GamePhase phase = state.CurrentPhase;
        if (CommandValidator.IsBettingPhase(phase) && !state.SomeoneCalled)
            return GameEngine.PlanBettingStep(state);

        if (phase == GamePhase.Drawing)
        {
            if (state.AllActivePlayersStood())
                return One(GameEngine.PlanPhaseStart(state, GamePhase.SecondBetting));
            return One(new TurnChanged { PlayerIndex = NextSeatStillToStand(state, state.CurrentPlayerIndex) });
        }

        return new List<GameEvent>();
    }

    private static int NextSeatStillToStand(GameState state, int from)
    {
        int n = state.Players.Count;
        for (int step = 1; step <= n; step++)
        {
            Player p = state.Players[(from + step) % n];
            if (p.State == PlayerState.Active && !p.HasStood)
                return (from + step) % n;
        }
        return GameEngine.NextActiveSeat(state, from);
    }

    private static List<GameEvent> One(GameEvent e) => new List<GameEvent> { e };
}
