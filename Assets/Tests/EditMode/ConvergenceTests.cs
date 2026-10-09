using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Sabacc.Core.Tests
{
    /// <summary>
    /// La garantía central de la arquitectura: si todos aplican los mismos
    /// eventos con el mismo reducer, todos ven la misma partida.
    ///
    /// Se simulan partidas completas: un Master (que conoce el mazo y decide con
    /// GameEngine) y dos clientes que SOLO reciben los eventos codificados en
    /// bytes, como por la red. Bots aleatorios eligen acciones válidas. Tras cada
    /// mensaje se comprueba que:
    ///  - el estado público de los tres es idéntico;
    ///  - el dinero total (créditos + botes) no cambia: nadie lo crea ni lo destruye;
    ///  - en el Master, las 76 cartas siguen existiendo exactamente una vez.
    ///
    /// El flujo lo conduce FlowCoordinator (GameFlow + cola de pasos), igual que el Master en red.
    /// Para reproducir la carrera real (un jugador pulsa mientras los demás aún ven
    /// la animación anterior), a veces el bot intenta actuar ANTES de que se dé el
    /// paso pendiente: el validador y las guardas de GameFlow deben impedir el lío.
    /// </summary>
    [TestFixture]
    public class ConvergenceTests
    {
        private const int Partidas = 300;

        [Test]
        public void MasterYClientes_VenLaMismaPartida_EnCientosDePartidasAleatorias()
        {
            var seenEvents = new Dictionary<GameEventType, int>();
            var seenOutcomes = new Dictionary<RoundOutcome, int>();

            for (int seed = 1; seed <= Partidas; seed++)
            {
                int players = 2 + seed % 3; // 2, 3 o 4 jugadores
                new MatchSimulator(seed, players, null, seenEvents, seenOutcomes).PlayGame(maxRounds: 12);
            }

            // La simulación solo demuestra algo si de verdad recorre todo el juego
            foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
            {
                if (type == GameEventType.PlayerLeft) continue; // lo cubre el test de abandonos
                Assert.IsTrue(seenEvents.ContainsKey(type), $"La simulación nunca produjo {type}");
            }
            foreach (RoundOutcome outcome in Enum.GetValues(typeof(RoundOutcome)))
                Assert.IsTrue(seenOutcomes.ContainsKey(outcome), $"La simulación nunca terminó una ronda con {outcome}");
        }

        [Test]
        public void ConJugadoresQueAbandonanEnCualquierMomento_TambienConverge()
        {
            var seenEvents = new Dictionary<GameEventType, int>();
            var seenOutcomes = new Dictionary<RoundOutcome, int>();

            for (int seed = 1; seed <= 200; seed++)
            {
                int players = 3 + seed % 2; // 3 o 4: al irse uno siguen quedando al menos 2
                new MatchSimulator(seed, players, null, seenEvents, seenOutcomes, leaveChance: 0.01)
                    .PlayGame(maxRounds: 8);
            }

            Assert.IsTrue(seenEvents.ContainsKey(GameEventType.PlayerLeft), "La simulación nunca hizo abandonar a nadie");
            Assert.IsTrue(seenOutcomes.ContainsKey(RoundOutcome.LastPlayerStanding));
        }

        [Test]
        public void ConActorNumbersConHuecos_TambienConverge()
        {
            new MatchSimulator(seed: 42, playerCount: 3, actorNumbers: new[] { 1, 4, 9 }).PlayGame(maxRounds: 10);
        }

        // ===================================================================

        private sealed class MatchSimulator
        {
            private readonly Random _rng;
            private readonly RulesConfig _rules = new RulesConfig();
            private readonly GameState _master;
            private readonly List<GameState> _clients = new List<GameState>();
            private readonly int _totalMoney;
            private int _messages;
            private readonly FlowCoordinator _flow = new FlowCoordinator();
            private readonly double _leaveChance;
            private readonly Queue<string> _recent = new Queue<string>();
            private bool _gameOver;
            private int _roundsStarted;
            private readonly Dictionary<GameEventType, int> _seenEvents;
            private readonly Dictionary<RoundOutcome, int> _seenOutcomes;

            public MatchSimulator(int seed, int playerCount, int[] actorNumbers = null,
                Dictionary<GameEventType, int> seenEvents = null, Dictionary<RoundOutcome, int> seenOutcomes = null,
                double leaveChance = 0)
            {
                _leaveChance = leaveChance;
                _seenEvents = seenEvents ?? new Dictionary<GameEventType, int>();
                _seenOutcomes = seenOutcomes ?? new Dictionary<RoundOutcome, int>();
                _rng = new Random(seed);
                actorNumbers = actorNumbers ?? Enumerable.Range(1, playerCount).ToArray();
                _master = TestCards.Game(_rules.StartingCredits, actorNumbers);
                for (int i = 0; i < 2; i++)
                    _clients.Add(TestCards.Game(_rules.StartingCredits, actorNumbers));
                _totalMoney = Money(_master);
            }

            // ----- "Red": el Master emite, todos aplican lo mismo -----

            private void Broadcast(IReadOnlyList<GameEvent> events)
            {
                if (events.Count == 0) return;
                byte[] wire = EventCodec.Encode(events);
                GameReducer.ApplyAll(_master, EventCodec.Decode(wire));
                foreach (GameState client in _clients)
                    GameReducer.ApplyAll(client, EventCodec.Decode(wire));
                _messages++;
                _recent.Enqueue(string.Join(", ", events));
                if (_recent.Count > 8) _recent.Dequeue();
                _flow.OnBroadcast(_master, events, now: 0);
                foreach (GameEvent e in events)
                {
                    if (e is GameOver) _gameOver = true;
                    if (e is RoundStarted) _roundsStarted++;
                    _seenEvents[e.Type] = _seenEvents.TryGetValue(e.Type, out int n) ? n + 1 : 1;
                    if (e is RoundEnded ended)
                        _seenOutcomes[ended.Outcome] = _seenOutcomes.TryGetValue(ended.Outcome, out int m) ? m + 1 : 1;
                }
                CheckInvariants(events);
            }

            private void Broadcast(GameEvent e) => Broadcast(new List<GameEvent> { e });

            private void CheckInvariants(IReadOnlyList<GameEvent> last)
            {
                string expected = Snapshot(_master);
                foreach (GameState client in _clients)
                {
                    string actual = Snapshot(client);
                    if (actual != expected)
                        Assert.Fail($"Desincronización tras el mensaje {_messages} ({string.Join(", ", last)}):\n" +
                                    $"MASTER:\n{expected}\nCLIENTE:\n{actual}");
                }

                Assert.AreEqual(_totalMoney, Money(_master), $"El dinero total cambió tras {string.Join(", ", last)}");

                // Las 76 cartas deben estar mientras hay una ronda en juego. Entre rondas
                // (fin de partida, reinicio) las manos viejas se descartan y no cuentan.
                if (_master.DeckIsKnown && _master.IsRoundActive)
                {
                    var all = _master.MainDeck.GetCards()
                        .Concat(_master.DiscardPile.GetCards())
                        .Concat(_master.Players.SelectMany(p => p.Hand.GetCards()))
                        .ToList();
                    Assert.AreEqual(76, all.Count, "Se han perdido o duplicado cartas");
                    Assert.AreEqual(76, new HashSet<SabaccCard>(all).Count, "Una carta está en dos sitios a la vez");
                }
            }

            private static int Money(GameState s) => s.Players.Sum(p => p.Credits) + s.HandPot + s.SabaccPot;

            private static string Snapshot(GameState s) => TestCards.PublicSnapshot(s);

            // ----- Flujo de partida: lo conduce GameFlow, igual que en el juego -----

            public void PlayGame(int maxRounds)
            {
                // Lo primero que hace el Master cuando todos han cargado la partida
                _flow.Start(now: 0);

                for (int step = 0; step < 20000; step++)
                {
                    if (_gameOver)
                        return;

                    // Alguien cierra el juego (OnPlayerLeftRoom en el Master). Con menos
                    // de 2 en la sala el juego vuelve al menú, así que siempre quedan 2.
                    if (_leaveChance > 0 && _rng.NextDouble() < _leaveChance && TryLeave())
                        continue;

                    // El jugador en turno actúa; a veces lo intenta aunque haya un paso
                    // automático pendiente (aún se está viendo la animación anterior)
                    bool playerTries = _flow.PendingCount == 0 || _rng.Next(4) == 0;
                    if (playerTries && TryAct())
                        continue;

                    // Si no, el Master da el siguiente paso automático (pasada la barrera)
                    if (_flow.PendingCount > 0)
                    {
                        FlowAction action = _flow.PeekNext;
                        bool startsRound = action == FlowAction.StartRound || action == FlowAction.NextRound;
                        if (startsRound && _roundsStarted >= maxRounds)
                            return;

                        // Los pasos que ya no proceden (guardas) se descartan solos
                        Broadcast(_flow.TakeNextStep(_master, _rules, _rng, 0.33f));
                        continue;
                    }

                    Assert.Fail($"Partida parada en {_master.CurrentPhase}: ni paso automático ni acción válida del jugador en turno\n" +
                                $"Últimos lotes:\n{string.Join("\n", _recent)}\n{Snapshot(_master)}");
                }
                Assert.Fail("La partida no termina: posible bucle en el flujo");
            }

            private bool TryLeave()
            {
                var inRoom = Enumerable.Range(0, _master.Players.Count).Where(i => !_master.Players[i].HasLeft).ToList();
                if (inRoom.Count <= 2)
                    return false;
                Broadcast(new PlayerLeft { PlayerIndex = inRoom[_rng.Next(inRoom.Count)] });
                return true;
            }

            // ----- Bots: eligen al azar entre las acciones que el validador permite -----

            private bool TryAct()
            {
                List<(CommandType type, int arg)> candidates;
                if (CommandValidator.IsBettingPhase(_master.CurrentPhase))
                    candidates = BettingCandidates();
                else if (_master.CurrentPhase == GamePhase.Drawing)
                    candidates = DrawingCandidates();
                else
                    return false;

                PlayerId current = _master.Players[_master.CurrentPlayerIndex].Id;
                var valid = candidates
                    .Select(c => GameEngine.Handle(_master, _rules, new GameCommand(c.type, current, c.arg)))
                    .Where(r => r.IsValid)
                    .ToList();
                if (valid.Count == 0)
                    return false;

                Broadcast(valid[_rng.Next(valid.Count)].Events);
                return true;
            }

            private List<(CommandType, int)> BettingCandidates()
            {
                var c = new List<(CommandType, int)>
                {
                    (CommandType.Check, 0), (CommandType.Check, 0), (CommandType.Match, 0), (CommandType.Match, 0),
                    (CommandType.Bet, 1 + _rng.Next(20)), (CommandType.Fold, 0)
                };
                if (_rng.Next(4) == 0) c.Add((CommandType.Call, 0));
                return c;
            }

            private List<(CommandType, int)> DrawingCandidates()
            {
                int cards = _master.Players[_master.CurrentPlayerIndex].Hand.GetCount();
                var c = new List<(CommandType, int)> { (CommandType.Draw, 0), (CommandType.Stand, 0) };
                for (int i = 0; i < cards; i++)
                {
                    c.Add((CommandType.Discard, i));
                    c.Add((CommandType.Protect, i));
                    c.Add((CommandType.Unprotect, i));
                }
                return c;
            }
        }
    }
}
