using UnityEngine;
using UnityEngine.UI;

// Clase para la vista de la carta
public class CardView : MonoBehaviour
{
    private SabaccCard _card;       // Referencia a la carta Sabacc asociada
    private bool _isFaceUp = false; // Estado de la carta (boca arriba o boca abajo)

    // Función para asignar la carta Sabacc a la vista
    public void SetCard(SabaccCard card)
    {
        _card = card;
    }

    // Revelar (faceUp = true) → rotación normal (0,0,0)
    // Ocultar (faceUp = false) → rotación en X = 180°
    public void SetFaceUp(bool faceUp)
    {
        _isFaceUp = faceUp;
        
        // Definir la rotación objetivo
        Vector3 targetRotation;

        // Ajustar la rotación según el estado faceUp
        if (faceUp)
            targetRotation = Vector3.zero;
        else
            targetRotation = new Vector3(180f, 0f, 0f);

        // Aplicar la rotación al transform de la carta
        transform.localRotation = Quaternion.Euler(targetRotation);
    }
    
    // Devuelve si está boca arriba
    public bool IsFaceUp()
    {
        return _isFaceUp;
    }

    // Función que devuelve el objeto Carta
    public SabaccCard GetCard()
    {
        return _card;
    }
}