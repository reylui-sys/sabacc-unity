using System.Collections.Generic;
using NUnit.Framework;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class EventCodecTests
    {
        /// <summary>Un ejemplo de cada tipo de evento, con valores no triviales.</summary>
        internal static List<GameEvent> OneOfEach()
        {
            var shifted = new CardsShifted { Kind = ShiftKind.AfterCall };
            // Coherente con la secuencia de OneOfEach para que también se pueda aplicar en orden
            shifted.Shifts.Add(new CardShift { PlayerIndex = 0, CardIndex = 2, OldCardId = "Sables_Maestro", NewCardId = "El_Idiota" });
            shifted.Shifts.Add(new CardShift { PlayerIndex = 0, CardIndex = 0, OldCardId = "Monedas_1", NewCardId = "Reina_del_Aire_y_Oscuridad" });

            return new List<GameEvent>
            {
                new RoundStarted
                {
                    Round = 3, DealerIndex = 1, CurrentPlayerIndex = 2, HandPot = 40, SabaccPot = 75,
                    Credits = new[] { 80, 0, 55 },
                    States = new[] { PlayerState.Active, PlayerState.Folded, PlayerState.Active },
                    Hands = new[] { new[] { "Monedas_1", "El_Idiota" }, new string[0], new[] { "Frascos_As", "Bastones_11" } }
                },
                new PhaseChanged { Phase = GamePhase.SecondBetting, FirstPlayerIndex = 2 },
                new TurnChanged { PlayerIndex = 1 },
                new PlayerChecked { PlayerIndex = 2 },
                new BetPlaced { PlayerIndex = 0, RaiseAmount = 15 },
                new BetMatched { PlayerIndex = 1, Amount = 15 },
                new PlayerCalled { PlayerIndex = 2 },
                new PlayerFolded { PlayerIndex = 1, Penalty = 10 },
                new CardDrawn { PlayerIndex = 0, CardId = "Sables_Maestro" },
                new PlayerStood { PlayerIndex = 0, NextPlayerIndex = 2 },
                new CardDiscarded { PlayerIndex = 2, CardIndex = 1, CardId = "Bastones_11" },
                new CardProtectionChanged { PlayerIndex = 0, CardIndex = 1, IsProtected = true, CardId = "El_Idiota" },
                shifted,
                new PenaltyPaid { PlayerIndex = 2, Amount = 50, Reason = PenaltyReason.BombedOut },
                new PlayerBombedOut { PlayerIndex = 2 },
                new PotAwarded { PlayerIndex = 0, FromHandPot = 40, FromSabaccPot = 125 },
                new RoundEnded
                {
                    Outcome = RoundOutcome.DefinitiveWin, WinnerIndex = 0, HandType = "Mano del Idiota",
                    WinnerHandValue = 5, AmountWon = 165, HandValues = new[] { 5, 0, 31 }, BombedOut = new[] { false, true, true }
                },
                new PlayerLeft { PlayerIndex = 1 },
                new GameOver { WinnerIndex = 0, AmountWon = 300 },
                new GameRestarted { StartingCredits = 100 },
            };
        }

        [Test]
        public void HayUnEjemploDeCadaTipoDeEvento()
        {
            var types = new HashSet<GameEventType>();
            foreach (GameEvent e in OneOfEach()) types.Add(e.Type);
            Assert.AreEqual(System.Enum.GetValues(typeof(GameEventType)).Length, types.Count,
                "Si añades un tipo de evento, añádelo también a OneOfEach (y al codec y al reducer)");
        }

        [Test]
        public void IdaYVuelta_ConservaTodosLosEventos()
        {
            List<GameEvent> original = OneOfEach();

            byte[] bytes = EventCodec.Encode(original);
            List<GameEvent> decoded = EventCodec.Decode(bytes);

            Assert.AreEqual(original.Count, decoded.Count);
            for (int i = 0; i < original.Count; i++)
                Assert.AreEqual(original[i].Type, decoded[i].Type, $"evento {i}");

            // Si al volver a codificar sale exactamente lo mismo, no se perdió ningún campo
            CollectionAssert.AreEqual(bytes, EventCodec.Encode(decoded));
        }

        [Test]
        public void IdaYVuelta_CamposConcretos()
        {
            var decoded = EventCodec.Decode(EventCodec.Encode(OneOfEach()));

            var round = (RoundStarted)decoded[0];
            Assert.AreEqual(75, round.SabaccPot);
            Assert.AreEqual(PlayerState.Folded, round.States[1]);
            Assert.AreEqual(0, round.Hands[1].Length);
            Assert.AreEqual("El_Idiota", round.Hands[0][1]);

            var shifted = (CardsShifted)decoded[12];
            Assert.AreEqual(ShiftKind.AfterCall, shifted.Kind);
            Assert.AreEqual("Reina_del_Aire_y_Oscuridad", shifted.Shifts[1].NewCardId);

            var ended = (RoundEnded)decoded[16];
            Assert.AreEqual("Mano del Idiota", ended.HandType);
            Assert.IsTrue(ended.BombedOut[2]);
        }

        [Test]
        public void ListaVacia_EsValida()
        {
            Assert.AreEqual(0, EventCodec.Decode(EventCodec.Encode(new List<GameEvent>())).Count);
        }

        [Test]
        public void OtraVersionDelFormato_SeRechaza()
        {
            byte[] bytes = EventCodec.Encode(OneOfEach());
            bytes[0] = (byte)(EventCodec.FormatVersion + 1);
            Assert.Throws<System.IO.InvalidDataException>(() => EventCodec.Decode(bytes));
        }
    }
}
