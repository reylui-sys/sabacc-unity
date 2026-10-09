/// <summary>
/// Decide si un comando es legal en el estado actual. Es una función pura:
/// no modifica nada y no sabe nada de Unity ni de la red.
///
/// La usan dos lados con el MISMO código:
///  - el cliente, antes de enviar, para dar feedback inmediato al jugador;
///  - el Master, al recibir, que es quien tiene la última palabra.
/// Así una regla vive en un único sitio y no puede haber dos versiones
/// distintas de "¿puedo descartar?".
///
/// Devuelve null si el comando es válido, o el motivo (para mostrarlo) si no.
/// </summary>
public static class CommandValidator
{
    public static string Validate(GameState state, RulesConfig rules, GameCommand command)
    {
        if (state == null)
            return "La partida aún no ha empezado.";

        int seat = state.IndexOf(command.Player);
        if (seat < 0)
            return "No participas en esta partida.";

        Player player = state.Players[seat];
        if (player.State != PlayerState.Active)
            return "Ya no estás jugando esta ronda.";

        if (state.CurrentPlayerIndex != seat)
            return "No es tu turno.";

        switch (command.Type)
        {
            case CommandType.Draw:
            case CommandType.Stand:
            case CommandType.Discard:
                return ValidateDrawingAction(state, rules, player, command);

            case CommandType.Protect:
            case CommandType.Unprotect:
                return ValidateInterferenceAction(state, rules, player, command);

            case CommandType.Check:
            case CommandType.Bet:
            case CommandType.Match:
            case CommandType.Call:
            case CommandType.Fold:
                return ValidateBettingAction(state, player, command);

            default:
                return "Acción desconocida.";
        }
    }

    /// <summary>Penalización que paga un jugador al retirarse en la fase actual</summary>
    public static int FoldPenalty(GameState state, RulesConfig rules, Player player)
    {
        if (state.CurrentPhase != GamePhase.FirstBetting)
            return 0;
        return System.Math.Min(rules.FoldPenaltyFirstBetting, System.Math.Max(0, player.Credits));
    }

    public static bool IsBettingPhase(GamePhase phase)
    {
        return phase == GamePhase.FirstBetting
            || phase == GamePhase.Calling
            || phase == GamePhase.SecondBetting;
    }

    // ===== ROBO / DESCARTE =====

    private static string ValidateDrawingAction(GameState state, RulesConfig rules, Player player, GameCommand command)
    {
        if (state.CurrentPhase != GamePhase.Drawing)
            return "Solo puedes hacer eso en la fase de robo.";

        Hand hand = player.Hand;

        switch (command.Type)
        {
            case CommandType.Draw:
                if (player.HasDiscardedThisTurn)
                    return "Ya has descartado: ahora solo puedes plantarte.";
                if (hand.GetCount() >= rules.MaxCardsInHand)
                    return $"Límite de cartas alcanzado (máximo {rules.MaxCardsInHand}).";
                return null;

            case CommandType.Stand:
                return null;

            case CommandType.Discard:
                if (!IsValidCardIndex(hand, command.Arg))
                    return "Selecciona una carta primero (teclas 1, 2, 3...).";
                if (player.HasDiscardedThisTurn)
                    return "Ya has descartado una carta. Solo puedes descartar UNA.";
                if (hand.GetCards()[command.Arg].IsProtected())
                    return "No puedes descartar una carta protegida. Primero retírala del campo.";
                if (hand.GetCount() < rules.MinCardsToDiscard)
                    return $"Necesitas al menos {rules.MinCardsToDiscard} cartas para descartar.";
                return null;
        }
        return "Acción desconocida.";
    }

    // ===== CAMPO DE INTERFERENCIA =====

    private static string ValidateInterferenceAction(GameState state, RulesConfig rules, Player player, GameCommand command)
    {
        if (state.CurrentPhase != GamePhase.Drawing && !IsBettingPhase(state.CurrentPhase))
            return "Ahora no puedes mover cartas al campo de interferencia.";

        Hand hand = player.Hand;
        if (!IsValidCardIndex(hand, command.Arg))
            return "Selecciona una carta primero (teclas 1, 2, 3...).";

        SabaccCard card = hand.GetCards()[command.Arg];

        if (command.Type == CommandType.Unprotect)
            return card.IsProtected() ? null : "Esta carta no está en el campo de interferencia.";

        if (card.IsProtected())
            return "Esta carta ya está en el campo de interferencia.";

        int protectedCount = CountProtected(hand);
        if (protectedCount >= rules.MaxProtectedCards)
            return $"Ya tienes {rules.MaxProtectedCards} cartas en el campo de interferencia.";
        if (hand.GetCount() - protectedCount <= rules.MinUnprotectedCards)
            return $"Debes tener al menos {rules.MinUnprotectedCards} cartas sin proteger en la mano.";
        return null;
    }

    // ===== APUESTAS =====

    private static string ValidateBettingAction(GameState state, Player player, GameCommand command)
    {
        if (!IsBettingPhase(state.CurrentPhase))
            return "No es momento de apostar.";

        // Si ya actuó en esta ronda y nadie ha subido después, no puede volver a actuar.
        // Esto es lo que frena el doble clic: entre que el Master acepta tu acción y
        // pasa el turno, sigue siendo "tu turno", pero ya has actuado.
        if (player.HasActedThisBettingRound)
            return "Ya has actuado en esta ronda de apuestas.";

        if (state.SomeoneCalled)
            return "Alguien ha hecho CALL: se va a revelar.";

        int amountToCall = state.AmountToCall(player);

        switch (command.Type)
        {
            case CommandType.Check:
                return amountToCall > 0
                    ? "No puedes pasar, debes igualar o retirarte."
                    : null;

            case CommandType.Bet:
                if (state.CurrentPhase == GamePhase.Calling)
                    return "No puedes subir en la fase de Calling. Usa Pasar, CALL o Retirarse.";
                if (command.Arg <= 0)
                    return "Cantidad inválida.";
                int totalNeeded = amountToCall + command.Arg;
                return player.CanAfford(totalNeeded)
                    ? null
                    : $"No tienes suficientes créditos. Necesitas {totalNeeded}, tienes {player.Credits}.";

            case CommandType.Match:
                if (amountToCall <= 0)
                    return "No hay nada que igualar.";
                return player.CanAfford(amountToCall)
                    ? null
                    : "No tienes suficientes créditos para igualar.";

            case CommandType.Call:
                if (state.CurrentPhase != GamePhase.Calling && state.CurrentPhase != GamePhase.SecondBetting)
                    return "Solo puedes hacer CALL en Calling o en la segunda ronda de apuestas.";
                return amountToCall > 0
                    ? "Antes de hacer CALL tienes que igualar la apuesta."
                    : null;

            case CommandType.Fold:
                return null;
        }
        return "Acción desconocida.";
    }

    // ===== AUXILIARES =====

    private static bool IsValidCardIndex(Hand hand, int index)
    {
        return index >= 0 && index < hand.GetCount();
    }

    private static int CountProtected(Hand hand)
    {
        int count = 0;
        foreach (SabaccCard card in hand.GetCards())
        {
            if (card.IsProtected())
                count++;
        }
        return count;
    }
}
