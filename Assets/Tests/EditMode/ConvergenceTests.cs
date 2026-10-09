using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                if (type == GameEventType.PlayerLeft) continue; // no lo provoca la simulación
                Assert.IsTrue(seenEvents.ContainsKey(type), $"La simulación nunca produjo {type}");
            }
            foreach (RoundOutcome outcome in Enum.GetValues(typeof(RoundOutcome)))
                Assert.IsTrue(seenOutcomes.ContainsKey(outcome), $"La simulación nunca terminó una ronda con {outcome}");
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
            private readonly Dictionary<GameEventType, int> _seenEvents;
            private readonly Dictionary<RoundOutcome, int> _seenOutcomes;

            public MatchSimulator(int seed, int playerCount, int[] actorNumbers = null,
                Dictionary<GameEventType, int> seenEvents = null, Dictionary<RoundOutcome, int> seenOutcomes = null)
            {
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
                foreach (GameEvent e in events)
                {
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

            private static string Snapshot(GameState s)
            {
                var sb = new StringBuilder();
                sb.Append($"ronda={s.CurrentRound} fase={s.CurrentPhase} turno={s.CurrentPlayerIndex} dealer={s.DealerIndex} ")
                  .Append($"mano={s.HandPot} sabacc={s.SabaccPot} max={s.CurrentHighestBet} call={s.CallerIndex} ")
                  .Append($"plantados={s.PlayersStood} activa={s.IsRoundActive}\n");
                foreach (Player p in s.Players)
                {
                    sb.Append($"  {p.Id} {p.State} cr={p.Credits} apuesta={p.CurrentBet} actuo={p.HasActedThisBettingRound} " +
                              $"descarto={p.HasDiscardedThisTurn} [");
                    sb.Append(string.Join(" ", p.Hand.GetCards().Select(c => c.GetCardId() + (c.IsProtected() ? "*" : ""))));
                    sb.Append("]\n");
                }
                sb.Append($"  descarte={string.Join(" ", s.DiscardPile.GetCards().Select(c => c.GetCardId()))}\n");
                return sb.ToString();
            }

            // ----- Flujo de partida (lo que hace NetworkGameController en el Master) -----

            public void PlayGame(int maxRounds)
            {
                for (int round = 0; round < maxRounds; round++)
                {
                    GameEngine.PrepareDeck(_master, _rng);
                    var start = GameEngine.PlanRoundStart(_master, _rules);
                    Broadcast(start);
                    if (start.OfType<GameOver>().Any())
                        return;

                    RoundEnded ended = PlayRound();
                    if (ended.Outcome == RoundOutcome.DefinitiveWin)
                        Broadcast(new GameRestarted { StartingCredits = _rules.StartingCredits });
                }
            }

            private RoundEnded PlayRound()
            {
                Broadcast(GameEngine.PlanPhaseStart(_master, GamePhase.FirstBetting));

                for (int step = 0; step < 2000; step++)
                {
                    switch (_master.CurrentPhase)
                    {
                        case GamePhase.FirstBetting:
                        case GamePhase.Calling:
                        case GamePhase.SecondBetting:
                        {
                            var events = Act(BettingCandidates());
                            if (events.OfType<PlayerCalled>().Any())
                            {
                                Broadcast(GameEngine.PlanShifting(_master, 0.33f, _rng, ShiftKind.AfterCall));
                                return Reveal();
                            }
                            var next = GameEngine.PlanBettingStep(_master);
                            Broadcast(next);
                            var lastStanding = next.OfType<RoundEnded>().FirstOrDefault();
                            if (lastStanding != null)
                                return lastStanding;
                            break;
                        }
                        case GamePhase.FirstShift:
                            Broadcast(GameEngine.PlanShifting(_master, 0.33f, _rng, ShiftKind.First));
                            Broadcast(GameEngine.PlanPhaseStart(_master, GamePhase.Drawing));
                            break;
                        case GamePhase.Drawing:
                            Act(DrawingCandidates());
                            if (_master.PlayersStood >= _master.GetActivePlayerCount())
                                Broadcast(GameEngine.PlanPhaseStart(_master, GamePhase.SecondBetting));
                            break;
                        case GamePhase.SecondShift:
                            Broadcast(GameEngine.PlanShifting(_master, 0.33f, _rng, ShiftKind.Second));
                            return Reveal();
                        default:
                            Assert.Fail($"Fase inesperada en la simulación: {_master.CurrentPhase}");
                            break;
                    }
                }
                Assert.Fail("La ronda no terminó: posible bucle en el flujo de fases");
                return null;
            }

            private RoundEnded Reveal()
            {
                Broadcast(GameEngine.PlanPhaseStart(_master, GamePhase.Reveal));
                var settlement = GameEngine.SettleRound(_master, _rules);
                Broadcast(settlement);
                return settlement.OfType<RoundEnded>().Single();
            }

            // ----- Bots: eligen al azar entre las acciones que el validador permite -----

            private List<GameEvent> Act(List<(CommandType type, int arg)> candidates)
            {
                PlayerId current = _master.Players[_master.CurrentPlayerIndex].Id;
                var valid = candidates
                    .Select(c => GameEngine.Handle(_master, _rules, new GameCommand(c.type, current, c.arg)))
                    .Where(r => r.IsValid)
                    .ToList();

                Assert.IsTrue(valid.Count > 0, $"El jugador en turno no tiene ninguna acción válida en {_master.CurrentPhase}");

                var chosen = valid[_rng.Next(valid.Count)].Events;
                Broadcast(chosen);
                return chosen;
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
