# ADR 0003 — Acciones de jugador como comandos validados por el Master

- **Estado:** aceptada
- **Fecha:** 2026-10-09

## Contexto

Cada botón tenía su propia validación en el cliente y luego enviaba un RPC. Unas acciones iban **a todos directamente** (apostar, pasar, igualar, CALL, retirarse) y otras al Master, que **no validaba nada** (robar, plantarse, descartar, proteger). Esto provocaba:

- **Doble clic:** los botones seguían activos hasta que el Master pasaba el turno (≈1 s). Dos clics en *Plantarse* hacían que el siguiente jugador perdiese su turno, y dos en *Pasar* lanzaban dos avances de turno.
- **Reglas duplicadas:** la misma comprobación ("¿puedo descartar?") estaba escrita en varios sitios, a veces con criterios distintos.
- **Estado de turno fuera del modelo:** la apuesta más alta, quién hizo CALL y si alguien ya había descartado eran campos privados del controlador. El Master ni siquiera conocía el "ya he descartado" de los demás.
- **Confianza ciega:** cualquier cliente podía emitir un evento con el índice de otro jugador.

## Decisión

1. Las acciones son **comandos** (`GameCommand`: tipo + jugador + un argumento), definidos en el núcleo.
2. Un **`CommandValidator`** puro decide si un comando es legal en un `GameState`. Lo usan:
   - el **cliente**, antes de enviar, para dar feedback inmediato;
   - el **Master**, al recibir, que tiene la última palabra.
3. El cliente envía **`RPC_SubmitCommand(tipo, arg)`** solo al Master y bloquea sus controles hasta recibir respuesta.
4. El Master identifica al jugador por **el remitente real** (`PhotonMessageInfo.Sender`), no por un índice que diga el cliente. Valida, ejecuta y emite el **evento** a todos (`RPC_PlayerBet`, `RPC_PlayerDrewCard`…). Si el comando no es válido, responde con `RPC_CommandRejected` solo al remitente.
5. Los eventos **solo se aceptan si vienen del Master** (`IsFromMaster(info)`).
6. El estado de turno pasa al modelo: `GameState.CurrentHighestBet`, `GameState.CallerIndex` y `Player.HasDiscardedThisTurn`. Las reglas configurables están en `RulesConfig`.

El doble clic se frena dos veces: en el cliente (`_awaitingCommandResult`) y en el Master (`HasActedThisBettingRound` hace inválida una segunda acción en el mismo turno).

## Alternativas consideradas

- **Desactivar los botones al pulsar y nada más**: arregla el síntoma visible, pero no la validación ni la confianza en los clientes.
- **Jerarquía de clases de comando con serialización propia**: más extensible, pero hoy todos los comandos caben en `(tipo, int)`. Se dejará para cuando un comando necesite más datos.
- **Aplicar ya un reducer de eventos (todos mutan el estado por el mismo camino)**: es el siguiente paso. Hacerlo a la vez habría multiplicado el riesgo del cambio.

## Consecuencias

- ✅ Cada regla de acción vive en un solo sitio y tiene tests (26 casos del validador).
- ✅ Se acaban el doble clic, los turnos saltados y los eventos emitidos por clientes.
- ✅ Se arreglan de paso dos bugs: el bote duplicado en el Master al retirarse todos, y los valores incorrectos en la revelación tras un CALL.
- ⚠️ Los eventos siguen siendo RPC con lógica dentro, y cada cliente modifica su estado al recibirlos. El Master todavía modifica algunas cosas antes de emitir (mazo, mano al robar o descartar). Lo resuelve el siguiente paso: un único *reducer* de eventos.
- ⚠️ Las fases siguen avanzando con `Invoke` y temporizadores. Lo resuelve el paso de la máquina de estados.
