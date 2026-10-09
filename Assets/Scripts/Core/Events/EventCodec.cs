using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Convierte listas de eventos en bytes y viceversa, para enviarlas por la red
/// en un único RPC (byte[] viaja por Photon sin registrar tipos propios).
///
/// Formato: [versión][nº de eventos] y, por cada evento, [tipo][campos...].
/// Si se cambia el formato de un evento hay que subir la versión: un cliente
/// con otra versión rechaza el mensaje en vez de leer basura.
/// </summary>
public static class EventCodec
{
    public const byte FormatVersion = 1;

    public static byte[] Encode(IReadOnlyList<GameEvent> events)
    {
        using (var stream = new MemoryStream())
        using (var w = new BinaryWriter(stream, Encoding.UTF8))
        {
            w.Write(FormatVersion);
            w.Write(events.Count);
            foreach (GameEvent e in events)
            {
                w.Write((byte)e.Type);
                Write(w, e);
            }
            w.Flush();
            return stream.ToArray();
        }
    }

    public static List<GameEvent> Decode(byte[] data)
    {
        using (var stream = new MemoryStream(data))
        using (var r = new BinaryReader(stream, Encoding.UTF8))
        {
            byte version = r.ReadByte();
            if (version != FormatVersion)
                throw new InvalidDataException($"Versión de eventos {version} no soportada (se esperaba {FormatVersion})");

            int count = r.ReadInt32();
            var events = new List<GameEvent>(count);
            for (int i = 0; i < count; i++)
            {
                var type = (GameEventType)r.ReadByte();
                events.Add(Read(r, type));
            }
            return events;
        }
    }

    // ===== ESCRITURA =====

    private static void Write(BinaryWriter w, GameEvent e)
    {
        switch (e)
        {
            case RoundStarted x:
                w.Write(x.Round); w.Write(x.DealerIndex); w.Write(x.CurrentPlayerIndex);
                w.Write(x.HandPot); w.Write(x.SabaccPot);
                WriteInts(w, x.Credits);
                w.Write(x.States.Length);
                foreach (PlayerState s in x.States) w.Write((byte)s);
                w.Write(x.Hands.Length);
                foreach (string[] hand in x.Hands) WriteStrings(w, hand);
                break;
            case PhaseChanged x: w.Write((byte)x.Phase); w.Write(x.FirstPlayerIndex); break;
            case TurnChanged x: w.Write(x.PlayerIndex); break;
            case PlayerChecked x: w.Write(x.PlayerIndex); break;
            case BetPlaced x: w.Write(x.PlayerIndex); w.Write(x.RaiseAmount); break;
            case BetMatched x: w.Write(x.PlayerIndex); w.Write(x.Amount); break;
            case PlayerCalled x: w.Write(x.PlayerIndex); break;
            case PlayerFolded x: w.Write(x.PlayerIndex); w.Write(x.Penalty); break;
            case CardDrawn x: w.Write(x.PlayerIndex); w.Write(x.CardId); break;
            case PlayerStood x: w.Write(x.PlayerIndex); w.Write(x.NextPlayerIndex); break;
            case CardDiscarded x: w.Write(x.PlayerIndex); w.Write(x.CardIndex); w.Write(x.CardId); break;
            case CardProtectionChanged x:
                w.Write(x.PlayerIndex); w.Write(x.CardIndex); w.Write(x.IsProtected); w.Write(x.CardId);
                break;
            case CardsShifted x:
                w.Write((byte)x.Kind);
                w.Write(x.Shifts.Count);
                foreach (CardShift s in x.Shifts)
                {
                    w.Write(s.PlayerIndex); w.Write(s.CardIndex); w.Write(s.OldCardId); w.Write(s.NewCardId);
                }
                break;
            case PenaltyPaid x: w.Write(x.PlayerIndex); w.Write(x.Amount); w.Write((byte)x.Reason); break;
            case PlayerBombedOut x: w.Write(x.PlayerIndex); break;
            case PotAwarded x: w.Write(x.PlayerIndex); w.Write(x.FromHandPot); w.Write(x.FromSabaccPot); break;
            case RoundEnded x:
                w.Write((byte)x.Outcome); w.Write(x.WinnerIndex); w.Write(x.HandType ?? "");
                w.Write(x.WinnerHandValue); w.Write(x.AmountWon);
                WriteInts(w, x.HandValues);
                w.Write(x.BombedOut.Length);
                foreach (bool b in x.BombedOut) w.Write(b);
                break;
            case PlayerLeft x: w.Write(x.PlayerIndex); break;
            case GameOver x: w.Write(x.WinnerIndex); w.Write(x.AmountWon); break;
            case GameRestarted x: w.Write(x.StartingCredits); break;
            default:
                throw new ArgumentException($"Evento sin codificación: {e.Type}");
        }
    }

    // ===== LECTURA =====

    private static GameEvent Read(BinaryReader r, GameEventType type)
    {
        switch (type)
        {
            case GameEventType.RoundStarted:
            {
                var x = new RoundStarted
                {
                    Round = r.ReadInt32(), DealerIndex = r.ReadInt32(), CurrentPlayerIndex = r.ReadInt32(),
                    HandPot = r.ReadInt32(), SabaccPot = r.ReadInt32(),
                    Credits = ReadInts(r)
                };
                x.States = new PlayerState[r.ReadInt32()];
                for (int i = 0; i < x.States.Length; i++) x.States[i] = (PlayerState)r.ReadByte();
                x.Hands = new string[r.ReadInt32()][];
                for (int i = 0; i < x.Hands.Length; i++) x.Hands[i] = ReadStrings(r);
                return x;
            }
            case GameEventType.PhaseChanged:
                return new PhaseChanged { Phase = (GamePhase)r.ReadByte(), FirstPlayerIndex = r.ReadInt32() };
            case GameEventType.TurnChanged: return new TurnChanged { PlayerIndex = r.ReadInt32() };
            case GameEventType.PlayerChecked: return new PlayerChecked { PlayerIndex = r.ReadInt32() };
            case GameEventType.BetPlaced: return new BetPlaced { PlayerIndex = r.ReadInt32(), RaiseAmount = r.ReadInt32() };
            case GameEventType.BetMatched: return new BetMatched { PlayerIndex = r.ReadInt32(), Amount = r.ReadInt32() };
            case GameEventType.PlayerCalled: return new PlayerCalled { PlayerIndex = r.ReadInt32() };
            case GameEventType.PlayerFolded: return new PlayerFolded { PlayerIndex = r.ReadInt32(), Penalty = r.ReadInt32() };
            case GameEventType.CardDrawn: return new CardDrawn { PlayerIndex = r.ReadInt32(), CardId = r.ReadString() };
            case GameEventType.PlayerStood: return new PlayerStood { PlayerIndex = r.ReadInt32(), NextPlayerIndex = r.ReadInt32() };
            case GameEventType.CardDiscarded:
                return new CardDiscarded { PlayerIndex = r.ReadInt32(), CardIndex = r.ReadInt32(), CardId = r.ReadString() };
            case GameEventType.CardProtectionChanged:
                return new CardProtectionChanged
                {
                    PlayerIndex = r.ReadInt32(), CardIndex = r.ReadInt32(), IsProtected = r.ReadBoolean(), CardId = r.ReadString()
                };
            case GameEventType.CardsShifted:
            {
                var x = new CardsShifted { Kind = (ShiftKind)r.ReadByte() };
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    x.Shifts.Add(new CardShift
                    {
                        PlayerIndex = r.ReadInt32(), CardIndex = r.ReadInt32(),
                        OldCardId = r.ReadString(), NewCardId = r.ReadString()
                    });
                }
                return x;
            }
            case GameEventType.PenaltyPaid:
                return new PenaltyPaid { PlayerIndex = r.ReadInt32(), Amount = r.ReadInt32(), Reason = (PenaltyReason)r.ReadByte() };
            case GameEventType.PlayerBombedOut: return new PlayerBombedOut { PlayerIndex = r.ReadInt32() };
            case GameEventType.PotAwarded:
                return new PotAwarded { PlayerIndex = r.ReadInt32(), FromHandPot = r.ReadInt32(), FromSabaccPot = r.ReadInt32() };
            case GameEventType.RoundEnded:
            {
                var x = new RoundEnded
                {
                    Outcome = (RoundOutcome)r.ReadByte(), WinnerIndex = r.ReadInt32(), HandType = r.ReadString(),
                    WinnerHandValue = r.ReadInt32(), AmountWon = r.ReadInt32(), HandValues = ReadInts(r)
                };
                x.BombedOut = new bool[r.ReadInt32()];
                for (int i = 0; i < x.BombedOut.Length; i++) x.BombedOut[i] = r.ReadBoolean();
                return x;
            }
            case GameEventType.PlayerLeft: return new PlayerLeft { PlayerIndex = r.ReadInt32() };
            case GameEventType.GameOver: return new GameOver { WinnerIndex = r.ReadInt32(), AmountWon = r.ReadInt32() };
            case GameEventType.GameRestarted: return new GameRestarted { StartingCredits = r.ReadInt32() };
            default:
                throw new InvalidDataException($"Tipo de evento desconocido: {(byte)type}");
        }
    }

    // ===== AUXILIARES =====

    private static void WriteInts(BinaryWriter w, int[] values)
    {
        w.Write(values.Length);
        foreach (int v in values) w.Write(v);
    }

    private static int[] ReadInts(BinaryReader r)
    {
        var values = new int[r.ReadInt32()];
        for (int i = 0; i < values.Length; i++) values[i] = r.ReadInt32();
        return values;
    }

    private static void WriteStrings(BinaryWriter w, string[] values)
    {
        w.Write(values.Length);
        foreach (string v in values) w.Write(v);
    }

    private static string[] ReadStrings(BinaryReader r)
    {
        var values = new string[r.ReadInt32()];
        for (int i = 0; i < values.Length; i++) values[i] = r.ReadString();
        return values;
    }
}
