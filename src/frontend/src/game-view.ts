import type { GameController } from './game-controller';
import type { TypingGameModule } from './game-module';
import template from './game-view.html?raw';

/**
 * Elemento `<typing-game module.bind="...">`: la interfaz del juego, que se dibuja dentro del área que el Shell le
 * asigna. Se expone por Module Federation como `typingGame/GameView`. Solo lee el estado de `module.controller`.
 *
 * Los elementos de este proyecto se definen con `static $au` en lugar de decoradores (`@customElement`, `@bindable`):
 * es lo mismo para Aurelia, pero funciona igual en Webpack y en Vitest sin configurar decoradores.
 */
export class TypingGameView {
  static $au = {
    type: 'custom-element' as const,
    name: 'typing-game',
    template,
    bindables: ['module'],
  };

  /** El módulo que el Shell (o el modo standalone) creó, inicializó y arrancó. */
  module: TypingGameModule | null = null;

  get controller(): GameController | null {
    return this.module?.controller ?? null;
  }
}
