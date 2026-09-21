import type { TypingGameModule } from '../game-module';
import template from './mock-shell.html?raw';

/**
 * SHELL SIMULADO. Carga el juego como lo haría el Shell real: pide a Module Federation los módulos
 * `typingGame/GameModule` y `typingGame/GameView`, crea el módulo, lo inicializa con un contexto y dibuja la vista.
 * Si esto funciona (y Aurelia no se carga dos veces) la integración con el Shell es viable.
 */
export class MockShell {
  static $au = { type: 'custom-element' as const, name: 'mock-shell', template };

  gameModule: TypingGameModule | null = null;
  /** Clase del elemento <typing-game>, cargada desde el remote. `au-compose` la dibuja. */
  gameView: unknown = null;

  status = 'cargando el remote desde Module Federation...';
  lastError: string | null = null;

  async attached(): Promise<void> {
    try {
      const [{ default: GameModuleClass }, { TypingGameView }] = await Promise.all([
        import('typingGame/GameModule'),
        import('typingGame/GameView'),
      ]);

      const module = new GameModuleClass({ devAuth: true });
      await module.initialize({
        matchId: new URLSearchParams(window.location.search).get('match') ?? 'shell-mock',
        gameType: 'typing',
        currentUser: { id: new URLSearchParams(window.location.search).get('user') ?? 'ana', displayName: 'Jugador del Shell' },
      });

      this.gameModule = module;
      this.gameView = TypingGameView;
      this.status = 'remote cargado e inicializado';
    } catch (error) {
      this.fail(error);
    }
  }

  async detaching(): Promise<void> {
    await this.gameModule?.dispose();
  }

  start(): Promise<void> {
    return this.call('en marcha', (module) => module.start());
  }

  pause(): Promise<void> {
    return this.call('en pausa', (module) => module.pause());
  }

  dispose(): Promise<void> {
    return this.call('destruido', (module) => module.dispose());
  }

  private async call(status: string, action: (module: TypingGameModule) => Promise<void>): Promise<void> {
    if (!this.gameModule) {
      return;
    }
    try {
      this.lastError = null;
      await action(this.gameModule);
      this.status = status;
    } catch (error) {
      this.fail(error);
    }
  }

  private fail(error: unknown): void {
    this.lastError = error instanceof Error ? error.message : String(error);
  }
}
