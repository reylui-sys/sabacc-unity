using System.Collections.Generic;

namespace Sabacc.Core.Tests
{
    /// <summary>Utilidades para construir cartas, manos y partidas en los tests.</summary>
    internal static class TestCards
    {
        public static SabaccCard Card(string id)
        {
            SabaccCard card = SabaccCardDefinitions.GetCardById(id);
            if (card == null)
                throw new System.ArgumentException($"ID de carta de test inválido: {id}");
            return card;
        }

        /// <summary>Carta normal de Monedas con ese valor (1..15)</summary>
        public static SabaccCard Coins(int value) => Normal("Monedas", value);

        public static SabaccCard Normal(string suit, int value)
        {
            foreach (var (name, v) in SabaccCardDefinitions.RankCards)
            {
                if (v == value)
                    return new SabaccCard(suit, v, name);
            }
            throw new System.ArgumentException($"No existe carta normal de valor {value}");
        }

        public static Hand HandOf(params SabaccCard[] cards)
        {
            var hand = new Hand();
            foreach (var c in cards)
                hand.AddCard(c);
            return hand;
        }

        /// <summary>Partida con jugadores cuyos ActorNumbers son los indicados.</summary>
        public static GameState Game(int startingCredits, params int[] actorNumbers)
        {
            var seats = new List<Seat>();
            foreach (int actor in actorNumbers)
                seats.Add(new Seat(new PlayerId(actor), $"Jugador {actor}"));
            return new GameState(seats, startingCredits);
        }

        /// <summary>Da al jugador i exactamente estas cartas.</summary>
        public static void GiveHand(GameState state, int playerIndex, params SabaccCard[] cards)
        {
            Hand hand = state.Players[playerIndex].Hand;
            hand.Clear();
            foreach (var c in cards)
                hand.AddCard(c);
        }
    }
}
