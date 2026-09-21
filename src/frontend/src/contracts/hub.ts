// Mensajes del hub /hubs/typing. Reflejan los DTO del backend (src/backend/TypingBattle.Api/Game/GameDtos.cs);
// el detalle de cada uno está en docs/hub-typing.md. Las fechas llegan como texto ISO-8601 en UTC («...Z»).

export type GameState = 'waiting' | 'countdown' | 'running' | 'finished';

export interface PlayerState {
  userId: string;
  displayName: string;
  ready: boolean;
  connected: boolean;
  /** Porcentaje del texto escrito correctamente (0–100). */
  progress: number;
  wpm: number;
  /** Porcentaje de teclas correctas (0–100). */
  accuracy: number;
  finished: boolean;
  /** Puesto de llegada (1 = primero en terminar); null si todavía no terminó. */
  rank: number | null;
}

export interface RoomSnapshot {
  matchId: string;
  state: GameState;
  minPlayers: number;
  maxPlayers: number;
  timeLimitSeconds: number;
  countdownSeconds: number;
  players: PlayerState[];
  serverTime: string;
  startsAt: string | null;
  startedAt: string | null;
  endsAt: string | null;
  textId: string | null;
  text: string | null;
  /** Lo que este jugador lleva escrito; solo viene en la respuesta de JoinGame. */
  myTyped: string | null;
}

export interface GameStarting {
  serverTime: string;
  startsAt: string;
  countdownSeconds: number;
}

export interface GameStarted {
  serverTime: string;
  startedAt: string;
  endsAt: string;
  timeLimitSeconds: number;
  textId: string;
  text: string;
}

export interface ProgressUpdate {
  serverTime: string;
  players: PlayerState[];
}

export interface PlayerFinished {
  userId: string;
  rank: number;
  wpm: number;
  accuracy: number;
}

export interface Standing {
  rank: number;
  userId: string;
  displayName: string;
  wpm: number;
  accuracy: number;
  progress: number;
  finished: boolean;
  score: number;
}

export interface GameFinished {
  serverTime: string;
  finishedAt: string;
  winnerUserId: string | null;
  standings: Standing[];
  /** Si el resultado quedó guardado (y por lo tanto ya aparece en el historial). */
  resultSaved: boolean;
}

/** Evento del servidor → carga que trae. */
export interface HubEventMap {
  RoomUpdated: RoomSnapshot;
  GameStarting: GameStarting;
  GameStarted: GameStarted;
  ProgressUpdated: ProgressUpdate;
  PlayerFinished: PlayerFinished;
  GameFinished: GameFinished;
}

export type HubEventName = keyof HubEventMap;

export const HUB_EVENTS: readonly HubEventName[] = [
  'RoomUpdated',
  'GameStarting',
  'GameStarted',
  'ProgressUpdated',
  'PlayerFinished',
  'GameFinished',
];
