# Interfaz de Typing Battle

Cómo está armada la interfaz del microfrontend (`src/frontend/src`). El contrato visual (`03-contratos-tecnicos.md`, sección 7) manda: el juego solo dibuja dentro del área que le da el Shell (ancho 100 %, máximo 1440 px, mínimo 1024 px, alto mínimo 700 px) y no toca su navegación, login, header ni footer.

## Pantallas según la fase de la partida

`game-view.html` (`<typing-game>`) lee `module.controller.phase` y muestra:

| Fase | Pantalla |
|---|---|
| `connecting` | Aviso de conexión con indicador de carga |
| `lobby` | Sala de espera (`tb-lobby`): quiénes están, quién está listo y cuántos faltan |
| `countdown` | Cuenta regresiva (`tb-countdown`) junto a los carriles |
| `running` | Área de escritura (`tb-typing-area`), tus números en vivo y los carriles (`tb-race-lanes`) |
| `finished` | Resultado (`tb-results`): quién ganó y la clasificación completa |
| `error` | Mensaje del problema con «Reintentar» |

La barra superior lleva el nombre del juego, la partida y, durante la carrera, el temporizador. Desde ella se pasa a **Mis resultados** (`tb-results-panel`: historial y estadísticas del jugador), sección que se deshabilita mientras hay una carrera en curso para no sacar al jugador de la pantalla de juego.

## Componentes

| Componente | Qué hace |
|---|---|
| `tb-typing-area` | Muestra el texto carácter por carácter (correcto, error, actual, pendiente) y captura el teclado con un `<textarea>` invisible encima. Solo informa lo escrito; **no calcula velocidad ni precisión**, eso lo hace el servidor. Cancela Enter, pegar y arrastrar |
| `tb-race-lanes` | Un carril por jugador, con su puesto en vivo, barra de avance, velocidad y precisión. Los carriles mantienen el orden de la sala (no saltan de lugar) y las barras se animan porque los objetos se reutilizan |
| `tb-lobby` | Sala de espera |
| `tb-countdown` | Cuenta regresiva; cada número entra con un «pop». Es el único movimiento destacado del diseño |
| `tb-results` | Titular según cómo le fue al jugador («¡Ganaste!», «Quedaste 2.º de 3») y tabla de clasificación con su fila resaltada. Avisa si el resultado no se pudo guardar |
| `tb-results-panel` | Historial (últimas 20 partidas) y estadísticas, leídos de la API REST. Tiene estados de carga, vacío y error con reintento |

Las funciones que toman decisiones (`describeOutcome`, `rankPlayers`, `updateChars`, los formatos de `format.ts`) son puras y tienen pruebas propias.

## Diseño

- **Tema claro.** La parte central es leer un texto largo y copiarlo, y para eso importa el máximo contraste; también es lo más probable que combine con el área del Shell.
- **Verde azulado profundo** para la estructura (encabezado, cuenta regresiva) y una versión más viva para lo accionable y lo correcto. El ámbar marca el carácter actual y el estado «en pausa». Los errores van en rojo **con subrayado**, para no depender solo del color.
- Cada jugador tiene un color de carril, pero el jugador local se identifica además con la etiqueta «Tú».
- Tipografía del sistema (`system-ui`), con números tabulares en velocidades, porcentajes y el temporizador para que no bailen al cambiar. El texto a copiar mide unos 68 caracteres por línea.
- Todos los colores y medidas salen de variables CSS bajo `.tb-app` (`src/styles/typing.css`) y todas las clases llevan el prefijo `tb-`, para no chocar con los estilos del Shell.

## Accesibilidad

- Contraste de los pares de color verificado con la fórmula WCAG: texto de al menos 4,5:1 y elementos gráficos de al menos 3:1.
- Las barras de avance son `role="progressbar"` con su valor y una etiqueta con el nombre del jugador; el temporizador es `role="timer"`; la cuenta regresiva se anuncia con `aria-live`; las tablas tienen título para lectores de pantalla.
- Foco visible en todos los controles y en el área de escritura, que además recibe el foco sola al empezar la carrera.
- Con `prefers-reduced-motion` se quitan las animaciones y transiciones.

## Detalles técnicos que importan

- **`.trigger` no cancela eventos en Aurelia 2** (en la versión 1 sí lo hacía). Por eso `tb-typing-area` llama a `event.preventDefault()` de forma explícita para Enter, pegar y arrastrar. Está cubierto por pruebas.
- Los elementos se definen con `static $au`, sin decoradores, para que funcionen igual en Webpack y en Vitest.
- Los estilos se importan desde `game-view.ts` y se inyectan al cargar el módulo; con Module Federation llegan al documento del Shell junto con el remote.

## Lo que no hay

- Modo oscuro.
- Diseño para pantallas menores a 1024 px: el Shell garantiza ese mínimo. Por debajo de 960 px la disposición pasa a una sola columna solo para no romperse.
- Sonidos ni efectos adicionales.
