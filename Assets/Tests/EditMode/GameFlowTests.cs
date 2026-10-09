using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    /// <summary>La tabla de transiciones del flujo de la partida.</summary>
    [TestFixture]
    public class GameFlowTests
    {
        private GameState _state;
        private RulesConfig _rules;

        [SetUp]
        public void SetUp()
        {
            _state = Game(100, 1, 2, 3);
            _rules = new RulesConfig();
        }

        private FlowAction Next(params GameEvent[] batch) => GameFlow.Next(_state, batch);

        private List<GameEvent> Plan(FlowAction action) =>
            GameFlow.Plan(action, _state, _rules, new System.Random(1), 0.33f);

        // ===== QUÉ VIENE DESPUÉS =====

        [Test]
        public void TrasRepartir_EmpiezaLaPrimeraApuesta()
        {
            Assert.AreEqual(FlowAction.StartFirstBetting, Next(new RoundStarted()));
        }

        [Test]
        public void TrasUnaAccionDeApuesta_ElMasterDecideElSiguientePaso()
        {
            Assert.AreEqual(FlowAction.BettingStep, Next(new PlayerChecked()));
            Assert.AreEqual(FlowAction.BettingStep, Next(new BetPlaced()));
            Assert.AreEqual(FlowAction.BettingStep, Next(new BetMatched()));
            Assert.AreEqual(FlowAction.BettingStep, Next(new PlayerFolded()));
        }

        [Test]
        public void TrasUnCall_ShiftYRevelacion()
        {
            Assert.AreEqual(FlowAction.ShiftAfterCall, Next(new PlayerCalled()));
            Assert.AreEqual(FlowAction.StartReveal, Next(new CardsShifted { Kind = ShiftKind.AfterCall }));
        }

        [TestCase(GamePhase.FirstShift, FlowAction.ShiftFirst)]
        [TestCase(GamePhase.SecondShift, FlowAction.ShiftSecond)]
        [TestCase(GamePhase.Reveal, FlowAction.Settle)]
        [TestCase(GamePhase.FirstBetting, FlowAction.None)]
        [TestCase(GamePhase.Calling, FlowAction.None)]
        [TestCase(GamePhase.Drawing, FlowAction.None)]
        public void AlEntrarEnUnaFase(GamePhase phase, FlowAction expected)
        {
            Assert.AreEqual(expected, Next(new PhaseChanged { Phase = phase }));
        }

        [Test]
        public void TrasLosShifts()
        {
            Assert.AreEqual(FlowAction.StartDrawing, Next(new CardsShifted { Kind = ShiftKind.First }));
            Assert.AreEqual(FlowAction.StartReveal, Next(new CardsShifted { Kind = ShiftKind.Second }));
        }

        [Test]
        public void Plantarse_SoloAvanzaCuandoSeHanPlantadoTodos()
        {
            _state.Players[0].HasStood = true;
            _state.Players[1].HasStood = true;
            Assert.AreEqual(FlowAction.None, Next(new PlayerStood()));

            _state.Players[2].HasStood = true;
            Assert.AreEqual(FlowAction.StartSecondBetting, Next(new PlayerStood()));
        }

        [Test]
        public void Plantarse_QuienSePlantoYSeFue_NoCuentaComoPendiente()
        {
            // Antes se comparaba un contador con los activos: si alguien se plantaba
            // y luego abandonaba, el contador seguía sumándolo y la fase avanzaba
            // antes de que el último jugador pudiera robar.
            _state.Players[0].HasStood = true;
            _state.Players[0].Fold();       // se plantó y abandonó
            _state.Players[1].HasStood = true;
            Assert.AreEqual(FlowAction.None, Next(new PlayerStood()), "Al jugador 2 aún le falta plantarse");
        }

        [Test]
        public void FinDeRonda_SiguienteRonda_OReinicioSiFueVictoriaDefinitiva()
        {
            Assert.AreEqual(FlowAction.NextRound, Next(new RoundEnded { Outcome = RoundOutcome.BestHand }));
            Assert.AreEqual(FlowAction.NextRound, Next(new RoundEnded { Outcome = RoundOutcome.LastPlayerStanding }));
            Assert.AreEqual(FlowAction.RestartGame, Next(new RoundEnded { Outcome = RoundOutcome.DefinitiveWin }));
            Assert.AreEqual(FlowAction.StartRound, Next(new GameRestarted()));
        }

        [Test]
        public void FinDePartida_NoHayMasPasos()
        {
            Assert.AreEqual(FlowAction.None, Next(new PotAwarded(), new GameOver()));
        }

        [Test]
        public void Manda_ElUltimoEventoQueDecide()
        {
            // La liquidación termina en RoundEnded aunque antes haya penalizaciones y premios
            Assert.AreEqual(FlowAction.NextRound,
                Next(new PenaltyPaid(), new PlayerBombedOut(), new PotAwarded(), new RoundEnded { Outcome = RoundOutcome.BestHand }));
            // Robar o cambiar el turno no decide nada: se espera al jugador
            Assert.AreEqual(FlowAction.None, Next(new CardDrawn(), new TurnChanged()));
        }

        // ===== ALGUIEN ABANDONA LA SALA =====

        [Test]
        public void SiAlguienSeVa_DuranteUnaRonda_SeRevisaElTurno()
        {
            _state.IsRoundActive = true;
            Assert.AreEqual(FlowAction.AfterPlayerLeft, Next(new PlayerLeft { PlayerIndex = 2 }));

            _state.IsRoundActive = false;
            Assert.AreEqual(FlowAction.None, Next(new PlayerLeft { PlayerIndex = 2 }), "Entre rondas no hay turno que revisar");
        }

        [Test]
        public void SiSoloQuedaUno_GanaElBote()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.Drawing;
            _state.HandPot = 30;
            _state.Players[1].Fold();
            _state.Players[2].Fold();

            var ended = Plan(FlowAction.AfterPlayerLeft).OfType<RoundEnded>().Single();
            Assert.AreEqual(RoundOutcome.LastPlayerStanding, ended.Outcome);
            Assert.AreEqual(0, ended.WinnerIndex);
        }

        [Test]
        public void SiSeVaQuienTeniaElTurnoDeApuesta_PasaAlSiguiente()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.CurrentPlayerIndex = 1;
            GameReducer.Apply(_state, new PlayerLeft { PlayerIndex = 1 });

            var turn = Plan(FlowAction.AfterPlayerLeft).OfType<TurnChanged>().Single();
            Assert.AreEqual(2, turn.PlayerIndex);
        }

        [Test]
        public void SiSeVaQuienTeniaElTurnoDeRobo_PasaAlSiguienteQueNoSePlanto()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.Drawing;
            _state.Players[0].HasStood = true;
            _state.CurrentPlayerIndex = 1;
            GameReducer.Apply(_state, new PlayerLeft { PlayerIndex = 1 });

            Assert.AreEqual(2, Plan(FlowAction.AfterPlayerLeft).OfType<TurnChanged>().Single().PlayerIndex);

            _state.Players[2].HasStood = true; // ya se habían plantado todos los demás
            Assert.AreEqual(GamePhase.SecondBetting, Plan(FlowAction.AfterPlayerLeft).OfType<PhaseChanged>().Single().Phase);
        }

        [Test]
        public void SiSeVaAlguienQueNoTeniaElTurno_NoCambiaNada()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.SecondBetting;
            _state.CurrentPlayerIndex = 0;
            _state.Players[2].Fold();
            Assert.AreEqual(0, Plan(FlowAction.AfterPlayerLeft).Count);
        }

        [Test]
        public void QuienAbandono_NoRecibeCartasEnLaSiguienteRonda()
        {
            GameReducer.Apply(_state, new PlayerLeft { PlayerIndex = 1 });
            var started = Plan(FlowAction.NextRound).OfType<RoundStarted>().Single();
            Assert.AreEqual(PlayerState.Folded, started.States[1]);
            Assert.AreEqual(0, started.Hands[1].Length);
            Assert.AreEqual(100, started.Credits[1], "No paga la apuesta inicial");
        }

        // ===== GUARDAS: un paso que ya no procede no hace nada =====

        [Test]
        public void PasoDeApuestas_SiLaRondaYaTermino_NoHaceNada()
        {
            // Ejemplo real: alguien se retira (BettingStep pendiente) y, antes de que
            // el Master lo ejecute, otro abandona y la ronda termina. Sin esta guarda
            // se liquidaría la ronda dos veces.
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.IsRoundActive = false;
            Assert.AreEqual(0, Plan(FlowAction.BettingStep).Count);
        }

        [Test]
        public void PasoDeApuestas_QueLlegaTarde_NoSaltaAlSiguiente()
        {
            // Ejemplo real: A se retira (paso pendiente) y B abandona la sala. El paso
            // de B mueve el turno a C. Cuando llega el paso de A, C aún no ha actuado:
            // pasar el turno otra vez le dejaría sin jugar y la partida se atascaría.
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.CurrentPlayerIndex = 2;
            Assert.AreEqual(0, Plan(FlowAction.BettingStep).Count);

            _state.Players[2].HasActedThisBettingRound = true;
            Assert.AreEqual(1, Plan(FlowAction.BettingStep).Count);
        }

        [Test]
        public void SiQuienTieneElTurnoSoloSeRetiro_ElPasoDeAbandonoNoLoMueve()
        {
            _state = Game(100, 1, 2, 3, 4);
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.CurrentPlayerIndex = 1;
            _state.Players[1].Fold();               // se retiró (su paso de apuestas está en cola)
            GameReducer.Apply(_state, new PlayerLeft { PlayerIndex = 2 }); // y otro se fue

            Assert.AreEqual(0, Plan(FlowAction.AfterPlayerLeft).Count);
        }

        [Test]
        public void Liquidar_SoloEnLaRevelacionDeUnaRondaActiva()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.SecondBetting;
            Assert.AreEqual(0, Plan(FlowAction.Settle).Count);

            _state.CurrentPhase = GamePhase.Reveal;
            GiveHand(_state, 0, Coins(10), Coins(9));
            GiveHand(_state, 1, Coins(10), Coins(8));
            GiveHand(_state, 2, Coins(10), Coins(7));
            Assert.AreEqual(1, Plan(FlowAction.Settle).OfType<RoundEnded>().Count());
        }

        [Test]
        public void NuevaRonda_NoEmpiezaSiYaHayUnaEnCurso()
        {
            _state.IsRoundActive = true;
            Assert.AreEqual(0, Plan(FlowAction.NextRound).Count);
        }

        [Test]
        public void NuevaRonda_BarajaYReparte()
        {
            var events = Plan(FlowAction.StartRound);
            Assert.AreEqual(1, events.OfType<RoundStarted>().Count());
            Assert.IsTrue(_state.DeckIsKnown, "Quien planifica la ronda es la autoridad y conoce el mazo");
        }

        [Test]
        public void Shift_SoloEnSuFase()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.Drawing;
            Assert.AreEqual(0, Plan(FlowAction.ShiftFirst).Count);

            _state.CurrentPhase = GamePhase.FirstShift;
            Assert.AreEqual(1, Plan(FlowAction.ShiftFirst).OfType<CardsShifted>().Count());
        }

        [Test]
        public void ShiftTrasCall_SoloSiAlguienHizoCall()
        {
            _state.IsRoundActive = true;
            _state.CurrentPhase = GamePhase.Calling;
            Assert.AreEqual(0, Plan(FlowAction.ShiftAfterCall).Count);

            _state.CallerIndex = 0;
            Assert.AreEqual(ShiftKind.AfterCall, Plan(FlowAction.ShiftAfterCall).OfType<CardsShifted>().Single().Kind);
        }

        [Test]
        public void SinPaso_NoHayEventos()
        {
            Assert.AreEqual(0, Plan(FlowAction.None).Count);
        }
    }
}
