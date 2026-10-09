using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class GameLogicTests
    {
        private GameLogic _logic;

        [SetUp]
        public void SetUp()
        {
            _logic = new GameLogic();
        }

        // ===== MEJOR MANO =====

        [Test]
        public void MejorMano_GanaLaMasCercanaA23()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(10), Coins(8));  // 18
            GiveHand(state, 1, Coins(10), Coins(10)); // 20

            Assert.AreSame(state.Players[1], _logic.GetBestHandForRound(state));
        }

        [Test]
        public void MejorMano_UnNegativoCercanoA_Menos23_GanaAUnPositivoMasLejano()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(11), Coins(10));                 // 21 → distancia 2
            GiveHand(state, 1, Card("El_Maligno"), Card("Moderacion"), Coins(7)); // -22 → distancia 1

            Assert.AreSame(state.Players[1], _logic.GetBestHandForRound(state));
        }

        [Test]
        public void MejorMano_MismaDistancia_ElPositivoGanaAlNegativo()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Card("El_Maligno"), Coins(5), Card("Resistencia"), Card("Reina_del_Aire_y_Oscuridad")); // -20
            GiveHand(state, 1, Coins(10), Coins(10)); // 20

            Assert.AreSame(state.Players[1], _logic.GetBestHandForRound(state));
        }

        [Test]
        public void MejorMano_IgnoraBombOutsYRetirados()
        {
            GameState state = Game(100, 1, 2, 3);
            GiveHand(state, 0, Coins(15), Coins(14)); // 29 → bomb out
            GiveHand(state, 1, Coins(15), Coins(7));  // 22, pero se retira
            GiveHand(state, 2, Coins(5), Coins(4));   // 9
            state.Players[1].Fold();

            Assert.AreSame(state.Players[2], _logic.GetBestHandForRound(state));
        }

        [Test]
        public void MejorMano_TodosBombOut_DevuelveNull()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(15), Coins(14));
            GiveHand(state, 1, Coins(8), Card("Resistencia")); // 0

            Assert.IsNull(_logic.GetBestHandForRound(state));
        }

        // Caracterización: con un empate EXACTO gana quien esté antes en la
        // lista (siempre el host si empata). La fase GamePhase.Tiebreaker
        // existe pero no se usa. Cuando se implemente el desempate, este
        // test debe reescribirse.
        [Test]
        public void MejorMano_EmpateExacto_GanaElPrimerAsiento_ComportamientoActual()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(10), Coins(9));
            GiveHand(state, 1, Normal("Sables", 10), Normal("Sables", 9));

            Assert.AreSame(state.Players[0], _logic.GetBestHandForRound(state));
        }

        // ===== GANADOR DEFINITIVO =====

        [Test]
        public void GanadorDefinitivo_ManoDelIdiota_GanaASabaccPuro()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(15), Coins(8));                                       // 23
            GiveHand(state, 1, Card("El_Idiota"), Card("Sables_2"), Card("Sables_3"));     // Idiota

            Player winner = _logic.GetDefinitiveWinner(state, out string handType);

            Assert.AreSame(state.Players[1], winner);
            Assert.AreEqual("Mano del Idiota", handType);
        }

        [Test]
        public void GanadorDefinitivo_SabaccPuro()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(10), Coins(9));
            GiveHand(state, 1, Card("El_Maligno"), Coins(15), Card("Resistencia"), Card("Moderacion"), Card("Reina_del_Aire_y_Oscuridad"), Coins(1)); // -23

            Player winner = _logic.GetDefinitiveWinner(state, out string handType);

            Assert.AreSame(state.Players[1], winner);
            Assert.AreEqual("Sabacc Puro", handType);
        }

        [Test]
        public void GanadorDefinitivo_SinManosEspeciales_DevuelveNull()
        {
            GameState state = Game(100, 1, 2);
            GiveHand(state, 0, Coins(10), Coins(9));
            GiveHand(state, 1, Coins(10), Coins(8));

            Assert.IsNull(_logic.GetDefinitiveWinner(state, out string handType));
            Assert.AreEqual("", handType);
        }

        // ===== TURNOS =====

        [Test]
        public void NextPlayer_SaltaRetirados_YDaLaVuelta()
        {
            GameState state = Game(100, 1, 2, 3, 4);
            state.Players[1].Fold();
            state.Players[2].MarkAsBombedOut();
            state.CurrentPlayerIndex = 0;

            _logic.NextPlayer(state);
            Assert.AreEqual(3, state.CurrentPlayerIndex);

            _logic.NextPlayer(state);
            Assert.AreEqual(0, state.CurrentPlayerIndex);
        }

        [Test]
        public void RotateDealer_AvanzaDealer_YEmpiezaElSiguienteActivo()
        {
            GameState state = Game(100, 1, 2, 3);
            state.Players[2].Fold();

            _logic.RotateDealer(state); // dealer 0 → 1, siguiente activo tras 1 es 0 (el 2 está retirado)

            Assert.AreEqual(1, state.DealerIndex);
            Assert.AreEqual(0, state.CurrentPlayerIndex);
        }

        // ===== RONDA Y APUESTAS =====

        [Test]
        public void NuevaRonda_MazoCompleto_BoteDeManoACero_BoteSabaccPersiste()
        {
            GameState state = Game(100, 1, 2);
            state.HandPot = 40;
            state.SabaccPot = 70;
            state.Players[0].Fold();

            _logic.InitializeNewRound(state);

            Assert.AreEqual(1, state.CurrentRound);
            Assert.AreEqual(76, state.MainDeck.GetCount());
            Assert.AreEqual(0, state.HandPot);
            Assert.AreEqual(70, state.SabaccPot);
            Assert.AreEqual(PlayerState.Active, state.Players[0].State, "Todos vuelven a estar activos");
        }

        [Test]
        public void Reparto_DosCartasACadaActivo_NadaAlRetirado()
        {
            GameState state = Game(100, 1, 2, 3);
            _logic.InitializeNewRound(state);
            state.Players[1].Fold();

            _logic.DealInitialCards(state);

            Assert.AreEqual(2, state.Players[0].Hand.GetCount());
            Assert.AreEqual(0, state.Players[1].Hand.GetCount());
            Assert.AreEqual(2, state.Players[2].Hand.GetCount());
            Assert.AreEqual(76 - 4, state.MainDeck.GetCount());
        }

        [Test]
        public void PlayerBet_DescuentaYSumaAAmbosBotes()
        {
            GameState state = Game(100, 1, 2);

            Assert.IsTrue(_logic.PlayerBet(state, state.Players[0], 10));

            Assert.AreEqual(90, state.Players[0].Credits);
            Assert.AreEqual(10, state.HandPot);
            Assert.AreEqual(10, state.SabaccPot);
        }

        [Test]
        public void PlayerBet_SinCreditos_NoCambiaNada()
        {
            GameState state = Game(5, 1, 2);

            Assert.IsFalse(_logic.PlayerBet(state, state.Players[0], 10));

            Assert.AreEqual(5, state.Players[0].Credits);
            Assert.AreEqual(0, state.HandPot);
        }

        // ===== SHIFTING =====

        [Test]
        public void Shifting_ProbabilidadCero_NoCambiaNada()
        {
            GameState state = DealtGame();
            var before = AllHandCards(state);

            var changes = _logic.ApplyShifting(state, 0f);

            Assert.AreEqual(0, changes.Count);
            CollectionAssert.AreEqual(before, AllHandCards(state));
        }

        [Test]
        public void Shifting_ProbabilidadUno_CambiaTodasLasNoProtegidas_YRespetaLasProtegidas()
        {
            GameState state = DealtGame();
            SabaccCard protectedCard = state.Players[0].Hand.GetCards()[0];
            protectedCard.SetProtected(true);
            int handCards = AllHandCards(state).Count;

            var changes = _logic.ApplyShifting(state, 1f);

            Assert.AreEqual(handCards - 1, changes.Count, "Cambian todas menos la protegida");
            Assert.AreSame(protectedCard, state.Players[0].Hand.GetCards()[0]);
            foreach (var change in changes)
            {
                SabaccCard nowInHand = state.Players[change.PlayerIndex].Hand.GetCards()[change.CardIndex];
                Assert.AreEqual(change.NewCardId, nowInHand.GetCardId(), "El ShiftResult describe lo que hay en la mano");
            }
        }

        [Test]
        public void Shifting_ConservaLas76Cartas_SinDuplicarNinguna()
        {
            GameState state = DealtGame();

            _logic.ApplyShifting(state, 1f);

            List<SabaccCard> hands = AllHandCards(state);
            List<SabaccCard> deck = state.MainDeck.GetCards();
            Assert.AreEqual(76, hands.Count + deck.Count);
            foreach (SabaccCard card in hands)
                Assert.IsFalse(deck.Contains(card), $"{card} está a la vez en una mano y en el mazo");
        }

        // ===== helpers =====

        private GameState DealtGame()
        {
            GameState state = Game(100, 1, 2, 3);
            _logic.InitializeNewRound(state);
            _logic.DealInitialCards(state);
            return state;
        }

        private static List<SabaccCard> AllHandCards(GameState state)
        {
            return state.Players.SelectMany(p => p.Hand.GetCards()).ToList();
        }
    }
}
