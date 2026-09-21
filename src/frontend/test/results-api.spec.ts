import { describe, expect, it, vi } from 'vitest';
import { ApiError, ResultsApi } from '../src/services/results-api';

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function setup(response: Response | (() => Response), options: { token?: string; devUser?: string } = {}) {
  const fetchFn = vi.fn(async () => (typeof response === 'function' ? response() : response));
  const api = new ResultsApi({
    baseUrl: 'http://localhost:5080',
    getAccessToken: options.token ? () => options.token : undefined,
    devUser: options.devUser,
    fetchFn: fetchFn as unknown as typeof fetch,
  });
  return { api, fetchFn };
}

const requested = (fetchFn: ReturnType<typeof vi.fn>) => fetchFn.mock.calls[0] as unknown as [string, RequestInit];

describe('ResultsApi', () => {
  it('pide el historial del jugador codificando su id (los de Auth0 llevan «|»)', async () => {
    const { api, fetchFn } = setup(json([]));

    await api.history('auth0|64f0c1');

    expect(requested(fetchFn)[0]).toBe('http://localhost:5080/api/games/typing/players/auth0%7C64f0c1/history');
  });

  it('agrega limit y offset a la consulta cuando se piden', async () => {
    const { api, fetchFn } = setup(json([]));

    await api.history('ana', 10, 20);

    expect(requested(fetchFn)[0]).toBe('http://localhost:5080/api/games/typing/players/ana/history?limit=10&offset=20');
  });

  it('devuelve el historial y las estadísticas tal como llegan', async () => {
    const item = { matchId: 'm1', score: 600, won: true };
    const stats = { userId: 'ana', gamesPlayed: 3, wins: 1 };

    expect(await setup(json([item])).api.history('ana')).toEqual([item]);
    expect(await setup(json(stats)).api.stats('ana')).toEqual(stats);
  });

  it('envía el token como Bearer', async () => {
    const { api, fetchFn } = setup(json({}), { token: 'abc.def.ghi' });

    await api.stats('ana');

    expect(requested(fetchFn)[1].headers).toMatchObject({ Authorization: 'Bearer abc.def.ghi' });
  });

  it('en desarrollo se identifica con X-Dev-User y no manda Authorization', async () => {
    const { api, fetchFn } = setup(json({}), { devUser: 'ana' });

    await api.stats('ana');

    const headers = requested(fetchFn)[1].headers as Record<string, string>;
    expect(headers['X-Dev-User']).toBe('ana');
    expect(headers.Authorization).toBeUndefined();
  });

  it('el resultado de una partida sin guardar es null, no un error', async () => {
    const { api } = setup(new Response(null, { status: 404 }));

    expect(await api.result('m1')).toBeNull();
  });

  it('un 401 se convierte en un error con un mensaje entendible', async () => {
    const { api } = setup(new Response(null, { status: 401 }));

    await expect(api.stats('ana')).rejects.toMatchObject({
      name: 'ApiError',
      status: 401,
      message: expect.stringContaining('identidad'),
    });
  });

  it('un error del servidor se informa con su código', async () => {
    const { api } = setup(new Response(null, { status: 500 }));

    const error = await api.history('ana').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(500);
  });
});
