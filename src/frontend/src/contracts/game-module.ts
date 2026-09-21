/** Usuario actual, tal como lo entrega el Shell (03-contratos-tecnicos.md, sección 5). */
export interface CurrentUser {
  id: string;
  displayName: string;
}

/**
 * Contexto que el Shell le entrega al juego al cargarlo (03-contratos-tecnicos.md, sección 5).
 *
 * `getAccessToken` y `apiBaseUrl` NO forman parte del contrato: son extensiones opcionales que necesita
 * la API propia del juego para autenticar al usuario (duda 3 de docs/contratos-pendientes.md).
 * Si el Shell no las entrega, el juego usa el token que tenga configurado y `TYPING_API_URL`.
 */
export interface GameContext {
  matchId: string;
  gameType: string;
  currentUser: CurrentUser;
  getAccessToken?: () => string | undefined | Promise<string | undefined>;
  apiBaseUrl?: string;
}

/** Ciclo de vida de un microfrontend de juego (03-contratos-tecnicos.md, sección 6). */
export interface GameModule {
  /** Recibe el contexto y prepara el estado inicial, sin iniciar la partida todavía. */
  initialize(context: GameContext): Promise<void>;

  /** Comienza la ejecución: conecta al hub propio y arranca los temporizadores. Si estaba en pausa, la reanuda. */
  start(): Promise<void>;

  /**
   * Pausa la entrada local (por ejemplo, si el usuario navega fuera del área del juego).
   * La carrera NO se detiene: es compartida con otros jugadores (ver el ADR de reglas de la partida).
   */
  pause(): Promise<void>;

  /** Libera recursos, desconecta del hub y limpia los listeners antes de que el Shell descargue el microfrontend. */
  dispose(): Promise<void>;
}
