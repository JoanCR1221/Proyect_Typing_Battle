import type { GameContext, GameModule } from './contracts/game-module';
import { resolveConfig, type TypingConfig } from './config';
import { GameController } from './game-controller';
import { ServerClock } from './services/clock';
import { TypingHubClient } from './services/hub-client';
import { ResultsApi } from './services/results-api';

export interface TypingGameModuleOptions {
  /** Cambia la configuración por defecto (URL de la API, ruta del hub). */
  config?: Partial<TypingConfig>;
  /** SOLO desarrollo: se identifica sin token. La API debe estar en Auth:Mode=Development. */
  devAuth?: boolean;
  /** Para pruebas: reemplaza el cliente real del hub. */
  hubFactory?: (url: string, context: GameContext) => TypingHubClient;
}

const GAME_TYPE = 'typing';

/**
 * Microfrontend de Typing Battle: implementa el ciclo de vida que el Shell espera de todo juego
 * (03-contratos-tecnicos.md, sección 6). Se expone por Module Federation como `typingGame/GameModule`.
 *
 * El Shell hace: `const m = new TypingGameModule(); await m.initialize(context); await m.start();` y al descargarlo
 * `await m.dispose()`. La interfaz (`<typing-game module.bind="m">`, ver game-view.ts) lee el estado de `m.controller`.
 */
export class TypingGameModule implements GameModule {
  /** Estado de la partida para la interfaz. Es null antes de `initialize` y después de `dispose`. */
  controller: GameController | null = null;

  constructor(private readonly options: TypingGameModuleOptions = {}) {}

  async initialize(context: GameContext): Promise<void> {
    if (this.controller) {
      await this.dispose();
    }
    assertValidContext(context);

    const config = resolveConfig({
      ...this.options.config,
      ...(context.apiBaseUrl ? { apiBaseUrl: context.apiBaseUrl } : {}),
    });
    const devUser = this.options.devAuth
      ? { id: context.currentUser.id, name: context.currentUser.displayName }
      : undefined;

    const hubUrl = `${config.apiBaseUrl}${config.hubPath}`;
    const hub =
      this.options.hubFactory?.(hubUrl, context) ??
      TypingHubClient.create({ url: hubUrl, getAccessToken: context.getAccessToken, devUser });
    const api = new ResultsApi({
      baseUrl: config.apiBaseUrl,
      getAccessToken: context.getAccessToken,
      devUser: devUser?.id,
    });

    // Se prepara todo pero no se conecta: eso lo hace start().
    this.controller = new GameController({ context, hub, api, clock: new ServerClock() });
  }

  async start(): Promise<void> {
    const controller = this.requireController('start');
    if (controller.paused) {
      controller.resume();
      return;
    }
    await controller.connect();
  }

  async pause(): Promise<void> {
    this.requireController('pause').pause();
  }

  async dispose(): Promise<void> {
    const controller = this.controller;
    this.controller = null;
    await controller?.dispose();
  }

  private requireController(operation: string): GameController {
    if (!this.controller) {
      throw new Error(`No se puede llamar a ${operation}(): primero hay que llamar a initialize(context).`);
    }
    return this.controller;
  }
}

function assertValidContext(context: GameContext): void {
  if (!context?.matchId?.trim()) {
    throw new Error('El contexto del juego no trae matchId.');
  }
  if (!context.currentUser?.id?.trim()) {
    throw new Error('El contexto del juego no trae currentUser.id.');
  }
  if (context.gameType && context.gameType !== GAME_TYPE) {
    throw new Error(`Este microfrontend es de '${GAME_TYPE}', pero el contexto es de '${context.gameType}'.`);
  }
}

export default TypingGameModule;
