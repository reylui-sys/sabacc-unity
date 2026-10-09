using System.Linq;
using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class GameReducerTests
    {
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _state = Game(100, 1, 2, 3);
        }

        [Test]
        public void RondaEmpezada_EnUnCliente_CreaLasCartasPorSuId()
        {
            GameReducer.Apply(_state, new RoundStarted
            {
                Round = 1, DealerIndex = 0, CurrentPlayerIndex = 1, HandPot = 40, SabaccPot = 40,
                Credits = new[] { 80, 80, 5 },
                States = new[] { PlayerState.Active, PlayerState.Active, PlayerState.Folded },
                Hands = new[] { new[] { "Monedas_1", "El_Idiota" }, new[] { "Sables_As", "Frascos_2" }, new string[0] }
            });

            Assert.IsFalse(_state.DeckIsKnown);
            Assert.AreEqual(GamePhase.Dealing, _state.CurrentPhase);
            Assert.AreEqual(40, _state.HandPot);
            Assert.AreEqual(PlayerState.Folded, _state.Players[2].State);
            Assert.AreEqual(5, _state.Players[2].Credits);
            Assert.AreEqual("El_Idiota", _state.Players[0].Hand.GetCards()[1].GetCardId());
            Assert.AreEqual(15 + 2, _state.Players[1].Hand.GetTotal());
        }

        [Test]
        public void RondaEmpezada_EnLaAutoridad_SacaLasCartasDelMazo()
        {
            GameEngine.PrepareDeck(_state, new System.Random(7));
            string top = _state.MainDeck.PeekIdFromTop(0);

            GameReducer.Apply(_state, new RoundStarted
            {
                Round = 1, Credits = new[] { 80, 80, 80 },
                States = new[] { PlayerState.Active, PlayerState.Active, PlayerState.Active },
                Hands = new[] { new[] { top }, new string[0], new string[0] }
            });

            Assert.AreEqual(75, _state.MainDeck.GetCount());
            Assert.IsFalse(_state.MainDeck.GetCards().Contains(_state.Players[0].Hand.GetCards()[0]));
        }

        [Test]
        public void Subida_IgualaLoPendienteMasLaSubida_YObligaALosDemasAVolverAActuar()
        {
            _state.CurrentHighestBet = 10;
            _state.Players[0].CurrentBet = 10;
            _state.Players[0].HasActedThisBettingRound = true;
            _state.Players[2].Fold();

            GameReducer.Apply(_state, new BetPlaced { PlayerIndex = 1, RaiseAmount = 20 }); // iguala 10 + sube 20

            Player bea = _state.Players[1];
            Assert.AreEqual(70, bea.Credits);
            Assert.AreEqual(30, bea.CurrentBet);
            Assert.AreEqual(30, _state.CurrentHighestBet);
            Assert.AreEqual(30, _state.HandPot);
            Assert.IsTrue(bea.HasActedThisBettingRound);
            Assert.IsFalse(_state.Players[0].HasActedThisBettingRound, "Ana tiene que responder a la subida");
        }

        [Test]
        public void Igualar_PagaYQuedaAlNivelDeLaApuestaMasAlta()
        {
            _state.CurrentHighestBet = 30;
            GameReducer.Apply(_state, new BetMatched { PlayerIndex = 0, Amount = 30 });

            Assert.AreEqual(70, _state.Players[0].Credits);
            Assert.AreEqual(30, _state.Players[0].CurrentBet);
            Assert.AreEqual(30, _state.HandPot);
        }

        [Test]
        public void Retirarse_PagaLaPenalizacionAlBoteDeSabacc()
        {
            GameReducer.Apply(_state, new PlayerFolded { PlayerIndex = 2, Penalty = 10 });

            Assert.AreEqual(PlayerState.Folded, _state.Players[2].State);
            Assert.AreEqual(90, _state.Players[2].Credits);
            Assert.AreEqual(10, _state.SabaccPot);
        }

        [Test]
        public void CambioAFaseDeApuestas_ReiniciaLaRondaDeApuestas()
        {
            _state.CurrentHighestBet = 50;
            _state.CallerIndex = 1;
            _state.Players[0].CurrentBet = 50;
            _state.Players[0].HasActedThisBettingRound = true;

            GameReducer.Apply(_state, new PhaseChanged { Phase = GamePhase.SecondBetting, FirstPlayerIndex = 2 });

            Assert.AreEqual(GamePhase.SecondBetting, _state.CurrentPhase);
            Assert.AreEqual(0, _state.CurrentHighestBet);
            Assert.AreEqual(-1, _state.CallerIndex);
            Assert.AreEqual(0, _state.Players[0].CurrentBet);
            Assert.IsFalse(_state.Players[0].HasActedThisBettingRound);
            Assert.AreEqual(2, _state.CurrentPlayerIndex);
        }

        [Test]
        public void CambioARevelacion_ConservaQuienHizoCall()
        {
            _state.CallerIndex = 1;
            GameReducer.Apply(_state, new PhaseChanged { Phase = GamePhase.Reveal });
            Assert.AreEqual(1, _state.CallerIndex, "La liquidación necesita saber quién hizo CALL");
        }

        [Test]
        public void CambioAFaseDeRobo_NadieHaDescartadoNiSeHaPlantado()
        {
            _state.Players[1].HasDiscardedThisTurn = true;
            _state.PlayersStood = 2;

            GameReducer.Apply(_state, new PhaseChanged { Phase = GamePhase.Drawing, FirstPlayerIndex = 0 });

            Assert.IsFalse(_state.Players[1].HasDiscardedThisTurn);
            Assert.AreEqual(0, _state.PlayersStood);
        }

        [Test]
        public void Plantarse_CuentaYPasaElTurno()
        {
            GameReducer.Apply(_state, new PlayerStood { PlayerIndex = 0, NextPlayerIndex = 2 });
            Assert.AreEqual(1, _state.PlayersStood);
            Assert.AreEqual(2, _state.CurrentPlayerIndex);
        }

        [Test]
        public void Descartar_MueveLaCartaAlDescarte_YMarcaQueYaHaDescartado()
        {
            GiveHand(_state, 0, Coins(5), Coins(6), Coins(7));

            GameReducer.Apply(_state, new CardDiscarded { PlayerIndex = 0, CardIndex = 1, CardId = "Monedas_6" });

            Assert.AreEqual(12, _state.Players[0].Hand.GetTotal());
            Assert.AreEqual("Monedas_6", _state.DiscardPile.PeekTop().GetCardId());
            Assert.IsTrue(_state.Players[0].HasDiscardedThisTurn);
        }

        [Test]
        public void Shift_EnUnCliente_CambiaLaCartaSinTocarElMazo()
        {
            GiveHand(_state, 0, Coins(5), Coins(6));
            var shifted = new CardsShifted();
            shifted.Shifts.Add(new CardShift { PlayerIndex = 0, CardIndex = 1, OldCardId = "Monedas_6", NewCardId = "El_Maligno" });

            GameReducer.Apply(_state, shifted);

            Assert.AreEqual(5 - 15, _state.Players[0].Hand.GetTotal());
            Assert.AreEqual(0, _state.MainDeck.GetCount());
        }

        [Test]
        public void Shift_EnLaAutoridad_LaCartaViejaVuelveAlFondoDelMazo()
        {
            GameEngine.PrepareDeck(_state, new System.Random(3));
            SabaccCard top = _state.MainDeck.GetCards().Last();
            GiveHand(_state, 0, Coins(5));
            SabaccCard old = _state.Players[0].Hand.GetCards()[0];

            var shifted = new CardsShifted();
            shifted.Shifts.Add(new CardShift { PlayerIndex = 0, CardIndex = 0, OldCardId = "Monedas_5", NewCardId = top.GetCardId() });
            GameReducer.Apply(_state, shifted);

            Assert.AreSame(top, _state.Players[0].Hand.GetCards()[0]);
            Assert.AreSame(old, _state.MainDeck.GetCards()[0], "La vieja va al fondo");
            Assert.AreEqual(76, _state.MainDeck.GetCount());
        }

        [Test]
        public void Penalizacion_YReparto_MuevenDineroEntreJugadoresYBotes()
        {
            _state.HandPot = 30;
            GameReducer.Apply(_state, new PenaltyPaid { PlayerIndex = 1, Amount = 50 });
            GameReducer.Apply(_state, new PotAwarded { PlayerIndex = 0, FromHandPot = 30, FromSabaccPot = 50 });

            Assert.AreEqual(50, _state.Players[1].Credits);
            Assert.AreEqual(180, _state.Players[0].Credits);
            Assert.AreEqual(0, _state.HandPot);
            Assert.AreEqual(0, _state.SabaccPot);
        }

        [Test]
        public void Reinicio_DevuelveATodosLosCreditosIniciales()
        {
            _state.Players[0].Credits = 3;
            _state.Players[1].Fold();
            _state.SabaccPot = 99;
            _state.CurrentRound = 7;

            GameReducer.Apply(_state, new GameRestarted { StartingCredits = 100 });

            Assert.IsTrue(_state.Players.All(p => p.Credits == 100 && p.State == PlayerState.Active));
            Assert.AreEqual(0, _state.SabaccPot);
            Assert.AreEqual(0, _state.CurrentRound);
        }

        [Test]
        public void TodosLosTiposDeEvento_SeAplicanSinExcepciones()
        {
            // Si alguien añade un evento y olvida el reducer, este test lo detecta
            GameState state = Game(100, 1, 2, 3);
            foreach (GameEvent e in EventCodecTests.OneOfEach())
                GameReducer.Apply(state, e);
        }
    }
}
