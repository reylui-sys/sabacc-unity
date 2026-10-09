using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Sabacc.Core.Tests
{
    /// <summary>
    /// El protocolo de red completo, simulado en el tiempo: lotes numerados, colas de
    /// presentación en cada equipo, confirmaciones y la barrera del Master.
    ///
    /// Cada equipo tarda un tiempo aleatorio en "ver" cada lote (las animaciones) y
    /// algunos se atascan de vez en cuando más que el plazo del Master. Los jugadores
    /// actúan cuando SU vista dice que es su turno, como en el juego. Se comprueba:
    ///  - que cada equipo, al terminar de ver el lote N, tiene exactamente el estado
    ///    público que tenía el Master al enviarlo (aunque vaya con retraso);
    ///  - que el Master nunca da un paso automático sin que todos hayan visto el último
    ///    lote, salvo cuando vence el plazo;
    ///  - que ninguna partida se queda parada.
    /// </summary>
    [TestFixture]
    public class NetworkProtocolTests
    {
        [Test]
        public void ConAnimacionesDeDuracionAleatoria_TodosVenLaMismaPartidaYNadieSeAdelanta()
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                var sim = new NetworkSimulator(seed, playerCount: 2 + seed % 3, stallChance: 0, leaveChance: 0);
                sim.Run(maxRounds: 5);
                Assert.AreEqual(0, sim.ForcedSteps, "Sin equipos atascados nunca debería vencer el plazo");
            }
        }

        [Test]
        public void UnEquipoQueSeAtasca_NoParaLaPartida_YLuegoSePoneAlDia()
        {
            int forced = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var sim = new NetworkSimulator(seed, playerCount: 3, stallChance: 0.04, leaveChance: 0);
                sim.Run(maxRounds: 4);
                forced += sim.ForcedSteps;
            }
            Assert.Greater(forced, 0, "La simulación nunca atascó a nadie lo bastante como para vencer el plazo");
        }

        [Test]
        public void QuienAbandona_NoBloqueaLaBarrera()
        {
            int left = 0;
            for (int seed = 1; seed <= 80; seed++)
            {
                var sim = new NetworkSimulator(seed, playerCount: 4, stallChance: 0, leaveChance: 0.002);
                sim.Run(maxRounds: 5);
                left += sim.PlayersWhoLeft;
                Assert.AreEqual(0, sim.ForcedSteps, "Nadie se atasca: quien se fue no debe hacer esperar al plazo");
            }
            Assert.Greater(left, 0, "La simulación nunca hizo abandonar a nadie");
        }

        // ===================================================================

        private sealed class NetworkSimulator
        {
            private const double Tick = 0.25;               // segundos simulados por paso
            private const double PresentationTimeout = 30;  // como presentationTimeout en el Master
            private const double MaxSimulatedTime = 4 * 3600;

            private sealed class Device
            {
                public int Actor;
                public int Seat;
                public GameState View;                       // la vista de este equipo
                public readonly Queue<(int seq, byte[] wire)> Inbox = new Queue<(int, byte[])>();
                public int Presenting = -1;                  // lote que está viendo ahora
                public int LastPresented = -1;               // último lote que terminó de ver
                public double BusyUntil;
                public bool InRoom = true;
                public bool AwaitingResult;                  // ha enviado un comando y espera respuesta
            }

            private readonly Random _rng;
            private readonly RulesConfig _rules = new RulesConfig();
            private readonly GameState _authority;
            private readonly FlowCoordinator _flow = new FlowCoordinator();
            private readonly List<Device> _devices = new List<Device>();
            private readonly Dictionary<int, string> _snapshotAtSeq = new Dictionary<int, string>();
            private readonly double _stallChance;
            private readonly double _leaveChance;
            private double _now;
            private bool _gameOver;
            private int _roundsStarted;
            private double _lastProgress;

            public int ForcedSteps { get; private set; }
            public int PlayersWhoLeft { get; private set; }

            public NetworkSimulator(int seed, int playerCount, double stallChance, double leaveChance)
            {
                _rng = new Random(seed);
                _stallChance = stallChance;
                _leaveChance = leaveChance;

                int[] actors = Enumerable.Range(1, playerCount).Select(i => i * 3).ToArray(); // con huecos, como en Photon
                _authority = TestCards.Game(_rules.StartingCredits, actors);
                for (int i = 0; i < playerCount; i++)
                    _devices.Add(new Device { Actor = actors[i], Seat = i, View = TestCards.Game(_rules.StartingCredits, actors) });
            }

            private IEnumerable<int> ActorsInRoom() => _devices.Where(d => d.InRoom).Select(d => d.Actor);

            // ----- Master -----

            private void Broadcast(List<GameEvent> events)
            {
                if (events.Count == 0) return;

                GameReducer.ApplyAll(_authority, events);
                int seq = _flow.OnBroadcast(_authority, events, _now);
                _snapshotAtSeq[seq] = TestCards.PublicSnapshot(_authority);
                _lastProgress = _now;

                foreach (GameEvent e in events)
                {
                    if (e is GameOver) _gameOver = true;
                    if (e is RoundStarted) _roundsStarted++;
                }

                byte[] wire = EventCodec.Encode(events);
                foreach (Device d in _devices.Where(d => d.InRoom))
                    d.Inbox.Enqueue((seq, wire));
            }

            private bool _maxRoundsReached;

            private void TryAdvance(bool force, int maxRounds)
            {
                if (!force && !_flow.EveryonePresented(ActorsInRoom()))
                    return;

                if (!force)
                {
                    // La barrera: nadie que siga en la sala puede ir por detrás del último lote
                    foreach (Device d in _devices.Where(d => d.InRoom))
                        Assert.AreEqual(_flow.LastSeq, d.LastPresented,
                            $"El Master avanzó sin que el actor {d.Actor} hubiera visto el lote {_flow.LastSeq}");
                }

                FlowAction next = _flow.PeekNext;
                if ((next == FlowAction.StartRound || next == FlowAction.NextRound) && _roundsStarted >= maxRounds)
                {
                    _maxRoundsReached = true;
                    return;
                }

                Broadcast(_flow.TakeNextStep(_authority, _rules, _rng, 0.33f));
            }

            private void Submit(Device d, CommandType type, int arg)
            {
                GameEngine.Result result = GameEngine.Handle(_authority, _rules, new GameCommand(type, new PlayerId(d.Actor), arg));
                if (!result.IsValid)
                {
                    d.AwaitingResult = false; // RPC_CommandRejected
                    return;
                }
                Broadcast(result.Events);
            }

            // ----- Equipos -----

            private double PresentationTime(List<GameEvent> events)
            {
                if (_rng.NextDouble() < _stallChance)
                    return PresentationTimeout + 5 + _rng.NextDouble() * 20; // se atasca más que el plazo
                if (events.Any(e => e is RoundEnded))
                    return 8;
                if (events.Any(e => e is RoundStarted || e is CardsShifted || e is PhaseChanged p && p.Phase == GamePhase.Reveal))
                    return 2 + _rng.NextDouble() * 4;
                return _rng.NextDouble() * 2;
            }

            private void StepDevice(Device d, int maxRounds)
            {
                // ¿Ha terminado de ver el lote actual?
                if (d.Presenting >= 0 && _now >= d.BusyUntil)
                {
                    Assert.AreEqual(_snapshotAtSeq[d.Presenting], TestCards.PublicSnapshot(d.View),
                        $"El actor {d.Actor} ve otra partida tras el lote {d.Presenting}");
                    d.LastPresented = d.Presenting;
                    d.Presenting = -1;
                    d.AwaitingResult = false;
                    _flow.Ack(d.Actor, d.LastPresented); // RPC_PresentationDone
                    TryAdvance(force: false, maxRounds);
                }

                // Siguiente lote de su cola
                if (d.Presenting < 0 && d.Inbox.Count > 0)
                {
                    var (seq, wire) = d.Inbox.Dequeue();
                    List<GameEvent> events = EventCodec.Decode(wire);
                    GameReducer.ApplyAll(d.View, events);
                    d.Presenting = seq;
                    d.BusyUntil = _now + PresentationTime(events);
                }
            }

            private void MaybeAct(Device d)
            {
                // Como la UI: solo con la cola al día, sin un comando en vuelo y si su vista dice que le toca
                if (!d.InRoom || d.Presenting >= 0 || d.Inbox.Count > 0 || d.AwaitingResult) return;
                if (d.View.CurrentPlayerIndex != d.Seat || !d.View.IsRoundActive) return;
                if (_rng.NextDouble() > 0.4) return; // el jugador piensa

                var candidates = new List<(CommandType, int)>();
                if (CommandValidator.IsBettingPhase(d.View.CurrentPhase))
                {
                    candidates.AddRange(new[] { (CommandType.Check, 0), (CommandType.Match, 0), (CommandType.Fold, 0), (CommandType.Bet, 1 + _rng.Next(15)) });
                    if (_rng.Next(4) == 0) candidates.Add((CommandType.Call, 0));
                }
                else if (d.View.CurrentPhase == GamePhase.Drawing)
                {
                    candidates.AddRange(new[] { (CommandType.Draw, 0), (CommandType.Stand, 0), (CommandType.Stand, 0) });
                    for (int i = 0; i < d.View.Players[d.Seat].Hand.GetCount(); i++)
                        candidates.Add((CommandType.Discard, i));
                }

                // El cliente valida con su vista antes de enviar (SubmitCommand)
                var valid = candidates
                    .Where(c => CommandValidator.Validate(d.View, _rules, new GameCommand(c.Item1, new PlayerId(d.Actor), c.Item2)) == null)
                    .ToList();
                if (valid.Count == 0) return;

                var (type, arg) = valid[_rng.Next(valid.Count)];
                d.AwaitingResult = true;
                Submit(d, type, arg);
            }

            private void MaybeLeave()
            {
                if (_leaveChance <= 0 || _rng.NextDouble() >= _leaveChance) return;
                var inRoom = _devices.Where(d => d.InRoom).ToList();
                if (inRoom.Count <= 2) return; // con menos de 2 el juego vuelve al menú

                Device leaver = inRoom[_rng.Next(inRoom.Count)];
                leaver.InRoom = false;
                leaver.Inbox.Clear();
                PlayersWhoLeft++;
                Broadcast(new List<GameEvent> { new PlayerLeft { PlayerIndex = leaver.Seat } }); // OnPlayerLeftRoom
            }

            public void Run(int maxRounds)
            {
                // Lote 0: la inicialización. Cada equipo crea su vista y lo confirma.
                _flow.Start(_now);
                _snapshotAtSeq[0] = TestCards.PublicSnapshot(_authority);
                foreach (Device d in _devices)
                {
                    d.Presenting = 0;
                    d.BusyUntil = _rng.NextDouble() * 2;
                }

                while (_now < MaxSimulatedTime)
                {
                    _now += Tick;
                    if (_gameOver || _maxRoundsReached)
                        return;

                    MaybeLeave();
                    foreach (Device d in _devices.Where(d => d.InRoom))
                        StepDevice(d, maxRounds);
                    foreach (Device d in _devices)
                        MaybeAct(d);

                    // El plazo de la barrera (CheckPresentationTimeout)
                    if (_flow.TimedOut(_now, PresentationTimeout))
                    {
                        ForcedSteps++;
                        _flow.RestartTimer(_now);
                        TryAdvance(force: true, maxRounds);
                    }

                    if (_now - _lastProgress > 600)
                        Assert.Fail($"Partida parada 10 minutos en {_authority.CurrentPhase} (turno {_authority.CurrentPlayerIndex}, " +
                                    $"pasos pendientes {_flow.PendingCount}):\n{TestCards.PublicSnapshot(_authority)}");
                }
                Assert.Fail("La partida no termina");
            }
        }
    }
}
