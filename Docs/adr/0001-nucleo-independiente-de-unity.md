# ADR 0001 — Núcleo de reglas independiente de Unity

- **Estado:** aceptada
- **Fecha:** 2026-10-09

## Contexto

Las reglas del Sabacc (`GameState`, `GameLogic`, `Hand`, `Deck`…) estaban en el mismo assembly que todo lo demás (`Assembly-CSharp`) y usaban `Debug.Log`. Nada impedía que acabaran dependiendo de escenas, de la UI o de Photon, y la única forma de probarlas era jugar una partida en red.

## Decisión

- Las reglas viven en `Assets/Scripts/Core` con su propio assembly, **`Sabacc.Core`**, que tiene activado *No Engine References*.
- El log del núcleo pasa por `CoreLog` (delegados configurables). `UnityCoreLogBridge` los conecta a `Debug.Log` al arrancar.
- Hay tests EditMode del núcleo en `Assets/Tests/EditMode` (assembly `Sabacc.Core.Tests`).

## Alternativas consideradas

- **Solo disciplina ("no uséis Unity en Model/")**: sin coste inicial, pero nada lo garantiza. Se descartó porque una regla que no comprueba el compilador acaba rompiéndose.
- **Interfaz `ILogger` inyectada**: más pura, pero obliga a pasar el logger por todos los constructores. Para un log de depuración, un punto estático configurable es suficiente.

## Consecuencias

- ✅ El compilador impide que el núcleo dependa de Unity o Photon.
- ✅ Los tests se ejecutan en milisegundos, sin escena ni red. Ya destaparon un bug real en el shifting.
- ✅ El núcleo se puede reutilizar para IA, modo offline o tutorial.
- ⚠️ `CoreLog` es estado global: un test que lo sustituya debe restaurarlo (`[TearDown]`).
- ⚠️ Los tipos siguen en el namespace global para no tocar todos los scripts a la vez. Moverlos a `Sabacc.Core` queda pendiente.
