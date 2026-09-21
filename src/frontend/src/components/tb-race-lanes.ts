import type { PlayerState } from '../contracts/hub';
import { formatNumber, formatPercent, ordinal } from './format';
import template from './tb-race-lanes.html?raw';

/** Un carril: un jugador con su barra de progreso. Los objetos se reutilizan para que la barra se anime en lugar de reaparecer. */
export interface Lane {
  userId: string;
  name: string;
  /** Color fijo del jugador (0–5), asignado según el orden en que aparece. */
  colorIndex: number;
  isMe: boolean;
  connected: boolean;
  finished: boolean;
  progress: number;
  /** Puesto actual en la carrera: los que ya terminaron por orden de llegada y luego los demás por avance. */
  position: string;
  speed: string;
  accuracy: string;
  progressText: string;
}

const LANE_COLORS = 6;

/** Puesto de cada jugador (1 = va primero). Quienes terminaron van primero, por orden de llegada; los demás, por avance. */
export function rankPlayers(players: readonly PlayerState[]): Map<string, number> {
  const ordered = players
    .map((player, index) => ({ player, index }))
    .sort((a, b) => {
      if (a.player.finished !== b.player.finished) {
        return a.player.finished ? -1 : 1;
      }
      if (a.player.finished) {
        return (a.player.rank ?? Number.MAX_SAFE_INTEGER) - (b.player.rank ?? Number.MAX_SAFE_INTEGER);
      }
      return b.player.progress - a.player.progress || a.index - b.index;
    });

  return new Map(ordered.map(({ player }, position) => [player.userId, position + 1]));
}

/**
 * Carriles de progreso de todos los jugadores, con su puesto en vivo. Se mantienen en el orden de la sala (no saltan de
 * lugar) y el puesto se muestra en cada uno.
 */
export class TbRaceLanes {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-race-lanes',
    template,
    bindables: ['players', 'meId'],
  };

  players: PlayerState[] = [];
  meId = '';

  lanes: Lane[] = [];
  private readonly known = new Map<string, Lane>();

  binding(): void {
    this.sync();
  }

  playersChanged(): void {
    this.sync();
  }

  meIdChanged(): void {
    this.sync();
  }

  private sync(): void {
    const players = this.players ?? [];
    const positions = rankPlayers(players);

    const next = players.map((player) => {
      let lane = this.known.get(player.userId);
      if (!lane) {
        lane = {
          userId: player.userId,
          name: player.displayName,
          colorIndex: this.known.size % LANE_COLORS,
          isMe: false,
          connected: true,
          finished: false,
          progress: 0,
          position: '',
          speed: '',
          accuracy: '',
          progressText: '',
        };
        this.known.set(player.userId, lane);
      }

      lane.name = player.displayName;
      lane.isMe = player.userId === this.meId;
      lane.connected = player.connected;
      lane.finished = player.finished;
      lane.progress = player.progress;
      lane.position = ordinal(positions.get(player.userId) ?? 0);
      lane.speed = formatNumber(player.wpm);
      lane.accuracy = formatPercent(player.accuracy);
      lane.progressText = formatPercent(player.progress);
      return lane;
    });

    // Solo se reemplaza el arreglo si cambió quién juega; si no, los carriles se actualizan en su lugar.
    const sameLanes = next.length === this.lanes.length && next.every((lane, index) => this.lanes[index] === lane);
    if (!sameLanes) {
      this.lanes = next;
    }
  }
}
