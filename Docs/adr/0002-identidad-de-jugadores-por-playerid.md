# ADR 0002 — Identidad de jugadores por `PlayerId`, no por índice

- **Estado:** aceptada
- **Fecha:** 2026-10-09

## Contexto

El asiento de cada jugador (su cámara, su área de mano, su turno) se calculaba como `ActorNumber - 1`. Photon nunca reutiliza los ActorNumbers. Si alguien entraba y salía de la sala antes de empezar, quedaban huecos (1, 3…): el segundo jugador obtenía el índice 2 en una lista de 2, nunca le llegaba el turno y la partida se quedaba esperando. El mismo cálculo estaba repetido en `NetworkGameController`, `PlayerSpawner` y la gestión de jugadores que abandonan.

## Decisión

- Cada jugador tiene un **`PlayerId`** estable (en red, su ActorNumber) y un **`Seat`** (id + nombre).
- El Master crea los asientos una sola vez, en el orden de `PhotonNetwork.PlayerList`, y envía nombres y ActorNumbers a todos en `RPC_GameInitialized`.
- Para obtener el índice de un jugador se busca por id: `GameState.IndexOf(id)`. Nunca se calcula.
- La sala se cierra (`IsOpen = false`, `IsVisible = false`) al empezar la partida, para que nadie entre sin asiento.

## Alternativas consideradas

- **`Array.IndexOf(PlayerList, jugador)` en cada sitio**: arregla el caso habitual, pero el índice cambia si alguien se va a mitad de partida. Se usa solo para el spawn, antes de que exista el `GameState`.
- **Propiedad de sala con el asiento de cada jugador**: más robusto para reconexiones, pero añade latencia y condiciones de carrera al arrancar. Queda como opción si se implementa la reconexión.

## Consecuencias

- ✅ El bug desaparece, y queda un test de regresión con ActorNumbers con huecos.
- ✅ Hay un único punto de verdad para "quién es quién".
- ⚠️ `PlayerSpawner` todavía calcula su asiento por su cuenta antes de que exista el `GameState`. Si alguien se va justo durante la carga, podría descuadrarse. Se resolverá cuando el spawn espere a la inicialización.
