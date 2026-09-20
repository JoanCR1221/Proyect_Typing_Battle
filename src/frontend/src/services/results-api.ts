import type { GameResult, PlayerHistoryItem, PlayerStats } from '../contracts/results';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface ResultsApiOptions {
  /** URL base de la API del juego, sin «/» final. */
  baseUrl: string;
  /** Token de acceso (JWT de Auth0). */
  getAccessToken?: () => string | undefined | Promise<string | undefined>;
  /** SOLO desarrollo: identidad sin token (encabezado X-Dev-User; la API debe estar en Auth:Mode=Development). */
  devUser?: string;
  /** Para pruebas. Por defecto, el `fetch` del navegador. */
  fetchFn?: typeof fetch;
}

/** Cliente de la API REST de resultados (docs/api-resultados.md). */
export class ResultsApi {
  constructor(private readonly options: ResultsApiOptions) {}

  /** Resultado detallado de una partida; `null` si todavía no existe. */
  async result(matchId: string): Promise<GameResult | null> {
    const response = await this.request(`/results/${encodeURIComponent(matchId)}`);
    if (response.status === 404) {
      return null;
    }
    return this.parse<GameResult>(response);
  }

  /** Historial del jugador, de la partida más reciente a la más antigua. */
  async history(userId: string, limit?: number, offset?: number): Promise<PlayerHistoryItem[]> {
    const query = new URLSearchParams();
    if (limit !== undefined) query.set('limit', String(limit));
    if (offset !== undefined) query.set('offset', String(offset));
    const suffix = query.size > 0 ? `?${query}` : '';

    return this.parse<PlayerHistoryItem[]>(await this.request(`/players/${encodeURIComponent(userId)}/history${suffix}`));
  }

  stats(userId: string): Promise<PlayerStats> {
    return this.request(`/players/${encodeURIComponent(userId)}/stats`).then((r) => this.parse<PlayerStats>(r));
  }

  private async request(path: string): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };

    const token = await this.options.getAccessToken?.();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    if (this.options.devUser) {
      headers['X-Dev-User'] = this.options.devUser;
    }

    const fetchFn = this.options.fetchFn ?? fetch;
    // Los ids de Auth0 llevan «|» (auth0|64f0c1...): por eso cada id se codifica con encodeURIComponent.
    return fetchFn(`${this.options.baseUrl}/api/games/typing${path}`, { headers });
  }

  private async parse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      throw new ApiError(response.status, describe(response.status));
    }
    return (await response.json()) as T;
  }
}

function describe(status: number): string {
  switch (status) {
    case 401:
      return 'No se pudo comprobar tu identidad. Vuelve a iniciar sesión.';
    case 403:
      return 'No tienes permiso para ver esto.';
    case 404:
      return 'No se encontró lo que buscabas.';
    default:
      return status >= 500 ? 'El servidor del juego tuvo un problema. Inténtalo de nuevo.' : `La solicitud falló (${status}).`;
  }
}
