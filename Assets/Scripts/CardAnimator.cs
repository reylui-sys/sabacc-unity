using UnityEngine;
using System.Collections;

// Componente para animar cartas individuales
public class CardAnimator : MonoBehaviour
{
    private Vector3 _targetPosition; // Posición objetivo
    private Quaternion _targetRotation; // Rotación objetivo
    private bool _isAnimating = false; // Estado de animación
    private float _animationSpeed = 8f; // Velocidad de animación
    private System.Action _onComplete; // Callback al completar la animación

    // Anima la carta hacia una posición y rotación objetivo
    public void AnimateTo(Vector3 targetPos, Quaternion targetRot, float duration = 0.5f, System.Action onComplete = null)
    {
        _targetPosition = targetPos; // Establecer posición objetivo
        _targetRotation = targetRot; // Establecer rotación objetivo
        _onComplete = onComplete; // Establecer callback
        _animationSpeed = 1f / duration; // Calcular velocidad basada en duración
        _isAnimating = true; // Iniciar animación
    }

    // Anima con curva (más realista) hacia una posición y rotación objetivo
    public void AnimateWithCurve(Vector3 targetPos, Quaternion targetRot, float arcHeight = 1f, float duration = 0.5f, System.Action onComplete = null)
    {
        _onComplete = onComplete; // Establecer callback
        StartCoroutine(AnimateArc(targetPos, targetRot, arcHeight, duration)); // Iniciar coroutine de animación con arco
    }

    void Update()
    {
        // Si no está animando, salir
        if (!_isAnimating)
            return; 

        // Interpolación suave
        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _animationSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, _targetRotation, Time.deltaTime * _animationSpeed);

        // Verificar si llegó al destino
        if (Vector3.Distance(transform.position, _targetPosition) < 0.01f)
        {
            transform.position = _targetPosition; // Asegurar posición final
            transform.rotation = _targetRotation; // Asegurar rotación final
            _isAnimating = false; // Finalizar animación
            _onComplete?.Invoke(); // Llamar callback si existe
        }
    }

    // Animación con arco (más visualmente atractivo) 
    private IEnumerator AnimateArc(Vector3 endPos, Quaternion endRot, float arcHeight, float duration)
    {
        Vector3 startPos = transform.position; // Posición inicial
        Quaternion startRot = transform.rotation; // Rotación inicial
        float elapsed = 0f; // Tiempo transcurrido

        // Animar durante la duración especificada
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // Incrementar tiempo transcurrido
            float t = elapsed / duration; // Normalizar tiempo (0 a 1)

            // Curva suave (ease in-out) para t
            float smoothT = t * t * (3f - 2f * t);

            // Posición con arco parabólico
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, smoothT);
            float arc = arcHeight * Mathf.Sin(smoothT * Mathf.PI); // Calcular altura del arco
            currentPos.y += arc; // Ajustar altura

            transform.position = currentPos; // Actualizar posición
            transform.rotation = Quaternion.Lerp(startRot, endRot, smoothT); // Actualizar rotación

            yield return null; // Esperar al siguiente frame
        }

        transform.position = endPos; // Asegurar posición final
        transform.rotation = endRot; // Asegurar rotación final
        _onComplete?.Invoke(); // Llamar callback si existe
    }

    // Animación de volteo
    public void FlipCard(bool faceUp, float duration = 0.3f, System.Action onComplete = null)
    {
        StartCoroutine(FlipCoroutine(faceUp, duration, onComplete)); // Iniciar coroutine de volteo
    }

    private IEnumerator FlipCoroutine(bool faceUp, float duration, System.Action onComplete)
    {
        Vector3 startRot = transform.localEulerAngles; // Rotación inicial
        Vector3 targetRot = faceUp ? Vector3.zero : new Vector3(180f, 0f, 0f); // Rotación objetivo
        float elapsed = 0f; // Tiempo transcurrido

        while (elapsed < duration)
        { 
            elapsed += Time.deltaTime; // Incrementar tiempo transcurrido
            float t = elapsed / duration; // Normalizar tiempo (0 a 1)
            transform.localEulerAngles = Vector3.Lerp(startRot, targetRot, t); // Actualizar rotación
            yield return null; // Esperar al siguiente frame
        }

        transform.localEulerAngles = targetRot; // Asegurar rotación final

        // Actualizar el CardView también
        var cardView = GetComponent<CardView>(); // Obtener componente CardView
        if (cardView != null)
        {
            cardView.SetFaceUp(faceUp); // Actualizar estado de la carta a faceUp
        }
        
        onComplete?.Invoke(); // Llamar callback si existe
    }
}