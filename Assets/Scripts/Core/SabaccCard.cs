public class SabaccCard
{
    public string Suit;    // Palo de la carta, null si es especial
    public int Value;      // Valor de la carta
    public string Name;    // Name, Value si es normal
    public bool Protected; // Indica si la carta está en el campo de interferencia
    
    public SabaccCard(string suit, int value, string name)
    {
        // Instancia los valores
        Suit = suit;
        Value = value;
        Name = name;
        Protected = false;
    }
    
    // Devuelve si es especial
    public bool IsSpecial()
    {
        return Suit == null;
    }

    // Devuelve si es el idiota
    public bool IsIdiot()
    {
        return Name == "El Idiota";
    }

    // Obtiene el Id de la carta
    public string GetCardId()
    {
        // El ID coincide con el nombre del prefab:
        // especiales → "El_Idiota", normales → "Palo_Nombre" (ej: "Monedas_Comandante")
        return CardIds.For(Suit, Name);
    }

    // Devuelve si la carta está protegida
    public bool IsProtected()
    {
        return Protected;
    }

    // Protege o desprotege la carta
    public void SetProtected(bool value)
    {
        Protected = value;
    }

    // Sobreescribe el .ToString()
    // Sirve para hacer los Debug.Log más cómodamente
    public override string ToString()
    {
        if (IsSpecial())
            return Name + " = " + Value;
        else
            return Name + " de " + Suit + " = " + Value;
    }
}