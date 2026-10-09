using System.Collections.Generic;
using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    /// <summary>
    /// Identidad de jugadores. Regresión del bug "índice = ActorNumber - 1":
    /// si alguien entra y sale de la sala antes de empezar, los ActorNumbers
    /// dejan huecos (1, 3...) y el índice calculado se salía de la lista.
    /// </summary>
    [TestFixture]
    public class GameStateSeatTests
    {
        [Test]
        public void ActorNumbersConHuecos_CadaJugadorTieneSuAsientoCorrecto()
        {
            GameState state = Game(100, 1, 3);

            Assert.AreEqual(0, state.IndexOf(new PlayerId(1)));
            Assert.AreEqual(1, state.IndexOf(new PlayerId(3)), "Con 'ActorNumber - 1' daría 2: fuera de rango");
            Assert.AreEqual(-1, state.IndexOf(new PlayerId(2)), "El jugador que se fue no tiene asiento");
        }

        [Test]
        public void GetPlayer_DevuelveElJugadorConEseId()
        {
            GameState state = Game(100, 4, 7, 9);

            Player player = state.GetPlayer(new PlayerId(7));

            Assert.IsNotNull(player);
            Assert.AreEqual("Jugador 7", player.Name);
            Assert.AreEqual(new PlayerId(7), player.Id);
            Assert.IsNull(state.GetPlayer(new PlayerId(5)));
        }

        [Test]
        public void Constructor_RespetaElOrdenDeAsientos_YCreditosIniciales()
        {
            GameState state = Game(150, 2, 5);

            Assert.AreEqual(2, state.Players.Count);
            Assert.AreEqual(new PlayerId(2), state.Players[0].Id);
            Assert.AreEqual(new PlayerId(5), state.Players[1].Id);
            Assert.AreEqual(150, state.Players[0].Credits);
            Assert.AreEqual(PlayerState.Active, state.Players[1].State);
        }

        [Test]
        public void Constructor_AsientoDuplicado_LanzaExcepcion()
        {
            var seats = new List<Seat>
            {
                new Seat(new PlayerId(1), "Ana"),
                new Seat(new PlayerId(1), "Ana otra vez"),
            };
            Assert.Throws<System.ArgumentException>(() => new GameState(seats, 100));
        }

        [Test]
        public void PlayerId_IgualdadPorValor()
        {
            Assert.IsTrue(new PlayerId(3) == new PlayerId(3));
            Assert.IsTrue(new PlayerId(3) != new PlayerId(4));
            Assert.AreEqual(new PlayerId(3).GetHashCode(), new PlayerId(3).GetHashCode());
        }
    }
}
