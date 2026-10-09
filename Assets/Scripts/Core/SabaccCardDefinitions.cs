using System.Collections.Generic;

// Clase estática, solo existe una vez y no se puede crear instancias nuevas
public static class SabaccCardDefinitions
{
    // Vector estático de solo lectura de strings con los palos
    public static readonly string[] Suits = { "Monedas", "Frascos", "Sables", "Bastones" };

    // Vector de tuplas (como el pair de C++) con el nombre y valor de las cartas normales
    public static readonly (string name, int value)[] RankCards = {
        ("1", 1), ("2", 2), ("3", 3), ("4", 4), ("5", 5),
        ("6", 6), ("7", 7), ("8", 8), ("9", 9), ("10", 10), ("11", 11),
        ("Comandante", 12),
        ("Amante", 13),
        ("Maestro", 14),
        ("As", 15)
    };
    
    // Vector de tuplas (como el pair de C++) con el nombre y valor de las cartas especiales
    public static readonly (string name, int value)[] SpecialCards = {
        ("Reina del Aire y Oscuridad", -2),
        ("Resistencia", -8),
        ("La Estrella", -10),
        ("El Equilibrio", -11),
        ("Fallecimiento", -13),
        ("Moderacion", -14),
        ("El Maligno", -15),
        ("El Idiota", 0)
    };

    // Método para crear un mazo
    public static List<SabaccCard> CreateFullDeck()
    {
        var deck = new List<SabaccCard>();

        // Recorre todos los palos y cartas normales
        foreach (string suit in Suits)
            foreach (var (name, value) in RankCards)
                deck.Add(new SabaccCard(suit, value, name));

        // Recorre las cartas especiales (2 copias de cada una)
        foreach (var (name, value) in SpecialCards)
        {
            deck.Add(new SabaccCard(null, value, name));
            deck.Add(new SabaccCard(null, value, name));
        }

        return deck;
    }

    /// <summary>
    /// Obtiene una carta por su ID - NO DEPENDE DE _deck
    /// Crea la carta directamente basándose en el ID
    /// </summary>
    public static SabaccCard GetCardById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            CoreLog.Error("GetCardById: ID es null o vacío");
            return null;
        }

        // Primero intentar cartas especiales (no tienen guión bajo al inicio o formato diferente)
        foreach (var (name, value) in SpecialCards)
        {
            if (id == CardIds.CleanName(name))
            {
                return new SabaccCard(null, value, name);
            }
        }

        // Para cartas normales: formato es "Palo_Valor" ej: "Monedas_5" o "Sables_Comandante"
        string[] parts = id.Split('_');
        if (parts.Length >= 2)
        {
            string suit = parts[0];
            string cardName = parts[1];
            
            // Verificar que el palo es válido
            bool validSuit = false;
            foreach (string s in Suits)
            {
                if (s == suit)
                {
                    validSuit = true;
                    break;
                }
            }
            
            if (validSuit)
            {
                // Buscar el valor de la carta
                foreach (var (name, value) in RankCards)
                {
                    if (name == cardName)
                    {
                        return new SabaccCard(suit, value, name);
                    }
                }
            }
        }

        CoreLog.Error($"GetCardById: No se pudo crear carta con ID '{id}'");
        return null;
    }
}
