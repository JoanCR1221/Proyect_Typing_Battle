import type { Standing } from '../contracts/hub';
import { formatInteger, formatNumber, formatPercent, ordinal } from './format';
import template from './tb-results.html?raw';

export interface ResultRow {
  userId: string;
  position: string;
  name: string;
  isMe: boolean;
  isWinner: boolean;
  speed: string;
  accuracy: string;
  progress: string;
  score: string;
}

/**
 * Titular y subtítulo de la pantalla final según cómo le fue al jugador local.
 * Es una función pura para poder probar todos los casos sin dibujar nada.
 */
export function describeOutcome(
  standings: readonly Standing[],
  winnerUserId: string | null,
  meId: string,
): { headline: string; detail: string } {
  const winner = standings.find((s) => s.userId === winnerUserId);
  const mine = standings.find((s) => s.userId === meId);

  if (!winner) {
    return { headline: 'Nadie escribió nada', detail: 'La carrera terminó sin ganador.' };
  }
  if (winner.userId === meId) {
    return { headline: '¡Ganaste!', detail: `Terminaste primero con ${formatInteger(winner.score)} puntos.` };
  }
  if (mine) {
    return {
      headline: `Quedaste ${ordinal(mine.rank)} de ${standings.length}`,
      detail: `Ganó ${winner.displayName} con ${formatInteger(winner.score)} puntos.`,
    };
  }
  return { headline: 'Terminó la carrera', detail: `Ganó ${winner.displayName}.` };
}

/** Pantalla final: quién ganó y la clasificación completa. */
export class TbResults {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-results',
    template,
    bindables: ['standings', 'winnerUserId', 'meId', 'resultSaved'],
  };

  standings: Standing[] = [];
  winnerUserId: string | null = null;
  meId = '';
  resultSaved = false;

  headline = '';
  detail = '';
  rows: ResultRow[] = [];

  binding(): void {
    this.refresh();
  }

  standingsChanged(): void {
    this.refresh();
  }

  winnerUserIdChanged(): void {
    this.refresh();
  }

  meIdChanged(): void {
    this.refresh();
  }

  private refresh(): void {
    const standings = this.standings ?? [];
    ({ headline: this.headline, detail: this.detail } = describeOutcome(standings, this.winnerUserId, this.meId));

    this.rows = standings.map((s) => ({
      userId: s.userId,
      position: ordinal(s.rank),
      name: s.displayName,
      isMe: s.userId === this.meId,
      isWinner: s.userId === this.winnerUserId,
      speed: formatNumber(s.wpm),
      accuracy: formatPercent(s.accuracy),
      progress: s.finished ? 'Terminó' : formatPercent(s.progress),
      score: formatInteger(s.score),
    }));
  }
}
