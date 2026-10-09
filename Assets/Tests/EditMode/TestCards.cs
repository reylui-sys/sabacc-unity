using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        /// <summary>
        /// Todo lo que un jugador puede ver de la partida, como texto: si dos equipos
        /// tienen el mismo, ven la misma partida. No incluye el mazo (solo lo conoce el Master).
        /// </summary>
        public static string PublicSnapshot(GameState s)
        {
            var sb = new StringBuilder();
            sb.Append($"ronda={s.CurrentRound} fase={s.CurrentPhase} turno={s.CurrentPlayerIndex} dealer={s.DealerIndex} ")
              .Append($"mano={s.HandPot} sabacc={s.SabaccPot} max={s.CurrentHighestBet} call={s.CallerIndex} ")
              .Append($"plantados={s.PlayersStood} activa={s.IsRoundActive}\n");
            foreach (Player p in s.Players)
            {
                sb.Append($"  {p.Id} {p.State} cr={p.Credits} apuesta={p.CurrentBet} actuo={p.HasActedThisBettingRound} " +
                          $"descarto={p.HasDiscardedThisTurn} planto={p.HasStood} fuera={p.HasLeft} [");
                sb.Append(string.Join(" ", p.Hand.GetCards().Select(c => c.GetCardId() + (c.IsProtected() ? "*" : ""))));
                sb.Append("]\n");
            }
            sb.Append($"  descarte={string.Join(" ", s.DiscardPile.GetCards().Select(c => c.GetCardId()))}\n");
            return sb.ToString();
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
