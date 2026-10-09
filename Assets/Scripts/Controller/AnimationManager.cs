using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

// AnimationManager - API centralizada para todas las animaciones de Sabacc
// Maneja: repartos, volteos, movimientos, reorganizaciones, transiciones
// Desacopla completamente la lógica de animación del controlador
public class AnimationManager : MonoBehaviour
{
    // CONFIGURACIÓN DE ANIMACIONES
    [System.Serializable]
    public struct AnimationSettings
    {
        [Header("Duraciones (segundos)")]
        public float cardDealDuration; // Duración para repartir una carta
        public float cardFlipDuration; // Duración para voltear una carta
        public float cardMoveDuration; // Duración para mover una carta
        public float cardArcHeight; // Altura del arco al repartir una carta

        [Header("Delays (segundos)")]
        public float delayBetweenDeals; // Delay entre repartir cartas
        public float delayBetweenFlips; // Delay entre volteos de cartas

        // Crea configuración por defecto
        public static AnimationSettings Default()
        {
            // Valores por defecto que quedan bien para la mayoría de casos
            return new AnimationSettings
            {
                cardDealDuration = 0.5f,
                cardFlipDuration = 0.3f,
                cardMoveDuration = 0.3f,
                cardArcHeight = 1f,
                delayBetweenDeals = 0.2f,
                delayBetweenFlips = 0.1f
            };
        }
    }

    [SerializeField]
    private AnimationSettings _settings = AnimationSettings.Default(); // Configuración actual de animaciones

    // EVENTOS
    public event Action<GameObject> OnCardAnimationStart; // Evento al iniciar animación de carta
    public event Action<GameObject> OnCardAnimationEnd; // Evento al finalizar animación de carta
    public event Action OnSequenceComplete; // Evento al completar una secuencia de animaciones

    // SINGLETON
    public static AnimationManager Instance { get; private set; } // Instancia singleton

    private Queue<IEnumerator> _animationQueue = new Queue<IEnumerator>(); // Cola de animaciones
    private bool _isProcessingQueue = false; // Indicador de procesamiento de cola
    private int _activeAnimations = 0; // Contador de animaciones activas

    void Awake()
    {
        // Configurar singleton
        if (Instance == null)
        {
            Instance = this; // Asignar instancia
            DontDestroyOnLoad(gameObject); // Persistir entre escenas
        }
        else
        {
            Destroy(gameObject); // Destruir duplicados
        }
    }

    // PROPIEDADES PÍšBLICAS

    // Comprueba si hay animaciones activas o en cola
    public bool IsAnimating => _activeAnimations > 0 || _isProcessingQueue;

    // Actualiza la configuración de animaciones
    public void UpdateSettings(AnimationSettings newSettings)
    {
        _settings = newSettings; // Actualizar configuración
    }

    // API: ANIMACIONES SIMPLES

    // Reparte cartas desde el mazo a las manos de jugadores
    public IEnumerator DealCardsToPlayers(
        List<GameObject>[] playerCardInstances, // Listas de instancias de cartas por jugador
        Transform[] handAreas, // Áreas de manos de jugadores
        Transform deckArea, // Área del mazo
        CardPrefabMap cardPrefabMap, // Mapa de prefabs de cartas
        List<Player> players, // Lista de jugadores
        HandPositionCalculator positionCalculator, // Calculador de posiciones para manos
        Action onComplete = null) // Callback al completar
    {
        _activeAnimations++; // Incrementar contador de animaciones

        // VALIDACIONES 
        if (playerCardInstances == null)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: playerCardInstances es NULL!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        if (handAreas == null)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: handAreas es NULL!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        if (deckArea == null)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: deckArea es NULL!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        if (cardPrefabMap == null)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: cardPrefabMap es NULL!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        if (players == null || players.Count == 0)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: players es NULL o vacÍ­o!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        if (positionCalculator == null)
        {
            Debug.LogError("[AnimationManager] DealCardsToPlayers: positionCalculator es NULL!");
            _activeAnimations--; // Decrementar contador de animaciones
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }
        // FIN VALIDACIONES 

        // Repartir 2 cartas a cada jugador activo
        for (int cardNum = 0; cardNum < 2; cardNum++)
        {
            // Recorre cada jugador
            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i]; // Obtener jugador actual

                // Validar Í­ndice y referencias
                if (i >= handAreas.Length)
                {
                    continue;
                }

                if (handAreas[i] == null)
                {
                    continue;
                }

                if (i >= playerCardInstances.Length)
                {
                    continue;
                }

                if (playerCardInstances[i] == null)
                {
                    playerCardInstances[i] = new List<GameObject>();
                }
                
                if(player.State == PlayerState.Active && player.Hand.GetCount() > cardNum)
                {
                    SabaccCard card = player.Hand.GetCards()[cardNum]; // Obtener carta a repartir

                    // Validar carta
                    if (card == null)
                    {
                        continue;
                    }

                    // Calcular posición objetivo en la mano
                    Vector3 targetPos = positionCalculator.CalculatePosition(handAreas[i], cardNum, player.Hand.GetCount());

                    // Repartir carta individual
                    yield return DealSingleCard(
                        card,
                        deckArea,
                        handAreas[i],
                        cardPrefabMap,
                        playerCardInstances[i],
                        targetPos,
                        faceUp: false
                    );
                    
                    yield return new WaitForSeconds(_settings.delayBetweenDeals); // Esperar entre repartos
                }
            }
        }

        _activeAnimations--; // Decrementar contador de animaciones
        onComplete?.Invoke(); // Invocar callback
        OnSequenceComplete?.Invoke(); // Invocar evento de secuencia completa
    }

    // Reparte una sola carta con arco visual
    public IEnumerator DealSingleCard(
        SabaccCard card,
        Transform fromArea,
        Transform toArea,
        CardPrefabMap cardPrefabMap,
        List<GameObject> targetList,
        Vector3 targetPosition,
        bool faceUp = false,
        Action<GameObject> onCardComplete = null)
    {
        _activeAnimations++;

        // VALIDACIONES 
        if (card == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCard: card es NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }

        if (fromArea == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCard: fromArea (deck) es NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }
        
        if (toArea == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCard: toArea (hand) es NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }
        
        if (cardPrefabMap == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCard: cardPrefabMap es NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }
        
        if (targetList == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCard: targetList es NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }
        // FIN VALIDACIONES 

        string cardId = card.GetCardId(); // Obtener ID de la carta
        GameObject prefab = cardPrefabMap.GetPrefab(cardId); // Obtener prefab de la carta

        if (prefab == null)
        {
            _activeAnimations--; // Decrementar contador de animaciones
            onCardComplete?.Invoke(null); // Invocar callback con NULL
            yield break; // Salir de la coroutine
        }

        GameObject instance = Instantiate(prefab, fromArea.position, fromArea.rotation); // Instanciar carta en el mazo
        OnCardAnimationStart?.Invoke(instance); // Invocar evento de inicio de animación

        var cardView = instance.GetComponent<CardView>(); // Obtener componente CardView
        if (cardView == null) // Si no existe, añadirlo
            cardView = instance.AddComponent<CardView>();

        cardView.SetCard(card); // Asignar carta al CardView
        cardView.SetFaceUp(false); // Inicialmente boca abajo

        var animator = instance.GetComponent<CardAnimator>(); // Obtener componente CardAnimator
        if (animator == null) // Si no existe, añadirlo
            animator = instance.AddComponent<CardAnimator>();

        instance.transform.localScale = Vector3.one * 0.5f; // Escalar carta

        bool animComplete = false; // Indicador de animación completa
        // Iniciar animación con arco hacia la posición objetivo
        animator.AnimateWithCurve( 
            targetPosition, // Posición objetivo
            toArea.rotation, // Rotación objetivo
            _settings.cardArcHeight, // Altura del arco
            _settings.cardDealDuration, // Duración de la animación
            () => animComplete = true // Callback al completar
        );

        // Esperar hasta que la animación se complete
        while (!animComplete)
            yield return null;

        instance.transform.SetParent(toArea, true); // Reparentear a la mano
        instance.transform.localRotation = Quaternion.identity; // Resetear rotación local
        instance.transform.localScale = Vector3.one; // Resetear escala local

        targetList.Add(instance); // Añadir instancia a la lista de la mano

        if (faceUp)
        {
            yield return FlipCard(instance, true); // Voltear carta si es necesario
        }

        _activeAnimations--; // Decrementar contador de animaciones
        OnCardAnimationEnd?.Invoke(instance); // Invocar evento de fin de animación
        onCardComplete?.Invoke(instance); // Invocar callback con la instancia de la carta
    }

    // Reparte una carta
    public IEnumerator DealSingleCardFaceDown(
        SabaccCard card,
        Transform fromArea,
        Transform toArea,
        CardPrefabMap cardPrefabMap,
        List<GameObject> targetList,
        Vector3 targetPosition,
        Action<GameObject> onCardComplete = null)
    {
        _activeAnimations++;

        if (card == null || fromArea == null || toArea == null || cardPrefabMap == null || targetList == null)
        {
            Debug.LogError("[AnimationManager] DealSingleCardFaceDown: parametro NULL!");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }

        string cardId = card.GetCardId();
        GameObject prefab = cardPrefabMap.GetPrefab(cardId);

        if (prefab == null)
        {
            Debug.LogError($"[AnimationManager] Prefab no encontrado para: {cardId}");
            _activeAnimations--;
            onCardComplete?.Invoke(null);
            yield break;
        }

        // Crear carta BOCA ABAJO desde el inicio (rotacion 180 en X)
        Quaternion faceDownRotation = Quaternion.Euler(180f, 0f, 0f);
        GameObject instance = Instantiate(prefab, fromArea.position, faceDownRotation);
        OnCardAnimationStart?.Invoke(instance);

        var cardView = instance.GetComponent<CardView>();
        if (cardView == null)
            cardView = instance.AddComponent<CardView>();

        cardView.SetCard(card);
        cardView.SetFaceUp(false);

        var animator = instance.GetComponent<CardAnimator>();
        if (animator == null)
            animator = instance.AddComponent<CardAnimator>();

        instance.transform.localScale = Vector3.one * 0.5f;

        // Animar hacia destino pero mantener rotacion boca abajo
        bool animComplete = false;
        animator.AnimateWithCurve(
            targetPosition,
            faceDownRotation,  // Rotacion destino = boca abajo
            _settings.cardArcHeight,
            _settings.cardDealDuration,
            () => animComplete = true
        );

        while (!animComplete)
            yield return null;

        instance.transform.SetParent(toArea, true);
        instance.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);  // Forzar boca abajo
        instance.transform.localScale = Vector3.one;
        
        targetList.Add(instance);

        // NO hacer flip - la carta queda boca abajo

        _activeAnimations--;
        OnCardAnimationEnd?.Invoke(instance);
        onCardComplete?.Invoke(instance);
    }

    // Voltea una carta
    public IEnumerator FlipCard(
        GameObject cardInstance,
        bool faceUp,
        float? customDuration = null,
        Action onComplete = null)
    {
        if (cardInstance == null)
        {
            Debug.LogError("[AnimationManager] FlipCard: cardInstance es NULL!");
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        var animator = cardInstance.GetComponent<CardAnimator>();
        if (animator == null)
            animator = cardInstance.AddComponent<CardAnimator>();

        float duration = customDuration ?? _settings.cardFlipDuration;
        bool flipComplete = false;

        animator.FlipCard(faceUp, duration, () => flipComplete = true);

        while (!flipComplete)
            yield return null;

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // Voltea mÍºltiples cartas en secuencia
    public IEnumerator FlipCards(
        List<GameObject> cards,
        bool faceUp,
        bool sequential = true,
        Action onComplete = null)
    {
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[AnimationManager] FlipCards: cards es NULL o vacÍ­o");
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        if(sequential)
        {
            foreach(var card in cards)
            {
                if (card != null)
                {
                    yield return FlipCard(card, faceUp);
                    yield return new WaitForSeconds(_settings.delayBetweenFlips);
                }
            }
        }
        else
        {
            // Paralelo
            List<bool> completed = new List<bool>();
            foreach(var card in cards)
                completed.Add(false);

            for(int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    int index = i;
                    StartCoroutine(FlipCard(cards[i], faceUp, null, () => completed[index] = true));
                }
                else
                {
                    completed[i] = true; // Marcar como completada si es null
                }
            }

            while(completed.Contains(false))
                yield return null;
        }

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // Mueve una carta a una posición con animación
    public IEnumerator MoveCard(
        GameObject card,
        Vector3 targetPosition,
        Quaternion targetRotation,
        bool useArc = true,
        float? customDuration = null,
        Action onComplete = null)
    {
        if (card == null)
        {
            Debug.LogError("[AnimationManager] MoveCard: card es NULL!");
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        var animator = card.GetComponent<CardAnimator>();
        if (animator == null)
            animator = card.AddComponent<CardAnimator>();

        float duration = customDuration ?? _settings.cardMoveDuration;
        bool moveComplete = false;

        if(useArc)
        {
            animator.AnimateWithCurve(
                targetPosition,
                targetRotation,
                _settings.cardArcHeight,
                duration,
                () => moveComplete = true
            );
        }
        else
        {
            animator.AnimateTo(
                targetPosition,
                targetRotation,
                duration,
                () => moveComplete = true
            );
        }

        while (!moveComplete)
            yield return null;

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // Reorganiza cartas en una mano con animación
    public IEnumerator ReorganizeHand(
        List<GameObject> hand,
        Transform handArea,
        HandPositionCalculator positionCalculator,
        Dictionary<GameObject, Vector3> positionCache = null,
        Action onComplete = null)
    {
        if (hand == null || hand.Count == 0)
        {
            Debug.LogWarning("[AnimationManager] ReorganizeHand: hand es NULL o vacÍ­o");
            onComplete?.Invoke();
            yield break;
        }

        if (handArea == null)
        {
            Debug.LogError("[AnimationManager] ReorganizeHand: handArea es NULL!");
            onComplete?.Invoke();
            yield break;
        }

        if (positionCalculator == null)
        {
            Debug.LogError("[AnimationManager] ReorganizeHand: positionCalculator es NULL!");
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        int totalCards = hand.Count;
        List<bool> animComplete = new List<bool>();

        for(int i = 0; i < totalCards; i++)
            animComplete.Add(false);

        for(int i = 0; i < totalCards; i++)
        {
            GameObject cardInstance = hand[i];
            
            if (cardInstance == null)
            {
                animComplete[i] = true;
                continue;
            }

            Vector3 newPos = positionCalculator.CalculatePosition(handArea, i, totalCards);

            var animator = cardInstance.GetComponent<CardAnimator>();
            if(animator == null)
                animator = cardInstance.AddComponent<CardAnimator>();

            int index = i;
            animator.AnimateTo(newPos, handArea.rotation, _settings.cardMoveDuration, () =>
            {
                animComplete[index] = true;
                if(positionCache != null && cardInstance != null)
                {
                    positionCache[cardInstance] = cardInstance.transform.localPosition;
                }
            });
        }

        while(animComplete.Contains(false))
            yield return null;

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // API: SECUENCIAS COMPLEJAS

    // Repartir y revelar mano de un jugador especÍ­fico
    public IEnumerator DealAndRevealSequence(
        List<GameObject>[] playerCardInstances, // Listas de instancias de cartas por jugador
        Transform[] handAreas, // Áreas de manos de jugadores
        Transform deckArea, // Área del mazo
        CardPrefabMap cardPrefabMap, // Mapa de prefabs de cartas
        List<Player> players, // Lista de jugadores
        int playerToReveal, // Í­ndice del jugador cuya mano se revelará
        HandPositionCalculator positionCalculator, // Calculador de posiciones para manos
        Action onComplete = null) // Callback al completar
    {
        // Repartir cartas a todos los jugadores
        yield return DealCardsToPlayers(
            playerCardInstances,
            handAreas,
            deckArea,
            cardPrefabMap,
            players,
            positionCalculator
        );

        yield return new WaitForSeconds(0.5f); // Pequeña pausa antes de revelar

        if (playerToReveal >= 0 && playerToReveal < playerCardInstances.Length)
        {
            yield return RevealPlayerHand(playerCardInstances[playerToReveal]); // Revelar mano del jugador
        }

        onComplete?.Invoke();
    }

    // Ocultar todas las manos excepto una y luego revelarla
    public IEnumerator HideAllHandsExcept(
        List<GameObject>[] allHands,
        int exceptIndex,
        Action onComplete = null)
    {
        if (allHands == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        for(int i = 0; i < allHands.Length; i++)
        {
            if(i != exceptIndex && allHands[i] != null)
            {
                yield return FlipCards(allHands[i], false, sequential: false);
            }
        }

        if(exceptIndex >= 0 && exceptIndex < allHands.Length && allHands[exceptIndex] != null)
        {
            yield return RevealPlayerHand(allHands[exceptIndex]);
        }

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // Revela las cartas de una mano
    public IEnumerator RevealPlayerHand(
        List<GameObject> hand, // Instancias de cartas en la mano
        Action onComplete = null) // Callback al completar
    {
        if (hand == null || hand.Count == 0)
        {
            onComplete?.Invoke(); // Invocar callback
            yield break; // Salir de la coroutine
        }

        _activeAnimations++; // Incrementar contador de animaciones

        // Voltear cada carta que esté boca abajo
        foreach (var card in hand)
        {
            if (card == null) continue; // Validar instancia

            var cardView = card.GetComponent<CardView>(); // Obtener componente CardView
            if (cardView != null && !cardView.IsFaceUp())
            {
                yield return FlipCard(card, true); // Voltear carta
                yield return new WaitForSeconds(_settings.delayBetweenFlips); // Esperar entre volteos
            }
        }

        _activeAnimations--; // Decrementar contador de animaciones
        onComplete?.Invoke(); // Invocar callback
    }

    // Transición entre jugadores: ocultar uno y revelar otro
    public IEnumerator TransitionBetweenPlayers(
        List<GameObject> currentHand,
        List<GameObject> nextHand,
        Action onComplete = null)
    {
        _activeAnimations++;

        if (currentHand != null && currentHand.Count > 0)
            yield return FlipCards(currentHand, false);
        
        yield return new WaitForSeconds(0.3f);
        
        if (nextHand != null && nextHand.Count > 0)
            yield return RevealPlayerHand(nextHand);

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // Revelar todas las manos en secuencia
    public IEnumerator RevealAllHands(
        List<GameObject>[] allHands,
        Action onComplete = null)
    {
        if (allHands == null)
        {
            Debug.LogError("[AnimationManager] RevealAllHands: allHands es NULL!");
            onComplete?.Invoke();
            yield break;
        }

        _activeAnimations++;

        for(int i = 0; i < allHands.Length; i++)
        {
            if (allHands[i] != null && allHands[i].Count > 0)
            {
                yield return RevealPlayerHand(allHands[i]);
                yield return new WaitForSeconds(0.5f);
            }
        }

        _activeAnimations--;
        onComplete?.Invoke();
    }

    // API: SISTEMA DE COLA

    // Añade una animación a la cola para procesar secuencialmente
    public void QueueAnimation(IEnumerator animation)
    {
        _animationQueue.Enqueue(animation); // Añadir a la cola

        if (!_isProcessingQueue)
        {
            StartCoroutine(ProcessQueue()); // Iniciar procesamiento si no está en curso
        }
    }

    // Procesa la cola de animaciones
    private IEnumerator ProcessQueue()
    {
        _isProcessingQueue = true; // Marcar como procesando

        // Procesar cada animación en la cola
        while (_animationQueue.Count > 0)
        {
            IEnumerator current = _animationQueue.Dequeue(); // Obtener siguiente animación
            yield return StartCoroutine(current); // Ejecutar animación
        }

        _isProcessingQueue = false; // Marcar como no procesando
    }

    // Limpia la cola de animaciones
    public void ClearQueue()
    {
        _animationQueue.Clear(); // Limpiar cola
        StopAllCoroutines(); // Detener cualquier procesamiento en curso
        _isProcessingQueue = false; // Marcar como no procesando
        _activeAnimations = 0; // Resetear contador de animaciones
    }

    // UTILIDADES

    // Obtiene la configuración actual
    public AnimationSettings GetSettings()
    {
        return _settings; // Devolver configuración
    }

    // Espera a que todas las animaciones terminen
    public IEnumerator WaitForAnimationsToComplete()
    {
        // Esperar hasta que no haya animaciones activas
        while (IsAnimating)
            yield return null;
    }
}

// Calculador de posiciones para manos de cartas
public abstract class HandPositionCalculator
{
    // Calcula la posición de una carta en la mano
    public abstract Vector3 CalculatePosition(Transform handArea, int cardIndex, int totalCards);
}

// Calculador de posiciones en linea horizontal
public class LinearHandPositionCalculator : HandPositionCalculator
{
    private float _cardSpacing; // Espaciado entre cartas

    public LinearHandPositionCalculator(float cardSpacing = 0.8f) 
    {
        _cardSpacing = cardSpacing; // Valor por defecto
    }

    public override Vector3 CalculatePosition(Transform handArea, int cardIndex, int totalCards)
    {
        Vector3 center = handArea.position; // Centro del área de la mano
        Vector3 right = handArea.right; // Dirección derecha del área de la mano

        float totalWidth = (totalCards - 1) * _cardSpacing; // Ancho total ocupado por las cartas
        Vector3 startPos = center + right * (totalWidth / 2f); // Posición inicial (extremo derecho)

        return startPos - right * (cardIndex * _cardSpacing); // Calcular posición de la carta
    }

    public void SetSpacing(float newSpacing)
    {
        _cardSpacing = newSpacing; // Actualizar espaciado
    }
}

// Calculador de posiciones en abanico
public class FanHandPositionCalculator : HandPositionCalculator
{
    private float _radius;
    private float _spreadAngle;

    public FanHandPositionCalculator(float radius = 2f, float spreadAngle = 60f)
    {
        _radius = radius;
        _spreadAngle = spreadAngle;
    }

    public override Vector3 CalculatePosition(Transform handArea, int cardIndex, int totalCards)
    {
        if(totalCards == 1)
            return handArea.position;

        float angleStep = _spreadAngle / (totalCards - 1);
        float angle = (cardIndex * angleStep) - (_spreadAngle / 2);
        float angleRad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Sin(angleRad) * _radius, -Mathf.Cos(angleRad) * _radius, 0);

        return handArea.position + offset;
    }
}