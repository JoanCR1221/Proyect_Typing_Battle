import type { GameContext } from '../src/contracts/game-module';
import type { PlayerState, RoomSnapshot } from '../src/contracts/hub';
import type { HubConnectionLike } from '../src/services/hub-client';

/** Conexión de SignalR falsa: registra lo que se invoca y permite «enviar» eventos del servidor. */
export class FakeConnection implements HubConnectionLike {
  readonly invocations: Array<{ method: string; args: unknown[] }> = [];
  readonly responses = new Map<string, (...args: unknown[]) => unknown>();
  started = false;
  stopped = false;
  failOn: string | null = null;

  private readonly handlers = new Map<string, Array<(...args: any[]) => void>>();
  private readonly reconnecting: Array<(error?: Error) => void> = [];
  private readonly reconnected: Array<(id?: string) => void> = [];
  private readonly closed: Array<(error?: Error) => void> = [];

  async start(): Promise<void> {
    if (this.failOn === 'start') {
      throw new Error('Failed to start the connection: Error: Failed to fetch');
    }
    this.started = true;
  }

  async stop(): Promise<void> {
    this.stopped = true;
  }

  async invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
    this.invocations.push({ method, args });
    if (this.failOn === method) {
      throw new Error(`An unexpected error occurred invoking '${method}' on the server. HubException: La partida ya comenzó.`);
    }
    return this.responses.get(method)?.(...args) as T;
  }

  on(method: string, handler: (...args: any[]) => void): void {
    this.handlers.set(method, [...(this.handlers.get(method) ?? []), handler]);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnecting.push(callback);
  }

  onreconnected(callback: (id?: string) => void): void {
    this.reconnected.push(callback);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closed.push(callback);
  }

  /** Simula un evento que envía el servidor. */
  emit(method: string, payload: unknown): void {
    for (const handler of this.handlers.get(method) ?? []) {
      handler(payload);
    }
  }

  simulateReconnecting(): void {
    this.reconnecting.forEach((callback) => callback());
  }

  simulateReconnected(): void {
    this.reconnected.forEach((callback) => callback('nueva-conexion'));
  }

  simulateClosed(): void {
    this.closed.forEach((callback) => callback());
  }

  calls(method: string): unknown[][] {
    return this.invocations.filter((call) => call.method === method).map((call) => call.args);
  }
}

export const CONTEXT: GameContext = {
  matchId: 'match-001',
  gameType: 'typing',
  currentUser: { id: 'ana', displayName: 'Ana' },
};

export function player(overrides: Partial<PlayerState> = {}): PlayerState {
  return {
    userId: 'ana',
    displayName: 'Ana',
    ready: false,
    connected: true,
    progress: 0,
    wpm: 0,
    accuracy: 0,
    finished: false,
    rank: null,
    ...overrides,
  };
}

export const NOW = '2026-09-02T20:00:00.000Z';

export function snapshot(overrides: Partial<RoomSnapshot> = {}): RoomSnapshot {
  return {
    matchId: 'match-001',
    state: 'waiting',
    minPlayers: 2,
    maxPlayers: 10,
    timeLimitSeconds: 60,
    countdownSeconds: 3,
    players: [player()],
    serverTime: NOW,
    startsAt: null,
    startedAt: null,
    endsAt: null,
    textId: null,
    text: null,
    myTyped: null,
    ...overrides,
  };
}
