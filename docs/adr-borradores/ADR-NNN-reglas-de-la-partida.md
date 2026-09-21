# ADR-NNN: Reglas de la partida: ganador, fin de la carrera y desconexiones

> **BORRADOR PARA REVISIÓN DEL EQUIPO.** Cuando el equipo lo apruebe, debe enviarse como Pull Request a `battlehub-contracts/adrs` (ver `README.md`). Elijan el número NNN revisando los ADRs y PRs abiertos allá y actualicen la fecha.

- **Estado**: Propuesto
- **Fecha**: 2026-09-20 (UTC)
- **Equipo/Autor**: Equipo 4 (Typing Battle)

## Contexto

El contrato deja al juego definir su propia mecánica (`03-contratos-tecnicos.md`, sección 4) y pide que el resultado guarde un `winnerUserId` (`04-persistencia-y-api-juegos.md`). Hay que decidir quién gana, cuándo termina una carrera y qué pasa si alguien pierde la conexión, teniendo en cuenta que el contrato de ciclo de vida (`GameModule.pause()`) existe pero no dice qué significa pausar una carrera compartida entre varias personas.

## Decisión

- **Ganador:** el primero en escribir el texto completo y sin errores. Si se acaba el tiempo (60 s por defecto) sin que nadie termine, gana quien llegó más lejos. Si nadie escribió nada, no hay ganador y no se guarda resultado.
- **Fin de la carrera:** cuando todos los jugadores conectados terminaron, cuando se cumple el tiempo límite, o cuando no queda nadie conectado.
- **Desconexión:** un jugador desconectado no retrasa el final de la carrera. Conserva su avance, aparece en la clasificación y puede reconectarse mientras la carrera dure, llamando de nuevo a `JoinGame`. Nadie nuevo puede entrar una vez que comenzó.
- **`pause()` del microfrontend:** solo detiene la entrada de teclado y el temporizador locales. La carrera no se pausa, porque es compartida y una pausa individual le daría ventaja a quien la use.
- **Salas en memoria:** el estado de una carrera vive en el servidor mientras dura. Al terminar se guarda el resultado en la base de datos.

## Alternativas consideradas

| Alternativa | Por qué no se eligió |
|---|---|
| Gana quien tenga el mayor puntaje (velocidad × precisión) | Una persona lenta y perfecta podría vencer a quien terminó primero; en una carrera, llegar primero es lo que se espera que decida. El puntaje se guarda igual para el historial. |
| Esperar a que todos terminen, sin límite de tiempo | Un jugador inactivo dejaría la partida colgada indefinidamente. |
| Pausar la carrera para todos cuando uno llama a `pause()` | Permitiría a un jugador congelar la partida cuando va perdiendo. |
| Guardar el estado de las salas en la base de datos | Permitiría sobrevivir a un reinicio del servicio, pero una carrera dura un minuto: no justifica la complejidad ni las escrituras constantes. |

## Consecuencias

- Positivas:
  - Reglas simples y previsibles para los jugadores; todas están cubiertas por pruebas unitarias con reloj falso.
  - Una carrera nunca se queda colgada por un jugador desconectado.
- Negativas / riesgos asumidos:
  - Si el servicio se reinicia a mitad de una carrera, esa partida se pierde (los resultados ya guardados no).
  - Quien se desconecta al empezar y no vuelve figura con 0 % de avance en la clasificación.
  - No se puede entrar a una partida ya comenzada: si el Shell carga el juego tarde, ese jugador no participa.

## Impacto en otros equipos

Posiblemente el Shell (Equipo 3): `pause()` no detiene la carrera. Debe documentarse en la integración con el Shell y, si el contrato de ciclo de vida se quiere precisar, en un Issue de `battlehub-contracts`.
