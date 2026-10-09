using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class HandTests
    {
        [Test]
        public void GetTotal_SumaLosValores_IncluidosNegativos()
        {
            Hand hand = HandOf(Coins(15), Coins(7), Card("La_Estrella")); // 15 + 7 - 10
            Assert.AreEqual(12, hand.GetTotal());
        }

        [TestCase(23, false)]
        [TestCase(24, true)]
        [TestCase(-23, false)]
        [TestCase(-24, true)]
        [TestCase(5, false)]
        public void IsBombOut_FueraDeRango(int total, bool expected)
        {
            Assert.AreEqual(expected, HandWithTotal(total).IsBombOut(), $"total={total}");
        }

        [Test]
        public void IsBombOut_TotalCero_Explota()
        {
            Hand hand = HandOf(Coins(8), Card("Resistencia")); // 8 - 8 = 0
            Assert.IsTrue(hand.IsBombOut());
        }

        [TestCase(23, true)]
        [TestCase(-23, true)]
        [TestCase(22, false)]
        public void IsPureSabacc_SoloConMasMenos23(int total, bool expected)
        {
            Assert.AreEqual(expected, HandWithTotal(total).IsPureSabacc(), $"total={total}");
        }

        [Test]
        public void IsIdiotsArray_IdiotaDosYTresMismoPalo()
        {
            Hand hand = HandOf(Card("El_Idiota"), Card("Monedas_2"), Card("Monedas_3"));
            Assert.IsTrue(hand.IsIdiotsArray());
            Assert.IsFalse(hand.IsBombOut(), "La mano del idiota suma 5, no debe contar como bomb out");
        }

        // Caracterización: la implementación actual EXIGE que el 2 y el 3 sean
        // del mismo palo. Si el equipo decide la regla clásica (cualquier palo),
        // este test debe cambiar a la vez que Hand.IsIdiotsArray.
        [Test]
        public void IsIdiotsArray_DosYTresDeDistintoPalo_NoCuenta_ReglaActual()
        {
            Hand hand = HandOf(Card("El_Idiota"), Card("Monedas_2"), Card("Sables_3"));
            Assert.IsFalse(hand.IsIdiotsArray());
        }

        [Test]
        public void IsIdiotsArray_RequiereExactamenteTresCartas()
        {
            Hand hand = HandOf(Card("El_Idiota"), Card("Monedas_2"), Card("Monedas_3"), Card("Monedas_1"));
            Assert.IsFalse(hand.IsIdiotsArray());
        }

        [Test]
        public void IsIdiotsArray_SinIdiota_NoCuenta()
        {
            Hand hand = HandOf(Card("Monedas_1"), Card("Monedas_2"), Card("Monedas_3"));
            Assert.IsFalse(hand.IsIdiotsArray());
        }

        [TestCase(20, 3)]
        [TestCase(-21, 2)]
        [TestCase(23, 0)]
        [TestCase(1, 22)]
        public void GetDistanceToTarget_DistanciaAlObjetivoMasCercano(int total, int expected)
        {
            Assert.AreEqual(expected, HandWithTotal(total).GetDistanceToTarget());
        }

        /// <summary>Mano con el total pedido usando cartas normales y "El Maligno" (-15).</summary>
        private static Hand HandWithTotal(int total)
        {
            var hand = new Hand();
            int remaining = total;
            while (remaining > 15) { hand.AddCard(Coins(15)); remaining -= 15; }
            while (remaining <= -15) { hand.AddCard(Card("El_Maligno")); remaining += 15; }
            if (remaining > 0) hand.AddCard(Coins(remaining));
            else if (remaining < 0) { hand.AddCard(Card("El_Maligno")); hand.AddCard(Coins(15 + remaining)); }
            Assert.AreEqual(total, hand.GetTotal(), "helper HandWithTotal");
            return hand;
        }
    }
}
