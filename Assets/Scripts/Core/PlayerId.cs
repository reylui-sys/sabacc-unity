using System;

/// <summary>
/// Identidad estable de un jugador durante toda la partida.
/// En red su valor es el ActorNumber de Photon, que nunca se reutiliza.
///
/// Por qué existe: antes el índice de un jugador se calculaba como
/// "ActorNumber - 1", lo que se rompe en cuanto alguien entra y sale de la
/// sala (ActorNumbers 1 y 3 → el segundo jugador obtenía el índice 2 en una
/// lista de 2). Ahora se busca al jugador por su id: GameState.IndexOf(id).
/// </summary>
public readonly struct PlayerId : IEquatable<PlayerId>
{
    public readonly int Value;

    public PlayerId(int value)
    {
        Value = value;
    }

    public bool Equals(PlayerId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => $"Player#{Value}";

    public static bool operator ==(PlayerId a, PlayerId b) => a.Equals(b);
    public static bool operator !=(PlayerId a, PlayerId b) => !a.Equals(b);
}
