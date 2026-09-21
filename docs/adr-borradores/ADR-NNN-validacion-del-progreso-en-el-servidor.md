# ADR-NNN: El servidor calcula el progreso, la velocidad y la precisión

> **BORRADOR PARA REVISIÓN DEL EQUIPO.** Cuando el equipo lo apruebe, debe enviarse como Pull Request a `battlehub-contracts/adrs` (ver `README.md`). Elijan el número NNN revisando los ADRs y PRs abiertos allá y actualicen la fecha.

- **Estado**: Propuesto
- **Fecha**: 2026-09-20 (UTC)
- **Equipo/Autor**: Equipo 4 (Typing Battle)

## Contexto

Typing Battle es una competencia: el ganador y el puntaje quedan guardados en el historial y en las estadísticas del jugador (`04-persistencia-y-api-juegos.md`). Si el microfrontend informara su propia velocidad o precisión, cualquiera podría mandar los números que quisiera desde las herramientas del navegador. El servidor tiene que poder confiar en lo que guarda.

## Decisión

El cliente solo envía **lo que el jugador tiene escrito** (`SubmitProgress(typed)`). El servidor compara con el texto, cuenta las teclas y los errores, calcula avance, velocidad, precisión y puntaje con su propio reloj, y decide el ganador. Además ignora un envío más largo de lo que una persona puede haber tecleado hasta ese momento (`35 caracteres/s × tiempo + 20`, configurable).

## Alternativas consideradas

| Alternativa | Por qué no se eligió |
|---|---|
| El cliente informa sus métricas (velocidad, precisión, puntaje) | Es lo más simple, pero se falsifica con una sola línea en la consola del navegador y el historial dejaría de significar algo. |
| El cliente envía cada pulsación de tecla como evento | Permitiría medir la precisión exacta, incluidos los errores corregidos entre dos envíos, pero multiplica los mensajes y la lógica del servidor y del cliente para un beneficio pequeño. |
| Un detector de trampas más sofisticado (patrones de tiempo entre teclas) | Excede lo necesario para un proyecto académico y produce falsos positivos. |

## Consecuencias

- Positivas:
  - Los resultados guardados son confiables: el cliente no puede declarar que ganó ni inventar una velocidad.
  - Toda la lógica de puntaje vive en un solo lugar y se prueba con un reloj falso, sin navegador.
  - Pegar el texto completo no sirve.
- Negativas / riesgos asumidos:
  - Cada envío lleva el texto escrito completo (unos 150 caracteres). El cliente debe enviarlo en cada cambio, no en cada milisegundo.
  - La precisión se calcula sobre lo que llega: un error que el jugador corrige entre dos envíos consecutivos no se cuenta.
  - El límite de velocidad detiene la trampa más simple, no a alguien decidido a automatizar la escritura con un ritmo humano.
  - La latencia de red desplaza un poco el tiempo de llegada de cada jugador.

## Impacto en otros equipos

Ninguno. Es una decisión interna del hub de Typing Battle (`/hubs/typing`); no cambia ningún contrato de `battlehub-contracts`.
