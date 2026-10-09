using System.Collections.Generic;
using System;

// Clase que hereda de una colección de cartas
public class Deck : CardCollection
{
    public Deck() : base()
    {}

    public Deck(List<SabaccCard> c)
    {
        cards = c;
    }

    // Método para barajar
    public void Shuffle()
    {
        var rand = new Random();  // Generador aleatorio
        int n = GetCount(); // Número de cartas
    
        // Mientras aun hayan cartas para barajar
        while (n > 1)
        {
            n--;                      // Decrementa n para actualizar la condición
            int k = rand.Next(n + 1); // Núm. aleatorio incluyendo el actual
      
            // Intercambio de lugar de las cartas usando una auxiliar
            SabaccCard aux = cards[k];
            cards[k] = cards[n];
            cards[n] = aux;
        }
    }

    // Método para robar una carta (del final/arriba del mazo)
    public SabaccCard Draw()
    {
        SabaccCard card; // Objeto carta a devolver
    
        // Si hay cartas se llama al método del padre que elimina y devuelve la carta
        // sino, devuélve null
        if(GetCount() > 0)
            card = RemoveCardAt(GetCount() - 1);
        else
            card = null;
        
        return card;
    }

    // Elimina del mazo ESA instancia concreta de carta (para el shifting).
    // Compara por referencia, no por valor: las cartas especiales tienen dos
    // copias idénticas y comparar por palo/valor/nombre podía quitar la otra
    // copia, dejando en el mazo la carta que acababa de pasar a una mano.
    public bool RemoveCard(SabaccCard cardToRemove)
    {
        return cards.Remove(cardToRemove);
    }

    // Método para añadir una carta al fondo del mazo (para el shifting)
    public void AddCardToBottom(SabaccCard card)
    {
        cards.Insert(0, card);
    }
}
