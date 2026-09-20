/**
 * El reloj del navegador no coincide con el del servidor, y las fechas de la partida (cuándo termina la cuenta
 * regresiva, cuándo acaba el tiempo) son del servidor. Cada mensaje trae `serverTime`; con él se calcula el
 * desfase para que el temporizador de todos los jugadores marque lo mismo.
 */
export class ServerClock {
  private offsetMs = 0;

  /** Registra la hora del servidor recibida en un mensaje. El retraso de red (unos milisegundos) se ignora. */
  sync(serverTime: string, localNow: number = Date.now()): void {
    const server = Date.parse(serverTime);
    if (!Number.isNaN(server)) {
      this.offsetMs = server - localNow;
    }
  }

  /** Hora actual del servidor, en milisegundos desde 1970. */
  now(): number {
    return Date.now() + this.offsetMs;
  }
}
