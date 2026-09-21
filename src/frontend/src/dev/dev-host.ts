import type { GameContext } from '../contracts/game-module';
import { TypingGameModule } from '../game-module';
import { TypingGameView } from '../game-view';
import template from './dev-host.html?raw';

export interface DevParams {
  matchId: string;
  userId: string;
  displayName: string;
  apiBaseUrl?: string;
  autostart: boolean;
}

/**
 * Lee los parámetros de la URL del modo standalone:
 * `?match=demo&user=ana&name=Ana&api=http://localhost:5080&autostart=0`.
 */
export function readParams(search: string): DevParams {
  const query = new URLSearchParams(search);
  const userId = query.get('user')?.trim() || 'ana';

  return {
    matchId: query.get('match')?.trim() || 'demo',
    userId,
    displayName: query.get('name')?.trim() || userId.charAt(0).toUpperCase() + userId.slice(1),
    apiBaseUrl: query.get('api')?.trim() || undefined,
    autostart: query.get('autostart') !== '0',
  };
}

/**
 * MODO STANDALONE: hace el papel del Shell para poder desarrollar sin él. Crea un contexto falso, inicializa y
 * arranca el módulo (o deja los botones para probar el ciclo de vida a mano) y dibuja el juego dentro de un área con
 * las medidas del contrato visual (03-contratos-tecnicos.md, sección 7).
 * Para simular dos jugadores, abrir dos pestañas con `?user=ana` y `?user=luis`. La API debe estar en Auth:Mode=Development.
 */
export class DevHost {
  static $au = { type: 'custom-element' as const, name: 'dev-host', template, dependencies: [TypingGameView] };

  readonly params = readParams(window.location.search);

  readonly context: GameContext = {
    matchId: this.params.matchId,
    gameType: 'typing',
    currentUser: { id: this.params.userId, displayName: this.params.displayName },
  };

  readonly contextJson = JSON.stringify(this.context, null, 2);
  readonly gameModule = new TypingGameModule({
    devAuth: true,
    config: this.params.apiBaseUrl ? { apiBaseUrl: this.params.apiBaseUrl } : undefined,
  });

  status = 'sin inicializar';
  lastError: string | null = null;

  async attached(): Promise<void> {
    await this.run('inicializado', () => this.gameModule.initialize(this.context));
    if (this.params.autostart) {
      await this.start();
    }
  }

  async detaching(): Promise<void> {
    await this.gameModule.dispose();
  }

  initialize(): Promise<void> {
    return this.run('inicializado', () => this.gameModule.initialize(this.context));
  }

  start(): Promise<void> {
    return this.run('en marcha', () => this.gameModule.start());
  }

  pause(): Promise<void> {
    return this.run('en pausa', () => this.gameModule.pause());
  }

  dispose(): Promise<void> {
    return this.run('destruido', () => this.gameModule.dispose());
  }

  private async run(status: string, action: () => Promise<void>): Promise<void> {
    try {
      this.lastError = null;
      await action();
      this.status = status;
    } catch (error) {
      this.lastError = error instanceof Error ? error.message : String(error);
    }
  }
}
