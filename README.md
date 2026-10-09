# Sabacc

Juego de cartas **Sabacc** (el del universo Star Wars) en 3D y multijugador online para 2–4 jugadores, hecho con **Unity** y **Photon PUN 2**.

Es un proyecto universitario de grupo (L1-G1). Este repositorio contiene el estado actual del proyecto e incluye una refactorización de la arquitectura del núcleo de reglas, que se describe más abajo.

## Características

- Partidas online de 2 a 4 jugadores con lobby, salas y selección de avatar.
- Reglas de Sabacc clásico: mazo de 76 cartas (60 normales en 4 palos + 16 especiales con valores negativos), bote de mano y bote de Sabacc.
- Ronda completa por fases: apuesta inicial, reparto, primera apuesta, *calling*, primer *shift*, robo/descarte, segunda apuesta, segundo *shift* y revelación.
- **Shift**: las cartas pueden cambiar aleatoriamente durante la partida.
- **Campo de interferencia**: protege hasta 2 cartas del shift, a cambio de que todos las vean.
- Manos especiales: **Sabacc Puro** (±23) y **Mano del Idiota** (Idiota + 2 + 3).
- Tutorial guiado, menús, ajustes de audio y créditos.

## Requisitos

- **Unity 2022.3.62f1** (LTS)
- Una **App ID de Photon PUN** (gratuita en [photonengine.com](https://www.photonengine.com/))

## Cómo ejecutarlo

1. Abre la carpeta del repositorio con Unity Hub (*Add project from disk*).
2. Pon tu App ID de Photon en `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` (campo *App Id PUN*). En este repositorio está vacía a propósito.
3. Abre la escena `Assets/Scenes/Inicio.unity` y pulsa *Play*.
4. Para probar el multijugador en un solo ordenador, genera varias builds desde el menú **Build → Build 2-4 Jugadores Windows** y abre dos o más instancias.

## Arquitectura

```
Assets/
├── Scripts/
│   ├── Core/              ← reglas del juego en C# puro (asmdef Sabacc.Core, sin UnityEngine)
│   │   ├── Commands/      ← comandos de jugador y su validador
│   │   ├── Engine/        ← GameEngine decide qué pasa; GameFlow y FlowCoordinator, el orden y el ritmo
│   │   └── Events/        ← eventos, su codificación para la red y el reducer
│   ├── Controller/        ← flujo de partida en red, animaciones, audio, tutorial
│   ├── View/              ← vista de cartas y mapa de prefabs
│   ├── Infrastructure/    ← puentes entre el núcleo y Unity (p. ej. log)
│   └── *.cs               ← lobby, conexión, spawn de jugadores, menús
├── Tests/EditMode/        ← tests unitarios del núcleo (NUnit, Unity Test Framework)
└── Editor/                ← utilidades de editor y scripts de build
Docs/adr/                  ← decisiones de arquitectura (ADR)
```

### Núcleo independiente del motor

Las reglas (`GameState`, `GameLogic`, `Hand`, `Deck`, cartas…) viven en el assembly **`Sabacc.Core`**, que tiene activado *No Engine References*. El compilador impide que las reglas dependan de Unity o de Photon, y eso las hace:

- **testeables** en milisegundos, sin abrir escenas ni conectarse a la red;
- **reutilizables** en el futuro desde el tutorial, una IA o un modo offline;
- **independientes** de la presentación: los logs pasan por `CoreLog`, y Unity los conecta con `Debug.Log` al arrancar (`UnityCoreLogBridge`).

### Identidad de jugadores

Cada jugador tiene un `PlayerId` estable (su ActorNumber de Photon) y un `Seat` en la mesa. Su posición se busca con `GameState.IndexOf(id)` en lugar de calcularse como `ActorNumber - 1`, una fórmula que se rompía si alguien entraba y salía de la sala antes de empezar.

### Comandos validados por el Master

Los jugadores no cambian la partida directamente: envían **comandos** (`Draw`, `Bet`, `Discard`…) al Master.

1. El cliente valida el comando en local con `CommandValidator`, para dar feedback inmediato, y bloquea sus controles.
2. El Master lo vuelve a validar con **el mismo código**, identificando al jugador por el remitente real del mensaje.
3. Si es válido, el Master lo ejecuta y emite el evento resultante a todos.
4. Si no lo es, responde con el motivo solo a quien lo envió.

Los eventos solo se aceptan si vienen del Master. Así se acaban el doble clic, los turnos saltados y las reglas duplicadas por la UI.

### Un único camino para cambiar el estado

```
cliente ──comando──► Master: GameEngine ──eventos──► todos (Master incluido): GameReducer.Apply + animación
```

- **`GameEngine`** decide qué pasa (comandos, inicio de ronda, shifting, apuestas, liquidación) y lo devuelve como **eventos**, sin modificar nada.
- **`GameReducer.Apply`** es la **única** función que modifica el estado de la partida.
- Master y clientes aplican los mismos eventos con el mismo reducer, así que no pueden ver partidas distintas. El Master no aplica nada por su cuenta: recibe sus propios eventos por el mismo RPC que los demás.
- El controlador de red solo presenta (animaciones, textos, sonidos). Antes tenía 76 asignaciones directas al estado; ahora ninguna.
- Solo el Master conoce el orden del mazo: es información oculta y no viaja por la red.

### Flujo de fases y ritmo, sin temporizadores

El orden de las fases es una **función de transición pura**, `GameFlow`:

```
Next(estado, lote)   → qué paso automático toca después (repartir, shift, liquidar, siguiente ronda…)
Plan(paso, estado)   → los eventos de ese paso, o ninguno si ya no procede (guardas)
```

El ritmo lo marca lo que tarda en verse cada cosa en todos los equipos:

- El Master aplica cada lote a su **autoridad** (la única copia con el mazo) y lo envía numerado.
- Cada equipo, el Master incluido, lo pone en una **cola de presentación**. La cola aplica el reducer a su **vista** y espera a que termine cada animación antes de pasar a la siguiente.
- Cada equipo confirma el lote al terminar de verlo. El Master solo da el siguiente paso cuando todos los que siguen en la sala lo han confirmado: una **barrera** (`FlowCoordinator`). Si alguien tarda más de 30 s, se sigue sin él y su cola se pone al día después.

Antes, cada fase lanzaba la siguiente con `Invoke` y esperas calibradas a las animaciones del Master. Ahora el controlador no tiene ningún temporizador de flujo.

### Tests

163 tests EditMode. Los más importantes son dos simulaciones:

- **Convergencia**: cientos de partidas completas, con y sin abandonos, entre un Master y dos clientes que solo reciben los eventos codificados en bytes. Tras cada mensaje comprueba que los tres ven exactamente lo mismo, que no se crea ni se destruye dinero y que ninguna carta se duplica.
- **Protocolo de red**: simula en el tiempo las colas de presentación, las confirmaciones y la barrera. Usa animaciones de duración aleatoria, equipos que se atascan y jugadores que actúan cuando *su* vista dice que les toca. Comprueba que cada equipo acaba viendo exactamente lo que envió el Master, que el Master nunca se adelanta a nadie salvo cuando vence el plazo y que ninguna partida se queda parada.
 Los demás cubren el motor, el reducer, la codificación de eventos, el validador de comandos, las manos (bomb out, Sabacc Puro, Mano del Idiota), el mazo y los IDs de carta, la mejor mano y los desempates, los turnos, las apuestas, el shifting y los asientos. Los de shifting destaparon un bug real: con las cartas especiales duplicadas, una misma carta podía quedar a la vez en una mano y en el mazo.

Para ejecutarlos: *Window → General → Test Runner → EditMode → Run All*.

### Decisiones de arquitectura

Cada cambio importante está documentado como ADR, con el contexto, la decisión, las alternativas consideradas y las consecuencias:

- [ADR 0001 — Núcleo de reglas independiente de Unity](Docs/adr/0001-nucleo-independiente-de-unity.md)
- [ADR 0002 — Identidad de jugadores por PlayerId](Docs/adr/0002-identidad-de-jugadores-por-playerid.md)
- [ADR 0003 — Comandos validados por el Master](Docs/adr/0003-comandos-validados-por-el-master.md)
- [ADR 0004 — Un único camino para cambiar el estado: eventos + reducer](Docs/adr/0004-un-unico-camino-para-cambiar-el-estado.md)
- [ADR 0005 — Flujo de fases explícito y barreras de presentación](Docs/adr/0005-flujo-de-fases-y-barreras-de-presentacion.md)

### Hoja de ruta

El objetivo es un **Master autoritativo basado en comandos y eventos**:

1. ✅ Núcleo independiente de Unity, con tests.
2. ✅ Identidad de jugadores por `PlayerId`.
3. ✅ Los clientes envían comandos y el Master los valida.
4. ✅ El estado se modifica en un único *reducer* que aplican todos a partir de los mismos eventos.
5. ✅ Las fases de la ronda como función de transición explícita (`GameFlow`), sin temporizadores.
6. ✅ La presentación consume los eventos desde una cola, y el Master avanza con barreras de presentación.
7. ⬜ Información oculta de verdad: cada jugador recibe solo su mano, y las demás se revelan al final.
8. ⬜ Temporizador de turno, como un paso más de `GameFlow`.
9. ⬜ Reconexión y migración del Master a partir de un *snapshot* de la autoridad.

## Tecnologías

Unity 2022.3 · C# 9 · Photon PUN 2 · TextMesh Pro · Unity Test Framework (NUnit)
