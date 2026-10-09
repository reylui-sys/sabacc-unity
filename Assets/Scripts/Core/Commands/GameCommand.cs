/// <summary>Tipos de acción que un jugador puede pedir durante la partida.</summary>
public enum CommandType : byte
{
    Draw = 1,   // Robar una carta (fase de robo)
    Stand,      // Plantarse (fase de robo)
    Discard,    // Descartar la carta Arg (fase de robo)
    Protect,    // Llevar la carta Arg al campo de interferencia
    Unprotect,  // Devolver la carta Arg a la mano
    Check,      // Pasar sin apostar
    Bet,        // Subir la apuesta en Arg créditos
    Match,      // Igualar la apuesta más alta
    Call,       // CALL: forzar la revelación
    Fold        // Retirarse de la ronda
}

/// <summary>
/// Una INTENCIÓN de un jugador ("quiero robar", "subo 20"), no un hecho.
/// El cliente la envía al Master, que la valida con CommandValidator y solo
/// entonces la ejecuta y avisa a todos del resultado.
///
/// Es un struct con un único argumento entero (índice de carta o cantidad)
/// porque así viaja por la red como (byte, int) sin serialización a medida.
/// Si algún comando necesitase más datos, el siguiente paso natural sería una
/// jerarquía de clases de comando con su propio serializador.
/// </summary>
public readonly struct GameCommand
{
    public readonly CommandType Type;
    public readonly PlayerId Player;
    public readonly int Arg;

    public GameCommand(CommandType type, PlayerId player, int arg = 0)
    {
        Type = type;
        Player = player;
        Arg = arg;
    }

    public override string ToString() => $"{Type}({Arg}) de {Player}";
}
