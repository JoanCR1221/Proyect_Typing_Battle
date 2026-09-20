// Respuestas de la API REST de resultados (docs/api-resultados.md).

export interface PlayerResult {
  userId: string;
  displayName: string;
  score: number;
}

export interface GameResult {
  matchId: string;
  gameType: string;
  players: PlayerResult[];
  startedAt: string;
  finishedAt: string;
  winnerUserId: string | null;
  metadata: Record<string, unknown>;
}

export interface PlayerHistoryItem {
  matchId: string;
  startedAt: string;
  finishedAt: string;
  score: number;
  /** Null si esa partida no guardó la velocidad. */
  wpm: number | null;
  accuracy: number | null;
  /** Puesto final (1 = ganador). */
  position: number;
  playersCount: number;
  won: boolean;
}

export interface PlayerStats {
  userId: string;
  gamesPlayed: number;
  wins: number;
  /** Fracción entre 0 y 1. */
  winRate: number;
  averageScore: number;
  bestScore: number;
  averageWpm: number | null;
  bestWpm: number | null;
  averageAccuracy: number | null;
  lastPlayedAt: string | null;
}
