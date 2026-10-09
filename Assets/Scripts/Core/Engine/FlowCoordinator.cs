using System;
using System.Collections.Generic;

/// <summary>
/// El ritmo de la partida en el Master: qué pasos automáticos quedan pendientes
/// y si ya se pueden dar.
///
/// Cada lote de eventos que envía el Master lleva un número. Cada equipo lo
/// confirma (Ack) cuando ha terminado de VERLO, animaciones incluidas. El Master
/// solo da el siguiente paso automático (GameFlow) cuando todos los que siguen en
/// la sala han confirmado el último lote: una barrera. Si alguien no confirma a
/// tiempo, el llamador puede forzar el paso (TimedOut).
///
/// No sabe nada de Unity ni de Photon (los tiempos y los jugadores se los pasa el
/// llamador), así que el protocolo completo se puede probar en una simulación.
/// </summary>
public sealed class FlowCoordinator
{
    private readonly Queue<FlowAction> _pending = new Queue<FlowAction>();
    private readonly HashSet<int> _acks = new HashSet<int>();
    private double _openedAt;

    /// <summary>Número del último lote enviado: el que hay que confirmar</summary>
    public int LastSeq { get; private set; }

    public int PendingCount => _pending.Count;

    /// <summary>El siguiente paso pendiente, sin sacarlo (None si no hay)</summary>
    public FlowAction PeekNext => _pending.Count > 0 ? _pending.Peek() : FlowAction.None;

    /// <summary>
    /// Arranque de la partida. La inicialización cuenta como el lote 0: la primera
    /// ronda se reparte cuando todos han creado su vista de la partida.
    /// </summary>
    public void Start(double now)
    {
        _pending.Clear();
        _pending.Enqueue(FlowAction.StartRound);
        Open(0, now);
    }

    /// <summary>
    /// El Master va a enviar este lote (ya aplicado a su autoridad). Apunta el paso
    /// automático que toca después y abre la barrera del lote. Devuelve su número.
    /// </summary>
    public int OnBroadcast(GameState authority, IReadOnlyList<GameEvent> events, double now)
    {
        FlowAction next = GameFlow.Next(authority, events);
        if (next != FlowAction.None)
            _pending.Enqueue(next);

        Open(LastSeq + 1, now);
        return LastSeq;
    }

    /// <summary>Un jugador ha terminado de ver el lote 'seq'. Las confirmaciones viejas no cuentan.</summary>
    public void Ack(int actor, int seq)
    {
        if (seq == LastSeq)
            _acks.Add(actor);
    }

    /// <summary>¿Han visto el último lote todos los que siguen en la sala? Quien se fue no bloquea.</summary>
    public bool EveryonePresented(IEnumerable<int> playersInRoom)
    {
        foreach (int actor in playersInRoom)
        {
            if (!_acks.Contains(actor))
                return false;
        }
        return true;
    }

    /// <summary>Quién falta por confirmar el último lote (para el registro)</summary>
    public List<int> Missing(IEnumerable<int> playersInRoom)
    {
        var missing = new List<int>();
        foreach (int actor in playersInRoom)
        {
            if (!_acks.Contains(actor))
                missing.Add(actor);
        }
        return missing;
    }

    /// <summary>¿Hay un paso esperando y la barrera lleva abierta más de 'timeout'?</summary>
    public bool TimedOut(double now, double timeout)
    {
        return _pending.Count > 0 && now - _openedAt >= timeout;
    }

    /// <summary>Vuelve a contar el plazo (tras forzar un paso que no produjo nada)</summary>
    public void RestartTimer(double now)
    {
        _openedAt = now;
    }

    /// <summary>
    /// Saca pasos pendientes hasta encontrar uno que aún proceda y devuelve sus
    /// eventos, o una lista vacía si no queda ninguno. Un paso que ya no procede
    /// (p. ej. la ronda terminó entre medias) se descarta sin esperar otra barrera.
    /// Solo StartRound/NextRound tocan la autoridad: barajan su mazo.
    /// </summary>
    public List<GameEvent> TakeNextStep(GameState authority, RulesConfig rules, Random rng, float shiftProbability)
    {
        while (_pending.Count > 0)
        {
            List<GameEvent> events = GameFlow.Plan(_pending.Dequeue(), authority, rules, rng, shiftProbability);
            if (events.Count > 0)
                return events;
        }
        return new List<GameEvent>();
    }

    private void Open(int seq, double now)
    {
        LastSeq = seq;
        _acks.Clear();
        _openedAt = now;
    }
}
