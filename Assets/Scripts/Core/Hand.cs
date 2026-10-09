using System;

// Clase para la mano del jugador que hereda de la colección de cartas
public class Hand : CardCollection
{
    // Constructor de la clase
    public Hand() : base()
    {}
  
    // Método para obtener el total de la mano
    public int GetTotal()
    {
        int total = 0;
    
        // Recorre todas las cartas y suma su valor
        foreach(SabaccCard card in cards)
            total += card.Value;
        return total;
    }

    // Devuelve si el jugador a explotado
    public bool IsBombOut()
    {
        int total = GetTotal();
        return total== 0 || total > 23 || total < -23;
    }

    // Devuelve si es sabacc puro
    public bool IsPureSabacc()
    {
        int total = GetTotal();
        return total == 23 || total == -23;
    }
  
    // Devuelve si tiene la mano del idiota
    public bool IsIdiotsArray()
    {
        // Inicializa las tres cartas que hay que buscar
        SabaccCard idiotCard = null, twoCard = null, threeCard = null;
    
        // Si no tiene tres cartas se ignora
        if(GetCount() == 3)
        {
            // Recorre las cartas y asigna si hay un idiota (0), 2 o 3
            foreach(SabaccCard card in cards)
            {
                if(card.Value == 0)
                    idiotCard = card;
                else if(card.Value == 2)
                    twoCard = card;
                else if(card.Value == 3)
                    threeCard = card;
            }
        }
    
        // Devuelve "true" si hay carta del idiota y el 2 y el 3 son del mismo palo
        return idiotCard != null && SameSuit(twoCard, threeCard);
    }
  
    // Compruba si dos cartas existen y si son del mismo palo
    private bool SameSuit(SabaccCard a, SabaccCard b)
    {
        return a != null && b != null && a.Suit == b.Suit;
    }

    // Obtiene la distancia a 23 o -23 para saber como de cerca está
    public int GetDistanceToTarget()
    {
        return Math.Min(Math.Abs(23 - GetTotal()), Math.Abs(-23 - GetTotal()));
    }
}