using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// Tutorial guiado del Sabacc
public class TutorialManager : MonoBehaviour
{
    [Header("UI - Panel de Tutorial")]
    public GameObject tutorialPanel;
    public TMP_Text tutorialText;
    public TMP_Text stepIndicator;
    public TMP_Text pressEnterText;

    [Header("UI - Elementos del juego")]
    public TMP_Text handPotText;
    public TMP_Text sabaccPotText;
    public TMP_Text phaseText;
    public TMP_Text player1CreditsText;
    public TMP_Text player2CreditsText;
    public TMP_Text player1HandValueText;
    public TMP_Text player2HandValueText;

    [Header("Areas del juego")]
    public Transform deckArea;
    public Transform discardArea;
    public Transform player1HandArea;
    public Transform player2HandArea;
    public Transform player1InterferenceField;

    [Header("Camaras")]
    public Camera mainCamera;
    public Camera player1Camera;
    public Camera player2Camera;

    [Header("Configuracion")]
    public CardPrefabMap cardPrefabMap;
    public float cardSpacing = 0.8f;

    [Header("Escena de salida")]
    public string exitSceneName = "Inicio";

    // Estado interno
    private int _currentStep = 0;
    private bool _canAdvance = true;
    private bool _isAnimating = false;
    private bool _isFinalStep = false;
    private List<TutorialStep> _steps;
    private List<GameObject> _player1Cards = new List<GameObject>();
    private List<GameObject> _player2Cards = new List<GameObject>();
    private List<GameObject> _deckCards = new List<GameObject>();
    private List<GameObject> _discardCards = new List<GameObject>();
    private List<GameObject> _protectedCards = new List<GameObject>();

    // Datos simulados
    private int _player1Credits = 100;
    private int _player2Credits = 100;
    private int _handPot = 0;
    private int _sabaccPot = 0;

    void Start()
    {
        InitializeTutorialSteps(); // Crear pasos del tutorial
        SetupCameras();            // Configurar camaras
        UpdateAllUI();             // Actualizar UI inicial
        ShowStep(0);               // Mostrar primer paso
        
        if (pressEnterText != null) // Instrucciones iniciales        
            pressEnterText.text = "Pulsa INTRO para continuar... (ESC para salir)";
    }

    void Update()
    {
        // ESC para salir siempre
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitTutorial();
            return;
        }

        // INTRO para avanzar o salir en el último paso
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // Si es el paso final, salir directamente
            if (_isFinalStep)
            {
                ExitTutorial();
                return;
            }
            
            if (_canAdvance && !_isAnimating)
            {
                AdvanceStep();
            }
        }
    }

    void SetupCameras()
    {
        if (player1Camera != null) player1Camera.enabled = false; // Desactivar camaras de jugadores
        if (player2Camera != null) player2Camera.enabled = false;
        if (mainCamera != null) mainCamera.enabled = true;       // Activar camara principal
    }

    void SwitchToCamera(int cameraIndex)
    {
        if (mainCamera != null) mainCamera.enabled = (cameraIndex == 0); // Camara principal  
        if (player1Camera != null) player1Camera.enabled = (cameraIndex == 1); // Camara jugador 1
        if (player2Camera != null) player2Camera.enabled = (cameraIndex == 2); // Camara jugador 2
    }

    void InitializeTutorialSteps()
    {
        _steps = new List<TutorialStep> // Definir pasos del tutorial
        {
            // INTRODUCCION
            new TutorialStep(
                "Bienvenido al Sabacc",
                "El Sabacc es un juego de cartas del universo Star Wars.\n\n" +
                "OBJETIVO: Conseguir una mano con valor 23, -23, o la 'Mano del Idiota'.\n\n" +
                "Si tu mano supera 23, baja de -23, o es 0, EXPLOTAS y pierdes.",
                TutorialAction.None, 0
            ),

            new TutorialStep(
                "Las Cartas",
                "El mazo tiene 76 cartas:\n\n" +
                "- 60 cartas normales (valores 1-15, 4 palos)\n" +
                "- 16 cartas especiales con valores negativos\n\n" +
                "Las especiales incluyen 'El Idiota' (0), 'La Estrella' (-17), etc.",
                TutorialAction.ShowDeck, 0
            ),

            new TutorialStep(
                "Los Botes",
                "Hay DOS botes:\n\n" +
                "BOTE DE MANO: Se gana cada ronda.\n\n" +
                "BOTE DE SABACC: Solo se gana con Sabacc Puro (23/-23) o Mano del Idiota.\n" +
                "Si nadie lo gana, se acumula.",
                TutorialAction.ShowPots, 0
            ),

            new TutorialStep(
                "Inicio de Ronda",
                "Cada ronda, todos pagan apuesta inicial a AMBOS botes.\n\n" +
                "Simulemos: Tu y un oponente pagan 10 creditos cada uno.",
                TutorialAction.PayBets, 0
            ),

            new TutorialStep(
                "Reparto",
                "Cada jugador recibe 2 cartas.\n\n" +
                "Solo TU puedes ver tus cartas.\n" +
                "Las del oponente estan ocultas.",
                TutorialAction.DealCards, 0
            ),

            new TutorialStep(
                "Tus Cartas",
                "Ahora puedes ver tu mano.\n\n" +
                "Tienes un 8 y un 7. Total: 15 puntos.\n\n" +
                "Recuerda: quieres llegar a 23 o -23.",
                TutorialAction.RevealPlayer1, 1
            ),

            new TutorialStep(
                "Fases de Apuestas",
                "Hay rondas de apuestas donde puedes:\n\n" +
                "- PASAR: No apostar (si nadie aposto)\n" +
                "- APOSTAR: Subir la apuesta\n" +
                "- IGUALAR: Pagar lo apostado\n" +
                "- RETIRARSE: Abandonar la ronda\n" +
                "- CALL: Forzar revelacion (con penalizacion si pierdes)",
                TutorialAction.None, 1
            ),

            new TutorialStep(
                "El SHIFT",
                "Lo mas emocionante del Sabacc: EL SHIFT!\n\n" +
                "Aleatoriamente, algunas cartas CAMBIAN.\n" +
                "Esto puede arruinar o mejorar tu mano.\n\n" +
                "Observa como tus cartas tiemblan...",
                TutorialAction.SimulateShift, 1
            ),

            new TutorialStep(
                "Campo de Interferencia",
                "Puedes PROTEGER hasta 2 cartas del Shift.\n\n" +
                "Las cartas protegidas:\n" +
                "- NO cambian durante el Shift\n" +
                "- Son VISIBLES para todos\n" +
                "- NO se pueden descartar\n\n" +
                "Observa como una carta va al campo...",
                TutorialAction.ProtectCard, 1
            ),

            new TutorialStep(
                "Fase de Robo",
                "En la fase de ROBO puedes:\n\n" +
                "- ROBAR: Tomar carta del mazo\n" +
                "- DESCARTAR: Tirar una carta (minimo 2 en mano)\n" +
                "- PLANTARTE: No hacer nada\n\n" +
                "IMPORTANTE: Si descartas, ya NO puedes robar ese turno.",
                TutorialAction.None, 1
            ),

            new TutorialStep(
                "Robar una Carta",
                "Vamos a robar una carta del mazo.\n\n" +
                "La carta llega boca abajo y luego la ves.",
                TutorialAction.DrawCard, 1
            ),

            new TutorialStep(
                "Descartar",
                "Para descartar, selecciona una carta (teclas 1-9)\n" +
                "y pulsa el boton Descartar.\n\n" +
                "Necesitas minimo 2 cartas en la mano.",
                TutorialAction.DiscardCard, 1
            ),

            new TutorialStep(
                "Revelacion",
                "Al final, todos revelan sus cartas.\n\n" +
                "Gana quien tenga:\n" +
                "1. Mano del Idiota (0 + 2 + 3 mismo palo)\n" +
                "2. Sabacc Puro (exactamente 23 o -23)\n" +
                "3. Mejor mano (mas cerca de 23/-23)",
                TutorialAction.RevealAll, 0
            ),

            new TutorialStep(
                "Mano del Idiota",
                "La MEJOR mano posible:\n\n" +
                "- Carta 'El Idiota' (valor 0)\n" +
                "- Un 2 y un 3 del MISMO palo\n\n" +
                "Gana AMBOS botes automaticamente!",
                TutorialAction.ShowIdiotsArray, 1
            ),

            new TutorialStep(
                "Explotar (Bomb Out)",
                "EXPLOTAS si tu mano:\n" +
                "- Suma MAS de 23\n" +
                "- Suma MENOS de -23\n" +
                "- Suma exactamente 0 (sin Mano del Idiota)\n\n" +
                "Si explotas, pagas penalizacion al Bote de Sabacc.",
                TutorialAction.ShowBombOut, 1
            ),

            new TutorialStep(
                "Flujo de una Ronda",
                "ORDEN DE FASES:\n\n" +
                "1. Apuesta inicial\n" +
                "2. Reparto (2 cartas)\n" +
                "3. Primera apuesta\n" +
                "4. Calling\n" +
                "5. Primer Shift\n" +
                "6. Robo/Descarte\n" +
                "7. Segunda apuesta\n" +
                "8. Segundo Shift\n" +
                "9. Revelacion",
                TutorialAction.None, 0
            ),

            new TutorialStep(
                "Fin del Tutorial",
                "Ya conoces las reglas del Sabacc!\n\n" +
                "RECUERDA:\n" +
                "- Objetivo: 23, -23, o Mano del Idiota\n" +
                "- Protege cartas importantes del Shift\n" +
                "- No pases de 23 ni bajes de -23\n\n" +
                "Buena suerte!",
                TutorialAction.EndTutorial, 0
            )
        };
    }

    void ShowStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _steps.Count) // Indice invalido
            return;

        _currentStep = stepIndex; // Actualizar paso actual
        TutorialStep step = _steps[stepIndex]; // Obtener datos del paso

        if (tutorialText != null) // Actualizar texto del tutorial
            tutorialText.text = "<b>" + step.Title + "</b>\n\n" + step.Description;

        if (stepIndicator != null)
            stepIndicator.text = $"Paso {stepIndex + 1} / {_steps.Count}";

        // Cambiar camara segun el paso
        SwitchToCamera(step.CameraIndex);

        // Ejecutar accion
        StartCoroutine(ExecuteStepAction(step.Action));
    }

    void AdvanceStep()
    {
        if (_currentStep < _steps.Count - 1) // Si no es el ultimo paso
        {
            ShowStep(_currentStep + 1); // Avanzar al siguiente paso
        }
    }

    IEnumerator ExecuteStepAction(TutorialAction action)
    {
        _isAnimating = true; // Marcar que se esta animando
        _canAdvance = false; // No permitir avanzar durante la animacion

        switch (action) // Accion a ejecutar
        {
            case TutorialAction.None:
                break;

            case TutorialAction.ShowDeck:
                yield return CreateVisualDeck();
                break;

            case TutorialAction.ShowPots:
                _handPot = 0;
                _sabaccPot = 0;
                UpdatePotsUI();
                break;

            case TutorialAction.PayBets:
                yield return AnimatePayBets();
                break;

            case TutorialAction.DealCards:
                yield return AnimateDealCards();
                break;

            case TutorialAction.RevealPlayer1:
                yield return RevealPlayerCards(_player1Cards);
                UpdateHandValueUI(15, -1);
                break;

            case TutorialAction.SimulateShift:
                yield return AnimateShift();
                break;

            case TutorialAction.ProtectCard:
                yield return AnimateProtectCard();
                break;

            case TutorialAction.DrawCard:
                yield return AnimateDrawCard();
                break;

            case TutorialAction.DiscardCard:
                yield return AnimateDiscardCard();
                break;

            case TutorialAction.RevealAll:
                yield return RevealPlayerCards(_player2Cards);
                UpdateHandValueUI(15, 15);
                break;

            case TutorialAction.ShowIdiotsArray:
                yield return ShowSpecialHand(new string[] { "El_Idiota", "Monedas_2", "Monedas_3" }, "MANO DEL IDIOTA!");
                break;

            case TutorialAction.ShowBombOut:
                yield return ShowSpecialHand(new string[] { "Monedas_As", "Sables_Maestro" }, "BOMBED OUT! (15+14=29 > 23)");
                break;

            case TutorialAction.EndTutorial:
                if (pressEnterText != null)
                    pressEnterText.text = "Pulsa INTRO para volver al menu...";
                // Marcar como paso final para poder salir
                _isFinalStep = true;
                _canAdvance = true;
                _isAnimating = false;
                break;
        }

        yield return new WaitForSeconds(0.3f);
        _isAnimating = false;
        _canAdvance = true;
    }

    // ANIMACIONES

    IEnumerator CreateVisualDeck()
    {
        ClearDeckCards(); // Limpiar mazo visual previo

        if (deckArea == null || cardPrefabMap == null) yield break; // Validar referencias

        // Crear mazo visual (cartas apiladas boca abajo)
        for (int i = 0; i < 15; i++)
        {
            GameObject prefab = cardPrefabMap.GetPrefab("Monedas_1"); // Usar carta comun para el mazo
            if (prefab != null)
            {
                Vector3 pos = deckArea.position + Vector3.up * (i * 0.003f);
                GameObject card = Instantiate(prefab, pos, Quaternion.Euler(180f, 0f, 0f));
                card.transform.SetParent(deckArea);
                card.transform.localScale = Vector3.one;
                _deckCards.Add(card);
            }
            yield return new WaitForSeconds(0.03f);
        }
    }

    IEnumerator AnimatePayBets()
    {
        _player1Credits = 90;
        _player2Credits = 90;
        _handPot = 20;
        _sabaccPot = 20;

        UpdateAllUI();
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator AnimateDealCards()
    {
        ClearPlayerCards();

        string[] p1Cards = { "Monedas_8", "Sables_7" };
        string[] p2Cards = { "Frascos_10", "Bastones_5" };

        // Repartir a jugador 1
        for (int i = 0; i < 2; i++)
        {
            yield return DealCardToHand(p1Cards[i], player1HandArea, _player1Cards, i, 2);
            yield return new WaitForSeconds(0.2f);
        }

        // Repartir a jugador 2
        for (int i = 0; i < 2; i++)
        {
            yield return DealCardToHand(p2Cards[i], player2HandArea, _player2Cards, i, 2);
            yield return new WaitForSeconds(0.2f);
        }

        if (phaseText != null)
            phaseText.text = "Fase: Reparto";
    }

    IEnumerator DealCardToHand(string cardId, Transform handArea, List<GameObject> cardList, int index, int total)
    {
        if (cardPrefabMap == null || deckArea == null || handArea == null) yield break;

        GameObject prefab = cardPrefabMap.GetPrefab(cardId);
        if (prefab == null)
        {
            Debug.LogError($"[TutorialManager] No se encontro prefab para cardId: {cardId}");
            yield break;
        }

        // Calcular posicion destino
        Vector3 targetPos = CalculateCardPosition(handArea, index, total);

        // Crear carta boca abajo en el mazo
        GameObject card = Instantiate(prefab, deckArea.position, Quaternion.Euler(180f, 0f, 0f));
        card.transform.localScale = Vector3.one * 0.5f;
        cardList.Add(card);

        // Animar vuelo al destino
        yield return AnimateCardMove(card, targetPos, Quaternion.Euler(180f, 0f, 0f), 0.4f);

        // Asignar a la mano
        card.transform.SetParent(handArea);
        card.transform.localScale = Vector3.one;
    }

    IEnumerator AnimateCardMove(GameObject card, Vector3 endPos, Quaternion endRot, float duration)
    {
        Vector3 startPos = card.transform.position;
        Quaternion startRot = card.transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = t * t * (3f - 2f * t);

            // Posicion con arco
            Vector3 pos = Vector3.Lerp(startPos, endPos, smoothT);
            pos.y += Mathf.Sin(smoothT * Mathf.PI) * 0.5f;
            card.transform.position = pos;

            // Mantener rotacion boca abajo durante el vuelo
            card.transform.rotation = endRot;

            yield return null;
        }

        card.transform.position = endPos;
        card.transform.rotation = endRot;
    }

    IEnumerator RevealPlayerCards(List<GameObject> cards)
    {
        foreach (var card in cards)
        {
            if (card != null)
            {
                yield return FlipCardAnimation(card, true);
                yield return new WaitForSeconds(0.15f);
            }
        }
    }

    IEnumerator FlipCardAnimation(GameObject card, bool faceUp)
    {
        float duration = 0.25f;
        float elapsed = 0f;

        Vector3 startRot = card.transform.localEulerAngles;
        Vector3 endRot = faceUp ? Vector3.zero : new Vector3(180f, 0f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            card.transform.localEulerAngles = Vector3.Lerp(startRot, endRot, t);
            yield return null;
        }

        card.transform.localEulerAngles = endRot;
    }

    IEnumerator AnimateShift()
    {
        // Hacer temblar las cartas
        if (_player1Cards.Count > 0)
        {
            foreach (var card in _player1Cards)
            {
                if (card != null)
                    StartCoroutine(ShakeCard(card));
            }
        }

        yield return new WaitForSeconds(0.8f);

        // Cambiar visualmente la primera carta por otra
        if (_player1Cards.Count > 0 && cardPrefabMap != null && player1HandArea != null)
        {
            GameObject oldCard = _player1Cards[0];
            Vector3 oldPos = oldCard.transform.position;
            Quaternion oldRot = oldCard.transform.rotation;
            
            // Destruir la carta vieja
            Destroy(oldCard);
            
            // Crear la carta nueva (un 4 en vez del 8)
            GameObject newPrefab = cardPrefabMap.GetPrefab("Monedas_4");
            if (newPrefab != null)
            {
                GameObject newCard = Instantiate(newPrefab, oldPos, oldRot);
                newCard.transform.SetParent(player1HandArea);
                newCard.transform.localScale = Vector3.one;
                _player1Cards[0] = newCard;
            }
        }

        // Actualizar texto para indicar que algo cambio (8+7=15 -> 4+7=11)
        UpdateHandValueUI(11, -1);
    }

    IEnumerator ShakeCard(GameObject card)
    {
        Vector3 originalPos = card.transform.position;
        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float intensity = 1f - (elapsed / duration);
            Vector3 shake = new Vector3(
                Random.Range(-0.02f, 0.02f) * intensity,
                Random.Range(-0.02f, 0.02f) * intensity,
                0
            );
            card.transform.position = originalPos + shake;
            yield return null;
        }

        card.transform.position = originalPos;
    }

    IEnumerator AnimateProtectCard()
    {
        if (_player1Cards.Count == 0 || player1InterferenceField == null) yield break;

        GameObject cardToProtect = _player1Cards[0];
        // Posicion del campo de interferencia + offset hacia arriba (0.5 unidades)
        Vector3 targetPos = player1InterferenceField.position + Vector3.up * 0.4f;

        // Mover carta al campo de interferencia
        yield return AnimateCardMove(cardToProtect, targetPos, Quaternion.identity, 0.4f);

        // Voltear boca arriba (visible para todos)
        yield return FlipCardAnimation(cardToProtect, true);

        cardToProtect.transform.SetParent(player1InterferenceField);
        _protectedCards.Add(cardToProtect);

        // Reorganizar mano restante
        yield return ReorganizeCards(_player1Cards, player1HandArea, 1);
    }

    IEnumerator AnimateDrawCard()
    {
        string newCardId = "Monedas_3";
        int newIndex = _player1Cards.Count;

        yield return DealCardToHand(newCardId, player1HandArea, _player1Cards, newIndex, newIndex + 1);

        // Reorganizar
        yield return ReorganizeCards(_player1Cards, player1HandArea, 0);

        // Revelar la nueva carta
        if (_player1Cards.Count > 0)
        {
            var lastCard = _player1Cards[_player1Cards.Count - 1];
            yield return FlipCardAnimation(lastCard, true);
        }

        UpdateHandValueUI(18, -1);
    }

    IEnumerator AnimateDiscardCard()
    {
        if (_player1Cards.Count < 2 || discardArea == null) yield break;

        // Descartar la ultima carta
        GameObject cardToDiscard = _player1Cards[_player1Cards.Count - 1];
        _player1Cards.Remove(cardToDiscard);

        // Mover al descarte
        yield return AnimateCardMove(cardToDiscard, discardArea.position, Quaternion.identity, 0.3f);

        cardToDiscard.transform.SetParent(discardArea);
        _discardCards.Add(cardToDiscard);

        // Reorganizar mano
        yield return ReorganizeCards(_player1Cards, player1HandArea, 0);

        UpdateHandValueUI(15, -1);
    }

    IEnumerator ShowSpecialHand(string[] cardIds, string message)
    {
        ClearPlayerCards();

        for (int i = 0; i < cardIds.Length; i++)
        {
            yield return DealCardToHand(cardIds[i], player1HandArea, _player1Cards, i, cardIds.Length);
            yield return new WaitForSeconds(0.2f);
        }

        // Revelar todas
        yield return RevealPlayerCards(_player1Cards);

        // Mostrar mensaje especial
        if (player1HandValueText != null)
            player1HandValueText.text = message;
    }

    IEnumerator ReorganizeCards(List<GameObject> cards, Transform handArea, int startIndex)
    {
        int total = cards.Count;
        for (int i = startIndex; i < total; i++)
        {
            if (cards[i] != null)
            {
                Vector3 targetPos = CalculateCardPosition(handArea, i, total);
                StartCoroutine(MoveCardSmooth(cards[i], targetPos));
            }
        }
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator MoveCardSmooth(GameObject card, Vector3 targetPos)
    {
        float duration = 0.2f; // Duracion del movimiento
        float elapsed = 0f; // Tiempo transcurrido
        Vector3 startPos = card.transform.position; // Posicion inicial

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // Incrementar tiempo
            card.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration); // Interpolar posicion
            yield return null; // Esperar al siguiente frame
        }
        card.transform.position = targetPos; // Asegurar posicion final
    }

    Vector3 CalculateCardPosition(Transform handArea, int index, int total)
    {
        Vector3 center = handArea.position; // Centro del area de la mano
        Vector3 right = handArea.right; // Direccion derecha local
        float totalWidth = (total - 1) * cardSpacing; // Ancho total ocupado por las cartas
        Vector3 startPos = center + right * (totalWidth / 2f); // Posicion inicial (izquierda)
        return startPos - right * (index * cardSpacing); // Posicion de la carta en base al indice
    }

    // UI 

    void UpdateAllUI()
    {
        UpdatePotsUI(); // Actualizar botes
        UpdateCreditsUI(); // Actualizar creditos
    }

    void UpdatePotsUI()
    {
        if (handPotText != null) handPotText.text = $"Bote Mano: {_handPot}"; // Actualizar bote de mano
        if (sabaccPotText != null) sabaccPotText.text = $"Bote Sabacc: {_sabaccPot}"; // Actualizar bote de sabacc
    }

    void UpdateCreditsUI()
    {
        if (player1CreditsText != null) player1CreditsText.text = $"Tu: {_player1Credits}"; // Actualizar creditos jugador 1
        if (player2CreditsText != null) player2CreditsText.text = $"Oponente: {_player2Credits}"; // Actualizar creditos jugador 2
    }

    void UpdateHandValueUI(int p1Value, int p2Value)
    {
        if (player1HandValueText != null) // Actualizar valor de la mano del jugador 1
        {
            if (p1Value >= 0) // Si es valido mostrar valor
                player1HandValueText.text = $"Tu mano: {p1Value}";
            else
                player1HandValueText.text = "";
        }

        if (player2HandValueText != null)
        {
            if (p2Value >= 0)
                player2HandValueText.text = $"Oponente: {p2Value}";
            else
                player2HandValueText.text = "Oponente: ???";
        }
    }

    // Linpieza 

    void ClearPlayerCards()
    {
        foreach (var c in _player1Cards) if (c != null) Destroy(c); // Destruir cartas del jugador 1
        foreach (var c in _player2Cards) if (c != null) Destroy(c); // Destruir cartas del jugador 2
        foreach (var c in _protectedCards) if (c != null) Destroy(c); // Destruir cartas protegidas
        _player1Cards.Clear(); // Limpiar listas
        _player2Cards.Clear();
        _protectedCards.Clear();
    }

    void ClearDeckCards()
    {
        foreach (var c in _deckCards) // Destruir cartas del mazo visual
            if (c != null)
                Destroy(c); 
        _deckCards.Clear();
    }

    
    void ExitTutorial()
    {
        //SceneManager.LoadScene(exitSceneName); // Volver a la escena de menu
        AudioManager.Instance.LoadSceneWithMusicFade("Inicio");
    }
    
    /*
    void ExitTutorial()
    {
        // Fade out la música actual antes de salir
        AudioManager.Instance.FadeOutMusic(0.8f);
        StartCoroutine(LoadExitSceneAfterFade());
    }

    IEnumerator LoadExitSceneAfterFade()
    {
        yield return new WaitForSeconds(0.8f);
        SceneManager.LoadScene(exitSceneName); // Volver a la escena de menu
    }
    */
}

// CLASES AUXILIARES

public enum TutorialAction // Acciones posibles en un paso del tutorial
{
    None,
    ShowDeck,
    ShowPots,
    PayBets,
    DealCards,
    RevealPlayer1,
    SimulateShift,
    ProtectCard,
    DrawCard,
    DiscardCard,
    RevealAll,
    ShowIdiotsArray,
    ShowBombOut,
    EndTutorial
}

public class TutorialStep // Representa un paso del tutorial
{
    public string Title; // Titulo del paso
    public string Description; // Descripcion del paso
    public TutorialAction Action; // Accion a ejecutar en este paso
    public int CameraIndex; // 0=main, 1=player1, 2=player2

    // Constructor
    public TutorialStep(string title, string description, TutorialAction action, int cameraIndex)
    {
        Title = title;
        Description = description;
        Action = action;
        CameraIndex = cameraIndex;
    }
}
