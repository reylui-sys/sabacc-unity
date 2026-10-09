/// <summary>
/// Un asiento en la mesa: quién se sienta (Id) y cómo se llama.
/// El orden de los asientos en GameState define el índice de cada jugador
/// (cámara, área de mano, turno), y se decide UNA vez al empezar la partida.
/// </summary>
public sealed class Seat
{
    public PlayerId Id { get; }
    public string Name { get; }

    public Seat(PlayerId id, string name)
    {
        Id = id;
        Name = name;
    }
}
