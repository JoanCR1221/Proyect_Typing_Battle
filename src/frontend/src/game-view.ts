import './styles/typing.css';
import { formatClock, formatInteger, formatNumber, formatPercent } from './components/format';
import { TbCountdown } from './components/tb-countdown';
import { TbLobby } from './components/tb-lobby';
import { TbRaceLanes } from './components/tb-race-lanes';
import { TbResults } from './components/tb-results';
import { TbResultsPanel } from './components/tb-results-panel';
import { TbTypingArea } from './components/tb-typing-area';
import type { GameController } from './game-controller';
import type { TypingGameModule } from './game-module';
import template from './game-view.html?raw';

/**
 * Elemento `<typing-game module.bind="...">`: la interfaz del juego, que se dibuja dentro del área que el Shell le
 * asigna. Se expone por Module Federation como `typingGame/GameView`. Solo lee el estado de `module.controller` y
 * reparte cada fase de la partida entre los componentes de `components/`.
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
    dependencies: [TbCountdown, TbLobby, TbRaceLanes, TbResults, TbResultsPanel, TbTypingArea],
  };

  /** El módulo que el Shell (o el modo standalone) creó, inicializó y arrancó. */
  module: TypingGameModule | null = null;

  /** Sección visible: la partida o «Mis resultados» (historial y estadísticas). */
  section: 'game' | 'mine' = 'game';

  /** Lo que escribe el jugador va al controlador, que lo envía al servidor. */
  readonly typedHandler = (typed: string): void => {
    this.controller?.submitTyped(typed);
  };

  get controller(): GameController | null {
    return this.module?.controller ?? null;
  }

  /** Durante la conexión y la carrera el jugador no debe salirse de la pantalla de juego. */
  get canBrowse(): boolean {
    const phase = this.controller?.phase;
    return phase !== 'connecting' && phase !== 'countdown' && phase !== 'running';
  }

  get inMine(): boolean {
    return this.section === 'mine' && this.canBrowse;
  }

  /** Cuenta regresiva y carrera comparten la pantalla partida en dos: escenario y carriles. */
  get racing(): boolean {
    const phase = this.controller?.phase;
    return !this.inMine && (phase === 'countdown' || phase === 'running');
  }

  get clock(): string {
    return formatClock(this.controller?.timeLeftSeconds ?? 0);
  }

  get urgent(): boolean {
    const controller = this.controller;
    return controller?.phase === 'running' && controller.timeLeftSeconds <= 10;
  }

  /** Cambia cuando termina la partida o se confirma que su resultado quedó guardado: entonces «Mis resultados» se recarga. */
  get resultsKey(): string {
    return `${this.controller?.phase}:${this.controller?.resultSaved}`;
  }

  get myWpm(): string {
    return formatNumber(this.controller?.me?.wpm);
  }

  get myAccuracy(): string {
    return formatPercent(this.controller?.me?.accuracy);
  }

  get myProgress(): string {
    return formatPercent(this.controller?.me?.progress);
  }

  get myScore(): string {
    const me = this.controller?.standings.find((s) => s.userId === this.controller?.userId);
    return formatInteger(me?.score);
  }

  showGame(): void {
    this.section = 'game';
  }

  showMine(): void {
    if (this.canBrowse) {
      this.section = 'mine';
    }
  }

  retry(): void {
    void this.controller?.connect();
  }

  resume(): void {
    this.controller?.resume();
  }
}
