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
│   │   └── Commands/      ← comandos de jugador y su validador
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

### Tests

82 tests EditMode cubren el validador de comandos (qué acciones son legales en cada fase), las manos (bomb out, Sabacc Puro, Mano del Idiota), el mazo y los IDs de carta, la mejor mano y los desempates, los turnos, las apuestas, el shifting y los asientos. Los de shifting destaparon un bug real: con las cartas especiales duplicadas, una misma carta podía quedar a la vez en una mano y en el mazo.

Para ejecutarlos: *Window → General → Test Runner → EditMode → Run All*.

### Decisiones de arquitectura

Cada cambio importante está documentado como ADR, con el contexto, la decisión, las alternativas consideradas y las consecuencias:

- [ADR 0001 — Núcleo de reglas independiente de Unity](Docs/adr/0001-nucleo-independiente-de-unity.md)
- [ADR 0002 — Identidad de jugadores por PlayerId](Docs/adr/0002-identidad-de-jugadores-por-playerid.md)
- [ADR 0003 — Comandos validados por el Master](Docs/adr/0003-comandos-validados-por-el-master.md)

### Hoja de ruta

El objetivo es un **Master autoritativo basado en comandos y eventos**:

1. ✅ Núcleo independiente de Unity, con tests.
2. ✅ Identidad de jugadores por `PlayerId`.
3. ✅ Los clientes envían comandos y el Master los valida.
4. ⬜ El estado se modifica en un único *reducer* que aplican todos a partir de los mismos eventos.
5. ⬜ Las fases de la ronda como máquina de estados explícita (patrón State), sin temporizadores.
6. ⬜ La presentación consume eventos desde una cola de animaciones.

## Tecnologías

Unity 2022.3 · C# 9 · Photon PUN 2 · TextMesh Pro · Unity Test Framework (NUnit)
