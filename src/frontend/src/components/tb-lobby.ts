import type { PlayerState } from '../contracts/hub';
import template from './tb-lobby.html?raw';

/** Sala de espera: quiénes están y cuántos faltan para poder empezar. */
export class TbLobby {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-lobby',
    template,
    bindables: ['players', 'minPlayers', 'maxPlayers', 'meId'],
  };

  players: PlayerState[] = [];
  minPlayers = 2;
  maxPlayers = 10;
  meId = '';

  /** Jugadores que faltan para llegar al mínimo. */
  get missing(): number {
    return Math.max(0, this.minPlayers - this.players.length);
  }
}
