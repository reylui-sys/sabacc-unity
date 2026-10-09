using System.Collections.Generic;

// Clase para la pila de descarte que hereda de CardCollection
public class DiscardPile : CardCollection
{
  // Constructor
  public DiscardPile() : base()
  { }

  // Método para descartar una carta (añadir a la pila)
  public void Discard(SabaccCard card)
  {
    AddCard(card);
  }

  // Método para ver la última carta descartada sin removerla
  public SabaccCard PeekTop()
  {
    if (GetCount() == 0)
      return null;

    return cards[cards.Count - 1];
  }

  // Método para obtener todas las cartas descartadas (para el shifting)
  // Este método es importante para que ApplyShifting no genere duplicados
  public List<SabaccCard> GetAllDiscarded()
  {
    return new List<SabaccCard>(cards);
  }
}