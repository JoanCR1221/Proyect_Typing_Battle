import type { GameContext } from './contracts/game-module';
import type {
  GameFinished,
  GameStarted,
  GameStarting,
  PlayerState,
  ProgressUpdate,
  RoomSnapshot,
  Standing,
} from './contracts/hub';
import type { GameResult } from './contracts/results';
import { ServerClock } from './services/clock';
import type { TypingHubClient } from './services/hub-client';
import type { ResultsApi } from './services/results-api';

export type GamePhase = 'idle' | 'connecting' | 'lobby' | 'countdown' | 'running' | 'finished' | 'error' | 'closed';

export interface GameControllerDeps {
  context: GameContext;
  hub: TypingHubClient;
  api: ResultsApi;
  clock?: ServerClock;
  /** Marcarse como «listo» al entrar a la sala, sin que el jugador tenga que hacer nada (por defecto, sí). */
  autoReady?: boolean;
  /** Cada cuánto se actualizan los segundos que muestran los temporizadores. */
  tickMs?: number;
}

/**
 * Estado de una partida para la interfaz. Recibe los eventos del hub, mantiene las propiedades que las vistas
 * muestran (fase, jugadores, texto, temporizadores, clasificación) y envía lo que el jugador escribe.
 * No depende de Aurelia: las vistas solo leen estas propiedades y Aurelia las observa.
 */
export class GameController {
  phase: GamePhase = 'idle';
  error: string | null = null;

  /** El jugador pausó su entrada (`GameModule.pause()`). La carrera sigue para los demás. */
  paused = false;
  reconnecting = false;

  players: PlayerState[] = [];
  minPlayers = 2;
  maxPlayers = 10;
  timeLimitSeconds = 60;

  text: string | null = null;
  textId: string | null = null;
  /** Lo que el jugador lleva escrito. */
  typed = '';

  /** Segundos que faltan para que empiece la carrera (cuenta regresiva). */
  countdownSeconds = 0;
  /** Segundos que quedan de carrera. */
  timeLeftSeconds = 0;

  standings: Standing[] = [];
  winnerUserId: string | null = null;
  /** Si el resultado ya quedó guardado y aparece en el historial. */
  resultSaved = false;

  private startsAt: number | null = null;
  private endsAt: number | null = null;
  private ticker: ReturnType<typeof setInterval> | null = null;
  private subscriptions: Array<() => void> = [];
  private sending = false;
  private pending: string | null = null;

  private readonly context: GameContext;
  private readonly hub: TypingHubClient;
  private readonly api: ResultsApi;
  private readonly clock: ServerClock;
  private readonly autoReady: boolean;
  private readonly tickMs: number;

  constructor({ context, hub, api, clock, autoReady = true, tickMs = 250 }: GameControllerDeps) {
    this.context = context;
    this.hub = hub;
    this.api = api;
    this.clock = clock ?? new ServerClock();
    this.autoReady = autoReady;
    this.tickMs = tickMs;
  }

  /** Id de la partida (el `matchId` que entregó el Shell). */
  get matchId(): string {
    return this.context.matchId;
  }

  /** Id del jugador local: con él se consulta su historial y sus estadísticas. */
  get userId(): string {
    return this.context.currentUser.id;
  }

  /** Cliente de la API de resultados, para que la interfaz muestre el historial y las estadísticas. */
  get resultsApi(): ResultsApi {
    return this.api;
  }

  /** El jugador local dentro de la lista de jugadores (undefined hasta que el servidor lo informe). */
  get me(): PlayerState | undefined {
    return this.players.find((player) => player.userId === this.context.currentUser.id);
  }

  /** Conecta al hub, entra a la sala de la partida y, si corresponde, se marca como listo. */
  async connect(): Promise<void> {
    if (this.phase !== 'idle' && this.phase !== 'error' && this.phase !== 'closed') {
      return;
    }

    this.reset();
    this.phase = 'connecting';
    this.subscribe();

    try {
      await this.hub.connect();
      await this.enterRoom();
    } catch (error) {
      this.fail(error);
    }
  }

  /**
   * Envía lo que el jugador tiene escrito. Solo durante la carrera y si no está en pausa. Si hay un envío en curso
   * se guarda solo el último texto: no tiene sentido enviar los intermedios cuando ya hay uno más nuevo.
   */
  submitTyped(typed: string): void {
    if (this.phase !== 'running' || this.paused) {
      return;
    }

    this.typed = typed;
    this.pending = typed;
    if (!this.sending) {
      void this.flush();
    }
  }

  pause(): void {
    this.paused = true;
  }

  resume(): void {
    this.paused = false;
  }

  /** Sale de la sala, desconecta y cancela las suscripciones y el temporizador. */
  async dispose(): Promise<void> {
    const connected = this.phase !== 'idle' && this.phase !== 'closed';

    this.unsubscribeAll();
    this.stopTicker();
    this.pending = null;

    if (connected) {
      // Si la conexión ya se había perdido, estas llamadas fallan; no importa, solo se está cerrando.
      await this.hub.leave().catch(() => undefined);
      await this.hub.disconnect().catch(() => undefined);
    }

    this.phase = 'closed';
  }

  private async enterRoom(): Promise<void> {
    const snapshot = await this.hub.join(this.context.matchId, this.context.currentUser.displayName);
    this.applySnapshot(snapshot);

    if (this.autoReady && snapshot.state === 'waiting') {
      await this.hub.ready();
    }
  }

  private subscribe(): void {
    this.unsubscribeAll();
    this.subscriptions.push(
      this.hub.on('RoomUpdated', (snapshot) => this.onRoomUpdated(snapshot)),
      this.hub.on('GameStarting', (event) => this.onGameStarting(event)),
      this.hub.on('GameStarted', (event) => this.onGameStarted(event)),
      this.hub.on('ProgressUpdated', (event) => this.onProgress(event)),
      this.hub.on('GameFinished', (event) => this.onGameFinished(event)),
      this.hub.onConnection('reconnecting', () => {
        this.reconnecting = true;
      }),
      this.hub.onConnection('reconnected', () => void this.onReconnected()),
      this.hub.onConnection('closed', () => this.onClosed()),
    );
  }

  private unsubscribeAll(): void {
    for (const unsubscribe of this.subscriptions) {
      unsubscribe();
    }
    this.subscriptions = [];
  }

  // ---- eventos del servidor ----

  private onRoomUpdated(snapshot: RoomSnapshot): void {
    if (this.phase === 'finished' || this.phase === 'closed' || this.phase === 'error') {
      return;
    }

    this.clock.sync(snapshot.serverTime);
    this.players = snapshot.players;
    this.minPlayers = snapshot.minPlayers;
    this.maxPlayers = snapshot.maxPlayers;

    if (this.phase === 'connecting' || this.phase === 'lobby' || this.phase === 'countdown') {
      this.applyLobbyState(snapshot);
    }
  }

  private onGameStarting(event: GameStarting): void {
    this.clock.sync(event.serverTime);
    this.phase = 'countdown';
    this.startsAt = Date.parse(event.startsAt);
    this.startTicker();
    this.tick();
  }

  private onGameStarted(event: GameStarted): void {
    this.clock.sync(event.serverTime);
    this.phase = 'running';
    this.text = event.text;
    this.textId = event.textId;
    this.typed = '';
    this.timeLimitSeconds = event.timeLimitSeconds;
    this.endsAt = Date.parse(event.endsAt);
    this.countdownSeconds = 0;
    this.startTicker();
    this.tick();
  }

  private onProgress(event: ProgressUpdate): void {
    this.clock.sync(event.serverTime);
    this.players = event.players;
  }

  private onGameFinished(event: GameFinished): void {
    this.clock.sync(event.serverTime);
    this.phase = 'finished';
    this.standings = event.standings;
    this.winnerUserId = event.winnerUserId;
    this.resultSaved = event.resultSaved;
    this.countdownSeconds = 0;
    this.timeLeftSeconds = 0;
    this.pending = null;
    this.stopTicker();

    // El último ProgressUpdated puede haber salido antes de las últimas teclas: la lista de jugadores
    // toma los valores finales de la clasificación para que ambas muestren lo mismo.
    this.players = this.players.map((player) => {
      const standing = event.standings.find((s) => s.userId === player.userId);
      return standing
        ? {
            ...player,
            progress: standing.progress,
            wpm: standing.wpm,
            accuracy: standing.accuracy,
            finished: standing.finished,
            rank: standing.finished ? standing.rank : null,
          }
        : player;
    });
  }

  private async onReconnected(): Promise<void> {
    this.reconnecting = false;
    try {
      // La conexión nueva no pertenece a la sala: hay que volver a entrar. El servidor devuelve lo que se llevaba escrito.
      await this.enterRoom();
    } catch (error) {
      this.fail(error);
    }
  }

  private onClosed(): void {
    if (this.phase === 'finished' || this.phase === 'closed') {
      return;
    }
    this.stopTicker();
    this.fail(new Error('Se perdió la conexión con el servidor del juego.'));
  }

  // ---- estado ----

  /** Aplica la foto de la sala que devuelve JoinGame (al entrar o al reconectarse). */
  private applySnapshot(snapshot: RoomSnapshot): void {
    this.clock.sync(snapshot.serverTime);
    this.players = snapshot.players;
    this.minPlayers = snapshot.minPlayers;
    this.maxPlayers = snapshot.maxPlayers;
    this.timeLimitSeconds = snapshot.timeLimitSeconds;

    switch (snapshot.state) {
      case 'waiting':
      case 'countdown':
        this.applyLobbyState(snapshot);
        break;
      case 'running':
        this.phase = 'running';
        this.text = snapshot.text;
        this.textId = snapshot.textId;
        this.typed = snapshot.myTyped ?? '';
        this.endsAt = snapshot.endsAt ? Date.parse(snapshot.endsAt) : null;
        this.startTicker();
        break;
      case 'finished':
        this.phase = 'finished';
        this.stopTicker();
        void this.loadFinishedResult();
        break;
    }

    this.tick();
  }

  private applyLobbyState(snapshot: RoomSnapshot): void {
    if (snapshot.state === 'countdown' && snapshot.startsAt) {
      this.phase = 'countdown';
      this.startsAt = Date.parse(snapshot.startsAt);
      this.startTicker();
    } else if (snapshot.state === 'waiting') {
      this.phase = 'lobby';
      this.startsAt = null;
      this.countdownSeconds = 0;
      this.stopTicker();
    }
  }

  /** Si el jugador vuelve cuando la partida ya terminó, la clasificación se reconstruye con el resultado guardado. */
  private async loadFinishedResult(): Promise<void> {
    try {
      const result = await this.api.result(this.context.matchId);
      if (result) {
        this.standings = standingsFromResult(result);
        this.winnerUserId = result.winnerUserId;
        this.resultSaved = true;
      }
    } catch {
      // Sin resultado se muestra la pantalla final sin clasificación; no es un error del juego.
    }
  }

  private async flush(): Promise<void> {
    this.sending = true;
    try {
      while (this.pending !== null) {
        const next = this.pending;
        this.pending = null;
        // Un fallo de envío no se trata aquí: si la conexión se cayó, los eventos de conexión ya lo informan.
        await this.hub.submit(next).catch(() => undefined);
      }
    } finally {
      this.sending = false;
    }
  }

  private tick(): void {
    const now = this.clock.now();

    if (this.phase === 'countdown' && this.startsAt !== null) {
      this.countdownSeconds = Math.max(0, Math.ceil((this.startsAt - now) / 1000));
    }
    if (this.phase === 'running' && this.endsAt !== null) {
      this.timeLeftSeconds = Math.max(0, Math.ceil((this.endsAt - now) / 1000));
    }
  }

  private startTicker(): void {
    if (this.ticker === null) {
      this.ticker = setInterval(() => this.tick(), this.tickMs);
    }
  }

  private stopTicker(): void {
    if (this.ticker !== null) {
      clearInterval(this.ticker);
      this.ticker = null;
    }
  }

  private reset(): void {
    this.error = null;
    this.paused = false;
    this.reconnecting = false;
    this.players = [];
    this.text = null;
    this.textId = null;
    this.typed = '';
    this.countdownSeconds = 0;
    this.timeLeftSeconds = 0;
    this.standings = [];
    this.winnerUserId = null;
    this.resultSaved = false;
    this.startsAt = null;
    this.endsAt = null;
    this.pending = null;
  }

  private fail(error: unknown): void {
    this.error = describeError(error);
    this.phase = 'error';
    this.stopTicker();
  }
}

/** Convierte un error técnico (de SignalR o de red) en un mensaje entendible para el jugador. */
export function describeError(error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);

  // Los errores que el servidor lanza a propósito llegan como «... HubException: <mensaje>».
  const hubMessage = /HubException: (.+)$/s.exec(message)?.[1]?.trim();
  if (hubMessage) {
    return hubMessage;
  }
  if (/403|forbidden/i.test(message)) {
    return 'Tu cuenta no tiene permiso para jugar Typing Battle.';
  }
  if (/401|unauthorized/i.test(message)) {
    return 'No se pudo comprobar tu identidad. Vuelve a iniciar sesión.';
  }
  if (/failed to (start|fetch|complete negotiation)|network|econnrefused/i.test(message)) {
    return 'No se pudo conectar con el servidor del juego.';
  }
  return message || 'Ocurrió un error inesperado.';
}

/** Clasificación reconstruida desde el resultado guardado (el orden de `players` es el ranking final). */
export function standingsFromResult(result: GameResult): Standing[] {
  const details = new Map<string, { wpm?: number; accuracy?: number; progress?: number; finished?: boolean }>();
  const players = result.metadata['players'];
  if (Array.isArray(players)) {
    for (const entry of players) {
      if (entry && typeof entry === 'object' && typeof (entry as { userId?: unknown }).userId === 'string') {
        details.set((entry as { userId: string }).userId, entry as { wpm?: number });
      }
    }
  }

  return result.players.map((player, index) => {
    const extra = details.get(player.userId);
    return {
      rank: index + 1,
      userId: player.userId,
      displayName: player.displayName,
      wpm: extra?.wpm ?? 0,
      accuracy: extra?.accuracy ?? 0,
      progress: extra?.progress ?? 0,
      finished: extra?.finished ?? false,
      score: player.score,
    };
  });
}
