import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { HUB_EVENTS, type HubEventMap, type HubEventName, type RoomSnapshot } from '../contracts/hub';

/** Lo mínimo que se usa de `HubConnection` de SignalR. Permite probar el cliente con una conexión falsa. */
export interface HubConnectionLike {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T>;
  on(method: string, handler: (...args: any[]) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export type ConnectionEvent = 'reconnecting' | 'reconnected' | 'closed';

export interface HubClientOptions {
  /** URL completa del hub, por ejemplo http://localhost:5080/hubs/typing. */
  url: string;
  /** Token de acceso (JWT de Auth0). Se pide cada vez que SignalR abre una conexión. */
  getAccessToken?: () => string | undefined | Promise<string | undefined>;
  /** SOLO desarrollo: identidad sin token (la API debe estar en Auth:Mode=Development). */
  devUser?: { id: string; name: string };
}

type Handler<T> = (payload: T) => void;

/**
 * Cliente tipado del hub /hubs/typing (docs/hub-typing.md): un método por operación del servidor y suscripción
 * a sus eventos con `on()`. No sabe nada de Aurelia ni de la interfaz.
 */
export class TypingHubClient {
  private readonly hubHandlers = new Map<HubEventName, Set<Handler<never>>>();
  private readonly connectionHandlers = new Map<ConnectionEvent, Set<() => void>>();

  constructor(private readonly connection: HubConnectionLike) {
    for (const name of HUB_EVENTS) {
      connection.on(name, (payload: unknown) => this.emitHub(name, payload));
    }
    connection.onreconnecting(() => this.emitConnection('reconnecting'));
    connection.onreconnected(() => this.emitConnection('reconnected'));
    connection.onclose(() => this.emitConnection('closed'));
  }

  static create({ url, getAccessToken, devUser }: HubClientOptions): TypingHubClient {
    const target = devUser
      ? `${url}?dev_user=${encodeURIComponent(devUser.id)}&dev_name=${encodeURIComponent(devUser.name)}`
      : url;

    const connection = new HubConnectionBuilder()
      .withUrl(target, getAccessToken ? { accessTokenFactory: async () => (await getAccessToken()) ?? '' } : {})
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    return new TypingHubClient(connection);
  }

  /** Se suscribe a un evento del servidor. Devuelve la función para cancelar la suscripción. */
  on<K extends HubEventName>(event: K, handler: Handler<HubEventMap[K]>): () => void {
    return this.add(this.hubHandlers as Map<K, Set<Handler<HubEventMap[K]>>>, event, handler);
  }

  /** Se suscribe a cambios de la conexión. Tras `reconnected` hay que volver a llamar a `join`. */
  onConnection(event: ConnectionEvent, handler: () => void): () => void {
    return this.add(this.connectionHandlers, event, handler);
  }

  connect(): Promise<void> {
    return this.connection.start();
  }

  /** Entra a la sala de la partida y devuelve su estado. Los dos argumentos son obligatorios en SignalR. */
  join(matchId: string, displayName: string | null): Promise<RoomSnapshot> {
    return this.connection.invoke<RoomSnapshot>('JoinGame', matchId, displayName);
  }

  ready(): Promise<void> {
    return this.connection.invoke('SetReady');
  }

  /** Envía TODO lo que el jugador tiene escrito (no solo la última tecla). */
  submit(typed: string): Promise<void> {
    return this.connection.invoke('SubmitProgress', typed);
  }

  leave(): Promise<void> {
    return this.connection.invoke('LeaveGame');
  }

  disconnect(): Promise<void> {
    return this.connection.stop();
  }

  private add<K, H>(registry: Map<K, Set<H>>, key: K, handler: H): () => void {
    const set = registry.get(key) ?? new Set<H>();
    set.add(handler);
    registry.set(key, set);
    return () => {
      set.delete(handler);
    };
  }

  private emitHub(name: HubEventName, payload: unknown): void {
    for (const handler of this.hubHandlers.get(name) ?? []) {
      (handler as Handler<unknown>)(payload);
    }
  }

  private emitConnection(event: ConnectionEvent): void {
    for (const handler of this.connectionHandlers.get(event) ?? []) {
      handler();
    }
  }
}
