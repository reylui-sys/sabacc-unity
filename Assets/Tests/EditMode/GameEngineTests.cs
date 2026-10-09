using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class GameEngineTests
    {
        private RulesConfig _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = new RulesConfig();
        }

        private static T Single<T>(List<GameEvent> events) where T : GameEvent
        {
            var found = events.OfType<T>().ToList();
            Assert.AreEqual(1, found.Count, $"Se esperaba un {typeof(T).Name}");
            return found[0];
        }

        // ===== EL MOTOR NO MODIFICA EL ESTADO =====

        [Test]
        public void Planificar_NoModificaElEstado()
        {
            GameState state = Game(100, 1, 2);
            GameEngine.PrepareDeck(state, new System.Random(1));
            int deckBefore = state.MainDeck.GetCount();

            GameEngine.PlanRoundStart(state, _rules);
            GameEngine.PlanShifting(state, 1f, new System.Random(1), ShiftKind.First);

            Assert.AreEqual(deckBefore, state.MainDeck.GetCount());
            Assert.AreEqual(100, state.Players[0].Credits);
            Assert.AreEqual(0, state.Players[0].Hand.GetCount());
        }

        // ===== INICIO DE RONDA =====

        [Test]
        public void InicioDeRonda_CobraApuestas_YRepartedosCartasDesdeLaCima()
        {
            GameState state = Game(100, 1, 2, 3);
            GameEngine.PrepareDeck(state, new System.Random(5));
            string top0 = state.MainDeck.PeekIdFromTop(0), top1 = state.MainDeck.PeekIdFromTop(1), top2 = state.MainDeck.PeekIdFromTop(2);

            var started = Single<RoundStarted>(GameEngine.PlanRoundStart(state, _rules));

            Assert.AreEqual(1, started.Round);
            CollectionAssert.AreEqual(new[] { 80, 80, 80 }, started.Credits);
            Assert.AreEqual(30, started.HandPot);
            Assert.AreEqual(30, started.SabaccPot);
            CollectionAssert.AreEqual(new[] { top0, top1 }, started.Hands[0]);
            Assert.AreEqual(top2, started.Hands[1][0]);
        }

        [Test]
        public void InicioDeRonda_QuienNoPuedePagar_SeRetira_YNoRecibeCartas()
        {
            GameState state = Game(100, 1, 2, 3);
            state.Players[1].Credits = 19;
            GameEngine.PrepareDeck(state, new System.Random(5));

            var started = Single<RoundStarted>(GameEngine.PlanRoundStart(state, _rules));

            Assert.AreEqual(PlayerState.Folded, started.States[1]);
            Assert.AreEqual(19, started.Credits[1]);
            Assert.AreEqual(0, started.Hands[1].Length);
            Assert.AreEqual(20, started.HandPot);
        }

        [Test]
        public void InicioDeRonda_ElBoteDeManoQueQuedoSeAcumula()
        {
            GameState state = Game(100, 1, 2);
            state.HandPot = 15; // ronda anterior en la que todos explotaron
            GameEngine.PrepareDeck(state, new System.Random(5));

            Assert.AreEqual(15 + 20, Single<RoundStarted>(GameEngine.PlanRoundStart(state, _rules)).HandPot);
        }

        [Test]
        public void InicioDeRonda_SoloUnoPuedePagar_FinDeLaPartida_YSeLlevaLosBotes()
        {
            GameState state = Game(100, 1, 2);
            state.Players[1].Credits = 5;
            state.SabaccPot = 40;

            var events = GameEngine.PlanRoundStart(state, _rules);

            Assert.AreEqual(40, Single<PotAwarded>(events).FromSabaccPot);
            var over = Single<GameOver>(events);
            Assert.AreEqual(0, over.WinnerIndex);
            Assert.AreEqual(40, over.AmountWon);
        }

        [Test]
        public void InicioDeRonda_NadiePuedePagar_FinSinGanador()
        {
            GameState state = Game(10, 1, 2);
            Assert.AreEqual(-1, Single<GameOver>(GameEngine.PlanRoundStart(state, _rules)).WinnerIndex);
        }

        // ===== APUESTAS =====

        [Test]
        public void TrasUnaAccion_SiFaltaAlguienPorActuar_LeTocaAlSiguiente()
        {
            GameState state = Game(100, 1, 2, 3);
            state.CurrentPhase = GamePhase.FirstBetting;
            state.Players[0].HasActedThisBettingRound = true;
            state.CurrentPlayerIndex = 0;

            Assert.AreEqual(1, Single<TurnChanged>(GameEngine.PlanBettingStep(state)).PlayerIndex);
        }

        [TestCase(GamePhase.FirstBetting, GamePhase.Calling)]
        [TestCase(GamePhase.Calling, GamePhase.FirstShift)]
        [TestCase(GamePhase.SecondBetting, GamePhase.SecondShift)]
        public void SiTodosHanActuadoEIgualado_EmpiezaLaSiguienteFase(GamePhase current, GamePhase next)
        {
            GameState state = Game(100, 1, 2);
            state.CurrentPhase = current;
            foreach (Player p in state.Players) p.HasActedThisBettingRound = true;

            Assert.AreEqual(next, Single<PhaseChanged>(GameEngine.PlanBettingStep(state)).Phase);
        }

        [Test]
        public void SiAlguienNoHaIgualado_LaRondaDeApuestasSigue()
        {
            GameState state = Game(100, 1, 2);
            state.CurrentPhase = GamePhase.FirstBetting;
            foreach (Player p in state.Players) p.HasActedThisBettingRound = true;
            state.CurrentHighestBet = 20;
            state.Players[0].CurrentBet = 20;

            Assert.AreEqual(1, GameEngine.PlanBettingStep(state).OfType<TurnChanged>().Count());
        }

        [Test]
        public void SiSoloQuedaUno_SeLlevaElBoteDeMano()
        {
            GameState state = Game(100, 1, 2, 3);
            state.HandPot = 60;
            state.Players[0].Fold();
            state.Players[2].Fold();

            var events = GameEngine.PlanBettingStep(state);

            var award = Single<PotAwarded>(events);
            Assert.AreEqual(1, award.PlayerIndex);
            Assert.AreEqual(60, award.FromHandPot);
            Assert.AreEqual(RoundOutcome.LastPlayerStanding, Single<RoundEnded>(events).Outcome);
        }

        [Test]
        public void SiNoQuedaNadie_LaRondaTerminaSinGanadorYElBoteSeGuarda()
        {
            var state = Game(100, 1, 2, 3);
            state.IsRoundActive = true;
            state.HandPot = 60;
            foreach (Player p in state.Players) p.Fold();

            var events = GameEngine.PlanLastPlayerStanding(state);

            Assert.AreEqual(0, events.OfType<PotAwarded>().Count());
            Assert.AreEqual(-1, Single<RoundEnded>(events).WinnerIndex);
            GameReducer.ApplyAll(state, events);
            Assert.IsFalse(state.IsRoundActive, "La partida no se queda parada");
            Assert.AreEqual(60, state.HandPot);
        }

        // ===== COMANDOS =====

        [Test]
        public void Robar_ConElMazoVacio_SeRechaza()
        {
            GameState state = Game(100, 1, 2);
            state.IsRoundActive = true;
            state.CurrentPhase = GamePhase.Drawing;
            GiveHand(state, 0, Coins(5), Coins(6));
            state.DeckIsKnown = true; // autoridad, pero sin cartas

            Assert.IsFalse(GameEngine.Handle(state, _rules, new GameCommand(CommandType.Draw, new PlayerId(1))).IsValid);
        }

        [Test]
        public void Plantarse_PasaAlSiguienteActivo()
        {
            GameState state = Game(100, 1, 2, 3);
            state.IsRoundActive = true;
            state.CurrentPhase = GamePhase.Drawing;
            state.Players[1].Fold();

            var result = GameEngine.Handle(state, _rules, new GameCommand(CommandType.Stand, new PlayerId(1)));

            Assert.AreEqual(2, ((PlayerStood)result.Events[0]).NextPlayerIndex);
        }

        [Test]
        public void ComandoInvalido_NoProduceEventos()
        {
            GameState state = Game(100, 1, 2);
            state.IsRoundActive = true;
            state.CurrentPhase = GamePhase.Drawing;

            var result = GameEngine.Handle(state, _rules, new GameCommand(CommandType.Stand, new PlayerId(2))); // no es su turno

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(0, result.Events.Count);
        }

        // ===== LIQUIDACIÓN =====

        [Test]
        public void Liquidacion_QuienExplota_PagaYQuedaFuera()
        {
            GameState state = Game(100, 1, 2);
            state.HandPot = 40;
            GiveHand(state, 0, Coins(15), Coins(14)); // 29
            GiveHand(state, 1, Coins(10), Coins(9));  // 19

            var events = GameEngine.SettleRound(state, _rules);

            Assert.AreEqual(50, Single<PenaltyPaid>(events).Amount);
            Assert.AreEqual(0, Single<PlayerBombedOut>(events).PlayerIndex);
            var ended = Single<RoundEnded>(events);
            Assert.AreEqual(RoundOutcome.BestHand, ended.Outcome);
            Assert.AreEqual(1, ended.WinnerIndex);
            Assert.AreEqual(40, ended.AmountWon);
        }

        [Test]
        public void Liquidacion_GanadorDefinitivo_SeLlevaAmbosBotes_IncluidasLasPenalizaciones()
        {
            GameState state = Game(100, 1, 2);
            state.HandPot = 40;
            state.SabaccPot = 30;
            GiveHand(state, 0, Coins(15), Coins(8));   // 23
            GiveHand(state, 1, Coins(15), Coins(14));  // 29 → paga 50

            var events = GameEngine.SettleRound(state, _rules);
            var award = Single<PotAwarded>(events);

            Assert.AreEqual(40, award.FromHandPot);
            Assert.AreEqual(30 + 50, award.FromSabaccPot);
            Assert.AreEqual("Sabacc Puro", Single<RoundEnded>(events).HandType);
        }

        [Test]
        public void Liquidacion_QuienHaceCallYGanaConLaMejorMano_NoPaga()
        {
            // Regresión: antes se penalizaba si no tenía una mano DEFINITIVA, aunque ganase la ronda
            GameState state = Game(100, 1, 2);
            state.CallerIndex = 0;
            GiveHand(state, 0, Coins(11), Coins(10)); // 21
            GiveHand(state, 1, Coins(10), Coins(8));  // 18

            var events = GameEngine.SettleRound(state, _rules);

            Assert.AreEqual(0, events.OfType<PenaltyPaid>().Count());
            Assert.AreEqual(0, Single<RoundEnded>(events).WinnerIndex);
        }

        [Test]
        public void Liquidacion_QuienHaceCallYPierde_Paga()
        {
            GameState state = Game(100, 1, 2);
            state.CallerIndex = 1;
            GiveHand(state, 0, Coins(11), Coins(10));
            GiveHand(state, 1, Coins(10), Coins(8));

            var penalty = Single<PenaltyPaid>(GameEngine.SettleRound(state, _rules));

            Assert.AreEqual(1, penalty.PlayerIndex);
            Assert.AreEqual(PenaltyReason.FailedCall, penalty.Reason);
            Assert.AreEqual(10, penalty.Amount);
        }

        [Test]
        public void Liquidacion_TodosExplotan_NadieCobra()
        {
            GameState state = Game(100, 1, 2);
            state.HandPot = 40;
            GiveHand(state, 0, Coins(15), Coins(14));
            GiveHand(state, 1, Coins(8), Card("Resistencia")); // 0

            var events = GameEngine.SettleRound(state, _rules);

            Assert.AreEqual(0, events.OfType<PotAwarded>().Count());
            Assert.AreEqual(RoundOutcome.AllBombedOut, Single<RoundEnded>(events).Outcome);
        }

        [Test]
        public void Liquidacion_LaPenalizacionNuncaSuperaLosCreditos()
        {
            GameState state = Game(100, 1, 2);
            state.Players[0].Credits = 12;
            GiveHand(state, 0, Coins(15), Coins(14));
            GiveHand(state, 1, Coins(10), Coins(9));

            Assert.AreEqual(12, GameEngine.SettleRound(state, _rules).OfType<PenaltyPaid>().First().Amount);
        }
    }
}
