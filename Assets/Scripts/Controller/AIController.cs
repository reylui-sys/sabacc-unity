using System.Collections.Generic;
// Clase de la IA, para tener un contrincante
public class AIController
{
    private Player _aiPlayer;    // El jugador de la IA
    private System.Random _rand; // Sistema de aleatoriedad

    // Constructor
    public AIController(Player aiPlayer)
    {
        _aiPlayer = aiPlayer;        // Asignar el jugador de la IA
        _rand = new System.Random(); // Inicializar el generador de números aleatorios
    }

    // Decide si robar una carta o plantarse
    public bool ShouldDraw()
    {
        int handValue = _aiPlayer.Hand.GetTotal(); // Valor total de la mano
        int cardCount = _aiPlayer.Hand.GetCount(); // Número de cartas en la mano

        if (handValue >= 19 && handValue <= 23)
            return false; // Muy cerca, no arriesgar

        if (handValue >= 16 && handValue <= 18)
            return _rand.NextDouble() < 0.3; // 30%

        if (handValue >= 13 && handValue <= 15)
            return _rand.NextDouble() < 0.6; // 60%

        if (handValue > 0 && handValue < 13)
            return true; // Muy bajo, robar

        // Manejo de valores negativos
        if (handValue <= -19 && handValue >= -23)
            return false; // Cerca de -23

        if (handValue <= -16 && handValue > -19)
            return _rand.NextDouble() < 0.3;

        if (handValue <= -13 && handValue > -16)
            return _rand.NextDouble() < 0.6;

        if (handValue < 0 && handValue > -13)
            return true; // Muy poco negativo

        // Si está cerca de 0 o en zona peligrosa, decidir aleatoriamente
        return _rand.NextDouble() < 0.5;
    }
    
    // FUNCIONES HECHAS POR CHATGPT

    // Decide cuántas cartas robar
    public int DecideHowManyCardsToDraw()
    {
        int handValue = _aiPlayer.Hand.GetTotal();
        int currentCards = _aiPlayer.Hand.GetCount();
        
        // Si ya está en zona óptima, no robar
        if((handValue >= 18 && handValue <= 23) || (handValue <= -18 && handValue >= -23))
            return 0;
        
        // Si está muy lejos, robar varias
        if(handValue >= 0 && handValue < 10)
            return _rand.Next(2, 4); // Robar 2-3 cartas
        
        if(handValue < 0 && handValue > -10)
            return _rand.Next(2, 4); // Robar 2-3 cartas
        
        // En zona intermedia, robar 1-2
        if((handValue >= 10 && handValue < 18) || (handValue <= -10 && handValue > -18))
            return _rand.Next(1, 3); // Robar 1-2 cartas
        
        // Por defecto, robar 1
        return 1;
    }

    // Decide si descartar después de robar (solo puede descartar UNA carta)
    public bool ShouldDiscard()
    {
        // Solo descartar si tiene al menos 3 cartas
        if(_aiPlayer.Hand.GetCount() < 3)
            return false;
        
        // Descartar si tiene muchas cartas (5+) o si mejora significativamente la mano
        return _aiPlayer.Hand.GetCount() >= 5 || _rand.NextDouble() < 0.4;
    }

    // Decide qué carta descartar (si ha robado)
    public int DecideCardToDiscard()
    {
        // Obtener la mano actual
        List<SabaccCard> hand = _aiPlayer.Hand.GetCards();
        
        // Si tiene 2 o menos cartas, no descarta
        if(hand.Count <= 2)
            return -1;
        
        // Estrategia: Intentar mantenerse cerca de 23 o -23
        int currentTotal = _aiPlayer.Hand.GetTotal(); // Total actual de la mano
        int bestDiscardIndex = 0;                     // Índice de la mejor carta a descartar
        int bestResultingTotal = int.MaxValue;        // Mejor total resultante después de descartar
        int bestDistance = int.MaxValue;              // Mejor distancia a 23 o -23
        
        // Probar descartar cada carta y ver cuál deja la mejor mano
        for(int i = 0; i < hand.Count; i++)
        {
            int totalWithoutCard = currentTotal - hand[i].Value;              // Total sin la carta i
            int distanceTo23 = System.Math.Abs(23 - totalWithoutCard);        // Distancia a 23
            int distanceToNeg23 = System.Math.Abs(-23 - totalWithoutCard);    // Distancia a -23
            int minDistance = System.Math.Min(distanceTo23, distanceToNeg23); // Distancia mínima a 23 o -23
            
            // También evitar quedarse en 0
            if(totalWithoutCard == 0)
                continue; // No descartar esta, causaría bomb out
            
            // Si esta opción es mejor, actualizar la mejor elección
            if(minDistance < bestDistance)
            {
                bestDistance = minDistance;            // Actualizar la mejor distancia
                bestDiscardIndex = i;                  // Actualizar el índice de la mejor carta a descartar
                bestResultingTotal = totalWithoutCard; // Actualizar el mejor total resultante
            }
        }
        // Devolver el índice de la carta a descartar
        return bestDiscardIndex;
    }
    
    // Decide si hacer "Call" en la fase de Calling
    public bool ShouldCall()
    {
        // Obtener el valor total de la mano
        int handValue = _aiPlayer.Hand.GetTotal();
        
        // Solo hace call si tiene una mano muy buena (20-23)
        return handValue >= 20 && handValue <= 23;
    }
    
    // Decide si retirarse (fold)
    public bool ShouldFold(int amountToCall)
    {
        // Si no puede pagar, debe retirarse
        if(!_aiPlayer.CanAfford(amountToCall))
            return true;
        
        int handValue = _aiPlayer.Hand.GetTotal();
        
        // Si ya está bomb out, retirarse
        if(_aiPlayer.Hand.IsBombOut())
            return true;
        
        // Si la mano es muy mala (< 5 o entre -5 y 5), retirarse
        if(handValue < 5 && handValue > -5)
            return true;
        
        return false;
    }
}