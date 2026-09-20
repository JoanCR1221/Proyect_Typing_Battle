# ADR-NNN: Convención de Module Federation del microfrontend de Typing Battle

> **BORRADOR PARA REVISIÓN DEL EQUIPO.** Cuando el equipo lo apruebe, debe enviarse como Pull Request a `battlehub-contracts/adrs` (ver `README.md`). Como afecta al Shell, necesita la aprobación del Tech Lead **antes** de darlo por definitivo. Elijan el número NNN revisando los ADRs y PRs abiertos allá y actualicen la fecha.

- **Estado**: Propuesto
- **Fecha**: 2026-09-20 (UTC)
- **Equipo/Autor**: Equipo 4 (Typing Battle)

## Contexto

El contrato fija Aurelia con Module Federation (Webpack) para integrar los microfrontends al Shell, y define la interfaz `GameModule` (`initialize`, `start`, `pause`, `dispose`). No define el nombre del remote, qué módulos expone, cómo se dibuja la interfaz (`initialize` no recibe un contenedor DOM) ni cómo se comparten las dependencias. Si cada juego lo resuelve distinto, el Shell no puede cargarlos de forma genérica. La comunidad de Aurelia ha reportado que compartir solo el paquete `aurelia` provoca el error `Conflicting @aurelia/metadata module import detected`.

## Decisión

- Bundler: **Webpack 5**, como indica el contrato.
- Nombre del remote: `typingGame` (`triviaGame` y `memoryGame` para los otros juegos); archivo `remoteEntry.js`.
- Módulos expuestos: `./GameModule` (`export default` de una clase que implementa `GameModule`) y `./GameView` (el elemento `<typing-game module.bind="...">` que el Shell dibuja con `au-compose`).
- Dependencias: `aurelia` y **todos** los `@aurelia/*` compartidos como singleton, con `requiredVersion` en la misma versión mayor que use el Shell.
- Extensiones opcionales al contexto (`getAccessToken`, `apiBaseUrl`) para autenticar contra la API propia del juego.

Se comprobó con un Shell simulado (`npm run start:shell`): Aurelia se carga una sola vez y el ciclo de vida completo funciona. El detalle está en [`integracion-shell.md`](../integracion-shell.md).

## Alternativas consideradas

| Alternativa | Por qué no se eligió |
|---|---|
| Compartir solo `aurelia` (como en el tutorial oficial de Module Federation de Aurelia) | La comunidad reportó que produce dos copias del runtime y el error `Conflicting @aurelia/metadata`. Compartir todos los paquetes evita el problema sin costo. |
| Vite con `@originjs/vite-plugin-federation` | El tutorial de Aurelia lo soporta, pero el contrato habla de Webpack y el Shell y los tres juegos tendrían que usar el mismo bundler. |
| Que `GameModule` reciba un contenedor DOM y se dibuje solo | Cambia el contrato de la interfaz (`initialize(context)`), lo que requiere un ADR aprobado y cambios en el Shell. Exponer la vista aparte no toca la interfaz. |
| Sin Module Federation (iframes o web components) | El contrato ya decidió Module Federation; cambiarlo requiere un ADR de fondo. |

## Consecuencias

- Positivas:
  - El Shell carga cualquiera de los tres juegos con el mismo código (`typingGame`, `triviaGame`, `memoryGame`).
  - Se evita el error de runtime duplicado de Aurelia.
  - El ciclo de vida del contrato no cambia.
- Negativas / riesgos asumidos:
  - Compartir todos los `@aurelia/*` obliga a que el Shell y los juegos usen versiones compatibles de Aurelia; subir la versión exige coordinación entre los equipos.
  - Aurelia 2 sigue en `2.0.0-rc.2` (candidata a versión final), no en una versión estable.
  - Solo se probó contra un Shell simulado; falta el Shell real.
  - `getAccessToken` y `apiBaseUrl` no están en el contrato: el Shell tiene que entregarlos.

## Impacto en otros equipos

**Sí.** Shell (Equipo 3): debe declarar el remote, compartir los mismos paquetes de Aurelia y dibujar la vista con `au-compose`. Equipos 5 y 6 (Trivia y Memory): deberían seguir la misma convención de nombres. Este ADR debe subirse como Pull Request a `battlehub-contracts` y ser aprobado por el Tech Lead antes de implementarse de forma definitiva.
