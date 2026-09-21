import type { PlayerHistoryItem, PlayerStats } from '../contracts/results';
import type { ResultsApi } from '../services/results-api';
import { formatDate, formatInteger, formatNumber, formatPercent, formatRatio, ordinal } from './format';
import template from './tb-results-panel.html?raw';

export type PanelState = 'loading' | 'ready' | 'error';

export interface HistoryRow {
  matchId: string;
  date: string;
  outcome: string;
  won: boolean;
  score: string;
  speed: string;
  accuracy: string;
}

export interface StatLine {
  label: string;
  value: string;
}

export function toHistoryRow(item: PlayerHistoryItem): HistoryRow {
  return {
    matchId: item.matchId,
    date: formatDate(item.finishedAt),
    outcome: item.won ? 'Ganaste' : `${ordinal(item.position)} de ${item.playersCount}`,
    won: item.won,
    score: formatInteger(item.score),
    speed: item.wpm === null ? '—' : `${formatNumber(item.wpm)} ppm`,
    accuracy: formatPercent(item.accuracy),
  };
}

export function toStatLines(stats: PlayerStats): StatLine[] {
  return [
    { label: 'Partidas jugadas', value: formatInteger(stats.gamesPlayed) },
    { label: 'Victorias', value: `${formatInteger(stats.wins)} (${formatRatio(stats.winRate)})` },
    { label: 'Puntaje promedio', value: formatInteger(stats.averageScore) },
    { label: 'Mejor puntaje', value: formatInteger(stats.bestScore) },
    { label: 'Velocidad promedio', value: stats.averageWpm === null ? '—' : `${formatNumber(stats.averageWpm)} ppm` },
    { label: 'Mejor velocidad', value: stats.bestWpm === null ? '—' : `${formatNumber(stats.bestWpm)} ppm` },
    { label: 'Precisión promedio', value: formatPercent(stats.averageAccuracy) },
    { label: 'Última partida', value: formatDate(stats.lastPlayedAt) },
  ];
}

/** Historial de partidas y estadísticas del jugador, leídos de la API de resultados. */
export class TbResultsPanel {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-results-panel',
    template,
    bindables: ['api', 'userId', 'refreshKey'],
  };

  api: ResultsApi | null = null;
  userId = '';

  /** Cuando cambia, se vuelve a cargar (por ejemplo, al terminar una partida y guardarse su resultado). */
  refreshKey = '';

  state: PanelState = 'loading';
  error = '';
  history: HistoryRow[] = [];
  stats: StatLine[] = [];
  gamesPlayed = 0;

  private requestId = 0;

  attached(): void {
    void this.load();
  }

  refreshKeyChanged(): void {
    void this.load();
  }

  retry(): void {
    void this.load();
  }

  async load(): Promise<void> {
    if (!this.api || !this.userId) {
      return;
    }

    // Si llegan dos cargas seguidas, solo cuenta la última.
    const request = ++this.requestId;
    this.state = 'loading';

    try {
      const [history, stats] = await Promise.all([this.api.history(this.userId, 20), this.api.stats(this.userId)]);
      if (request !== this.requestId) {
        return;
      }
      this.history = history.map(toHistoryRow);
      this.stats = toStatLines(stats);
      this.gamesPlayed = stats.gamesPlayed;
      this.state = 'ready';
    } catch (error) {
      if (request !== this.requestId) {
        return;
      }
      this.error = error instanceof Error ? error.message : 'No se pudieron cargar tus resultados.';
      this.state = 'error';
    }
  }
}
