# ADR 0005 — Flujo de fases explícito y barreras de presentación

- **Estado:** aceptada
- **Fecha:** 2026-10-09
- **Amplía:** [ADR 0004](0004-un-unico-camino-para-cambiar-el-estado.md)

## Contexto

Tras el ADR 0004 todos aplicaban los mismos eventos, pero **el orden de las fases y el ritmo** seguían repartidos por el controlador de red:

- Cada fase lanzaba la siguiente desde el final de una animación del Master. Por ejemplo, `ShowDeckAndDeal` hacía `Invoke(StartFirstBettingPhase, 1f)`, `AnimateShifting` emitía la fase de robo al terminar, y la revelación liquidaba la ronda desde el *callback* de su animación.
- Las esperas (`WaitForSeconds(1f)`, `Invoke(..., 1.5f)`, `DelayedPrepareNextRound(8f)`…) estaban **calibradas a la duración de las animaciones del Master**. Si un cliente iba más lento, recibía la fase siguiente con la anterior aún a medias, y dos corrutinas podían tocar las mismas cartas a la vez.
- Para saber "qué viene después del reparto" había que seguir una cadena de RPC, `Invoke` y corrutinas por un fichero de casi 3.000 líneas, y nada de eso tenía tests.
- Los abandonos tenían casos sin cubrir. Si quien tenía el turno de robo abandonaba, la partida podía quedarse esperándole. Quien se plantaba y luego se iba seguía contando como plantado. Y quien abandonaba **volvía a recibir cartas** en la ronda siguiente.

## Decisión

### 1. El flujo de la partida es una función de transición pura (`GameFlow`)

```
Next(estado, lote)          → qué paso automático toca tras ese lote de eventos
Plan(paso, estado, reglas)  → los eventos de ese paso, o ninguno si ya no procede
```

Toda la tabla de transiciones se lee de un vistazo en `GameFlow.Next`:

- `RoundStarted` → empieza la primera apuesta;
- una acción de apuesta → `BettingStep`, que pasa el turno o la fase, o termina la ronda si solo queda uno;
- `PhaseChanged(FirstShift)` → shift;
- `CardsShifted(First)` → empieza el robo;
- todos plantados → segunda apuesta;
- `Reveal` → liquidar;
- `RoundEnded` → siguiente ronda, o reinicio tras una victoria definitiva;
- un abandono → `AfterPlayerLeft`.

Cada paso tiene una **guarda**. Si cuando llega su momento ya no procede, porque la ronda terminó entre medias o porque otro paso ya movió el turno, no produce nada. Así un paso que llega tarde nunca actúa dos veces.

### 2. El Master separa autoridad y vista

- **Autoridad** (`_authority`, solo en el Master): aplica cada lote en cuanto lo decide, conoce el mazo y es contra la que se validan los comandos.
- **Vista** (`gameState`, en todos, Master incluido): avanza al ritmo de la presentación. El Master es un cliente más de sí mismo.

### 3. Cola de presentación en cada equipo

`RPC_ApplyEvents(seq, lote)` solo encola. Una corrutina permanente (`PresentationLoop`) saca los lotes en orden. Para cada evento aplica el reducer a la vista y **espera a que termine su animación** antes de pasar al siguiente. Los presentadores (`Present*`) devuelven `IEnumerator` en lugar de lanzar corrutinas sueltas.

Una animación que se cuelga o lanza una excepción tiene un límite (`presenterTimeout`). La cola se registra el fallo y sigue: nunca se para.

### 4. Barrera de presentación con plazo (`FlowCoordinator`)

```
Master: decide ─► autoridad ─► lote N ─► todos: cola ─► reducer + animación ─► Ack(N)
                                                                                 │
Master: cuando TODOS los que siguen en la sala han confirmado N ◄────────────────┘
        ─► siguiente paso de GameFlow ─► lote N+1 …
```

- Cada lote lleva un número. Las confirmaciones de lotes anteriores no cuentan.
- La inicialización es el lote 0: la primera ronda se reparte cuando todos han creado su vista.
- Quien abandona la sala deja de contar para la barrera.
- Si alguien no confirma en `presentationTimeout` segundos (30 por defecto), el Master sigue sin él. Su cola se pondrá al día en orden más tarde.
- `FlowCoordinator` no sabe nada de Unity ni de Photon: recibe los tiempos y la lista de jugadores. Eso permite probar el protocolo completo en una simulación.

### 5. Abandonos

- `PlayerLeft` marca al jugador (`HasLeft`). `PlanRoundStart` no vuelve a repartirle cartas.
- `AfterPlayerLeft` mueve el turno si lo tenía él: en apuestas al siguiente, en robo al siguiente que no se haya plantado o a la segunda apuesta. Si solo queda un jugador, ese jugador gana el bote.
- "¿Se han plantado todos?" se calcula jugador a jugador (`HasStood`) y no con un contador.
- El validador rechaza comandos de quien ya se plantó y del último jugador que queda. Este último caso evitaba una ronda sin nadie, porque podía retirarse antes de que el Master cerrase la ronda.
- Si se va el **Master**, todos vuelven al menú con un aviso. Antes la partida se quedaba parada para siempre: la autoridad y el mazo solo existían en él.

## Cómo se verifica

- **`GameFlowTests`**: la tabla de transiciones y cada guarda, incluidos los casos reales que la motivaron.
- **`ConvergenceTests`**: 300 partidas conducidas por `FlowCoordinator`, más 200 con abandonos aleatorios. Los bots a veces intentan actuar **antes** de que se dé el paso pendiente, que es la carrera real de un jugador que pulsa mientras los demás aún ven la animación.
- **`NetworkProtocolTests`**: simula el protocolo en el tiempo, con duraciones de animación aleatorias por equipo, equipos que se atascan más que el plazo y abandonos. Los jugadores actúan cuando *su vista* dice que les toca. Comprueba que:
  - cada equipo, al terminar de ver el lote N, tiene **exactamente** el estado público que tenía el Master al enviarlo;
  - el Master **nunca** avanza sin que todos hayan visto el último lote, salvo cuando vence el plazo;
  - ninguna partida se queda parada.

Para comprobar que los tests sirven, se reintrodujeron dos fallos a propósito y los tests detectaron los dos:

- aceptar confirmaciones de lotes anteriores (la barrera se abriría antes de tiempo);
- quitar la guarda del paso de apuestas tardío (saltaría el turno de un jugador y la partida se atascaría).

## Alternativas consideradas

- **Patrón State clásico (una clase por fase con `Enter`/`Exit`/`Handle`)**: era lo previsto en la hoja de ruta. Aquí las transiciones las disparan los **eventos**, y las fases no tienen comportamiento propio que encapsular, porque las reglas de cada acción ya viven en `CommandValidator` y `GameEngine`. Nueve clases casi vacías habrían repartido la tabla en nueve ficheros. Una función de transición pura la deja en uno y se prueba con una línea por transición. Si algún día una fase necesita estado propio (p. ej. un temporizador de turno), se puede extraer a una clase sin cambiar `Next`.
- **Mantener los temporizadores y alargarlos**: oculta el problema en equipos rápidos y lo empeora en los lentos. El ritmo seguiría sin depender de lo que de verdad ve cada jugador.
- **Esperar solo al Master (sin confirmaciones)**: más simple y sin RPC extra, pero vuelve a suponer que todos van a su ritmo.
- **Barrera sin plazo**: un solo equipo colgado pararía la partida de todos.

## Consecuencias

- ✅ El controlador ya no tiene ningún `Invoke` ni ningún paso de flujo en corrutinas del Master. Se eliminan:
  - `TriggerShift`, `StartFirstBettingPhase`, `ProcessNextBettingTurn`, `HandleCallSequence` y `AdvanceAfterDrawing`;
  - `RestartGame` y los tres `Delayed*`;
  - `PrepareNextRound` y `RPC_CleanupRound`.
- ✅ El ritmo lo marca lo que tarda en verse cada cosa en **todos** los equipos. Las animaciones de dos lotes ya no se pisan.
- ✅ Se corrigen cinco problemas con los abandonos y uno de carrera (robar tras plantarse mientras se anima la última acción).
- ✅ 163 tests, incluida la simulación del protocolo de red.
- ⚠️ Cada lote añade una confirmación por jugador: hasta 4 mensajes pequeños por lote, sin importancia en una partida por turnos.
- ⚠️ La partida espera al equipo más lento, hasta el plazo. Con un equipo muy lento, todos esperan.
- ⚠️ Sigue sin haber temporizador de turno. Si el jugador en turno no hace nada, la partida espera. Con `GameFlow` se añadiría como un paso más (`TurnTimeout`).
- ⚠️ La migración del Master y la reconexión siguen sin soporte. Ahora están acotadas: bastaría enviar un *snapshot* de la autoridad y la cola de pasos de `FlowCoordinator`.
