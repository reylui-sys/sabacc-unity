using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Sabacc.Core.Tests
{
    [TestFixture]
    public class DeckAndCardTests
    {
        private System.Action<string> _previousError;
        private List<string> _errors;

        [SetUp]
        public void CaptureErrors()
        {
            _previousError = CoreLog.Error;
            _errors = new List<string>();
            CoreLog.Error = msg => _errors.Add(msg);
        }

        [TearDown]
        public void RestoreLog()
        {
            CoreLog.Error = _previousError;
        }

        [Test]
        public void MazoCompleto_Tiene76Cartas_60Normales_16Especiales()
        {
            List<SabaccCard> deck = SabaccCardDefinitions.CreateFullDeck();
            Assert.AreEqual(76, deck.Count);
            Assert.AreEqual(60, deck.Count(c => !c.IsSpecial()));
            Assert.AreEqual(16, deck.Count(c => c.IsSpecial()));
            foreach (string suit in SabaccCardDefinitions.Suits)
                Assert.AreEqual(15, deck.Count(c => c.Suit == suit), suit);
        }

        [Test]
        public void CadaCarta_SuIdSeConvierteDeVueltaEnLaMismaCarta()
        {
            foreach (SabaccCard original in SabaccCardDefinitions.CreateFullDeck())
            {
                string id = original.GetCardId();
                SabaccCard rebuilt = SabaccCardDefinitions.GetCardById(id);

                Assert.IsNotNull(rebuilt, $"No se pudo reconstruir '{id}'");
                Assert.AreEqual(original.Suit, rebuilt.Suit, id);
                Assert.AreEqual(original.Value, rebuilt.Value, id);
                Assert.AreEqual(original.Name, rebuilt.Name, id);
            }
            Assert.AreEqual(0, _errors.Count, "No debería registrarse ningún error");
        }

        [Test]
        public void Ids_Son68Distintos_LasEspecialesTienenDosCopias()
        {
            var ids = SabaccCardDefinitions.CreateFullDeck().Select(c => c.GetCardId()).ToList();
            Assert.AreEqual(60 + 8, ids.Distinct().Count());
        }

        [TestCase("El_Idiota", "El_Idiota")]
        [TestCase("Reina_del_Aire_y_Oscuridad", "Reina_del_Aire_y_Oscuridad")]
        [TestCase("Sables_Comandante", "Sables_Comandante")]
        [TestCase("Monedas_1", "Monedas_1")]
        public void Ids_CoincidenConElNombreDeLosPrefabs(string id, string expected)
        {
            Assert.AreEqual(expected, SabaccCardDefinitions.GetCardById(id).GetCardId());
        }

        [Test]
        public void CardIds_LimpiaEspaciosYAcentos()
        {
            Assert.AreEqual("Moderacion_y_Diseno", CardIds.CleanName("Moderación y Diseño"));
        }

        [TestCase("Monedas_99")]
        [TestCase("Espadas_1")]
        [TestCase("")]
        [TestCase(null)]
        public void GetCardById_IdInvalido_DevuelveNullYRegistraError(string id)
        {
            Assert.IsNull(SabaccCardDefinitions.GetCardById(id));
            Assert.AreEqual(1, _errors.Count);
        }

        [Test]
        public void Draw_MazoVacio_DevuelveNull()
        {
            Assert.IsNull(new Deck().Draw());
        }

        [Test]
        public void Draw_CogeLaCartaDeArriba_YReduceElMazo()
        {
            var deck = new Deck(SabaccCardDefinitions.CreateFullDeck());
            SabaccCard top = deck.GetCards()[deck.GetCount() - 1];

            SabaccCard drawn = deck.Draw();

            Assert.AreSame(top, drawn);
            Assert.AreEqual(75, deck.GetCount());
        }

        [Test]
        public void Shuffle_ConservaExactamenteLasMismasCartas()
        {
            var deck = new Deck(SabaccCardDefinitions.CreateFullDeck());
            var before = deck.GetCards().ToList();

            deck.Shuffle();

            Assert.AreEqual(before.Count, deck.GetCount());
            CollectionAssert.AreEquivalent(before, deck.GetCards());
        }
    }
}
