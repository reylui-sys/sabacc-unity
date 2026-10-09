# ADR 0004 — Un único camino para cambiar el estado: eventos + reducer

- **Estado:** aceptada
- **Fecha:** 2026-10-09
- **Amplía:** [ADR 0003](0003-comandos-validados-por-el-master.md)

## Contexto

Tras el ADR 0003 el Master validaba las acciones, pero el estado seguía cambiando por muchos caminos distintos:

- Cada RPC cambiaba el estado a su manera. El Master, a veces, ya lo había cambiado antes de enviarlo (`if (!PhotonNetwork.IsMasterClient) ...`).
- Algunas reglas solo se aplicaban en el Master: las penalizaciones por explotar y por CALL fallido. Los clientes mostraban otros créditos hasta la ronda siguiente.
- El controlador de red tenía **76 asignaciones directas** al estado de la partida: botes, créditos, fase, turno…
- No había forma de demostrar que Master y clientes veían lo mismo, salvo jugando.

Era la causa común de los bugs de desincronización: el bote duplicado, los valores incorrectos tras un CALL, las penalizaciones invisibles…

## Decisión

Se separan tres responsabilidades, todas en el núcleo (`Sabacc.Core`):

| Pieza | Responsabilidad | Modifica el estado |
|---|---|---|
| `GameEngine` | **Decide** qué pasa (comandos, inicio de ronda, shifting, avance de apuestas, liquidación) y lo devuelve como **eventos** | No |
| `EventCodec` | Convierte una lista de eventos en `byte[]` y viceversa, para la red | No |
| `GameReducer` | **Aplica** un evento al estado | **Sí, y es el único** |

El flujo en red queda así:

```
cliente ──comando──► Master: GameEngine.Handle ──eventos──► RPC_ApplyEvents (a todos, Master incluido)
                                                              └─► GameReducer.Apply + Present (visual)
```

- El Master **no aplica nada por su cuenta**: recibe sus propios eventos por el mismo RPC que los clientes.
- `NetworkGameController` queda como **presentación** (animaciones, textos, sonidos) más el disparo del flujo (temporizadores). Ya no contiene ninguna asignación al estado de la partida.
- **Información oculta:** solo el Master conoce el mazo (`GameState.DeckIsKnown`). El reducer de los clientes crea las cartas a partir de su ID. Los eventos nunca llevan el orden del mazo.
- **Orden de aplicación:** en Photon, un RPC a "All" se ejecuta en el Master al instante. Si el Master decide algo mientras está aplicando un lote, ese envío se aplaza hasta terminarlo. Así aplica los eventos exactamente en el mismo orden en que llegan a los clientes.

## Cómo se verifica

`ConvergenceTests` simula **300 partidas completas** con 2, 3 o 4 jugadores. Un Master y dos clientes reciben los eventos codificados en bytes, y bots aleatorios eligen acciones válidas. Tras cada mensaje se comprueba que:

1. el estado público de los tres es **idéntico**;
2. el **dinero total** (créditos + botes) no cambia;
3. en el Master, las **76 cartas** existen exactamente una vez mientras hay una ronda en juego.

El propio test comprueba también que la simulación recorre todos los tipos de evento y todos los finales de ronda. Si se reintroduce a propósito el bug del bote duplicado, el test lo detecta en el primer reparto de botes.

## Alternativas consideradas

- **Sincronizar el estado completo (snapshot) tras cada acción**: es más simple de razonar, pero envía las manos de todos a todos (rompe la información oculta) y no dice *qué* ha pasado, que es justo lo que necesitan las animaciones.
- **Event sourcing con historial persistente**: no hace falta guardar ni reproducir partidas. Los eventos son un mecanismo de sincronización, no una base de datos.
- **Un RPC por tipo de evento** (como antes): cada uno con su propio código de aplicación, que es justo lo que generaba las divergencias. Un único `RPC_ApplyEvents` con un único reducer lo hace imposible por construcción.

## Consecuencias

- ✅ La desincronización entre Master y clientes es imposible por construcción, y está comprobada en cientos de partidas.
- ✅ Se corrigen tres bugs de reglas al moverlas al motor:
  - la penalización de CALL se cobraba aunque quien llamó ganase con la mejor mano;
  - el bote de mano desaparecía tras una ronda en la que todos explotaban, aunque el mensaje decía que persistía;
  - el ganador por créditos recibía su apuesta inicial dos veces en los clientes.
- ✅ Las reglas se pueden probar sin Unity ni red. Hay tests del motor, del reducer y del codec.
- ✅ El controlador pasa de unas 4.100 a unas 2.850 líneas.
- ⚠️ Los eventos todavía llevan las manos de todos al empezar la ronda (`RoundStarted`), como ya pasaba antes. Para ocultarlas de verdad, el Master tendría que enviar a cada jugador solo su mano y revelar el resto en la liquidación.
- ⚠️ El ritmo de la partida sigue dependiendo de temporizadores del Master (`Invoke`, `WaitForSeconds`) ajustados a la duración de sus animaciones. Eso lo resuelve el paso siguiente: una máquina de estados de fases con barreras de presentación.
- ⚠️ La reconexión y la migración del Master siguen sin soporte. Con este modelo serían viables enviando un *snapshot* del estado público y retomando el flujo de eventos.
