using System.Collections.Generic;

// Clase base abstracta para cualquier grupo de cartas
// Si es una clase abstracta, se podrá heredar el mazo, la mano, los descartes etc
public abstract class CardCollection
{
    protected List<SabaccCard> cards;

    public CardCollection()
    {
        cards = new List<SabaccCard>();
    }

    // Devuelve la lista de cartas
    public List<SabaccCard> GetCards()
    {
        return cards;
    }
    
    // Devuelve cuantas cartas hay
    public int GetCount()
    {
        return cards.Count;
    }

    // Añade una carta
    // Virtual se usa para hacer override en las clases heredadas
    public virtual void AddCard(SabaccCard card)
    {
        cards.Add(card);
    }
    
    // Elimina una carta
    public virtual SabaccCard RemoveCardAt(int index)
    {
        var card = cards[index]; // Coge la carta para devolverla
        cards.RemoveAt(index);   // La elimina de la lista
        return card;
    }
    
    // Reemplaza una carta en un índice específico (usado para el Shifting)
    public virtual void ReplaceCardAt(int index, SabaccCard newCard)
    {
        if (index >= 0 && index < cards.Count)
        {
            cards[index] = newCard;
        }
    }
    
    // Borra la colección
    public void Clear()
    {
        cards.Clear();
    }
}
