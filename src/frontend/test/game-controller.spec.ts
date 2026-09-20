import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { describeError, GameController, standingsFromResult } from '../src/game-controller';
import type { GameResult } from '../src/contracts/results';
import type { ResultsApi } from '../src/services/results-api';
import { TypingHubClient } from '../src/services/hub-client';
import { CONTEXT, FakeConnection, NOW, player, snapshot } from './fakes';

const T0 = Date.parse(NOW);
const iso = (secondsFromStart: number) => new Date(T0 + secondsFromStart * 1000).toISOString();

function setup(options: { autoReady?: boolean; joinAs?: ReturnType<typeof snapshot> } = {}) {
  const connection = new FakeConnection();
  connection.responses.set('JoinGame', () => options.joinAs ?? snapshot());
  const result = vi.fn<ResultsApi['result']>(async () => null);
  const api = { result } as unknown as ResultsApi;
  const controller = new GameController({
    context: CONTEXT,
    hub: new TypingHubClient(connection),
    api,
    tickMs: 100,
    autoReady: options.autoReady,
  });
  return { connection, controller, result };
}

/** Conecta y deja la carrera en curso con este texto. */
async function startRace(text = 'hola mundo') {
  const ctx = setup();
  await ctx.controller.connect();
  ctx.connection.emit('GameStarting', { serverTime: iso(0), startsAt: iso(3), countdownSeconds: 3 });
  ctx.connection.emit('GameStarted', {
    serverTime: iso(3),
    startedAt: iso(3),
    endsAt: iso(63),
    timeLimitSeconds: 60,
    textId: 't-01',
    text,
  });
  return ctx;
}

describe('GameController', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(T0));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('conexión y sala de espera', () => {
    it('conecta, entra a la sala con el nombre del contexto y se marca como listo', async () => {
      const { connection, controller } = setup();

      await controller.connect();

      expect(connection.started).toBe(true);
      expect(connection.calls('JoinGame')).toEqual([['match-001', 'Ana']]);
      expect(connection.calls('SetReady')).toHaveLength(1);
      expect(controller.phase).toBe('lobby');
      expect(controller.players.map((p) => p.userId)).toEqual(['ana']);
      expect(controller.minPlayers).toBe(2);
    });

    it('no se marca como listo si autoReady está desactivado', async () => {
      const { connection, controller } = setup({ autoReady: false });

      await controller.connect();

      expect(connection.calls('SetReady')).toHaveLength(0);
    });

    it('no se marca como listo si la partida ya estaba en marcha', async () => {
      const running = snapshot({ state: 'running', text: 'hola', textId: 't-01', myTyped: 'ho', endsAt: iso(60) });
      const { connection, controller } = setup({ joinAs: running });

      await controller.connect();

      expect(connection.calls('SetReady')).toHaveLength(0);
      expect(controller.phase).toBe('running');
      expect(controller.typed).toBe('ho'); // recupera lo que llevaba escrito
      expect(controller.text).toBe('hola');
    });

    it('conectar dos veces no entra dos veces a la sala', async () => {
      const { connection, controller } = setup();

      await controller.connect();
      await controller.connect();

      expect(connection.calls('JoinGame')).toHaveLength(1);
    });

    it('identifica al jugador local en la lista', async () => {
      const { controller, connection } = setup();
      await controller.connect();

      connection.emit('RoomUpdated', snapshot({ players: [player({ userId: 'luis', displayName: 'Luis' }), player()] }));

      expect(controller.me?.displayName).toBe('Ana');
    });

    it('refleja los cambios de la sala de espera', async () => {
      const { controller, connection } = setup();
      await controller.connect();

      connection.emit('RoomUpdated', snapshot({ players: [player(), player({ userId: 'luis', displayName: 'Luis' })] }));

      expect(controller.players.map((p) => p.displayName)).toEqual(['Ana', 'Luis']);
    });
  });

  describe('cuenta regresiva y carrera', () => {
    it('muestra la cuenta regresiva y la va bajando con el reloj', async () => {
      const { controller, connection } = setup();
      await controller.connect();

      connection.emit('GameStarting', { serverTime: iso(0), startsAt: iso(3), countdownSeconds: 3 });
      expect(controller.phase).toBe('countdown');
      expect(controller.countdownSeconds).toBe(3);

      await vi.advanceTimersByTimeAsync(1000);
      expect(controller.countdownSeconds).toBe(2);
    });

    it('si alguien se va, la cuenta regresiva se cancela y vuelve a la sala de espera', async () => {
      const { controller, connection } = setup();
      await controller.connect();
      connection.emit('GameStarting', { serverTime: iso(0), startsAt: iso(3), countdownSeconds: 3 });

      connection.emit('RoomUpdated', snapshot({ state: 'waiting' }));

      expect(controller.phase).toBe('lobby');
      expect(controller.countdownSeconds).toBe(0);
    });

    it('al empezar recibe el texto y los segundos que quedan', async () => {
      const { controller } = await startRace('hola mundo');

      expect(controller.phase).toBe('running');
      expect(controller.text).toBe('hola mundo');
      expect(controller.textId).toBe('t-01');
      expect(controller.typed).toBe('');
      expect(controller.timeLeftSeconds).toBe(60);

      vi.setSystemTime(new Date(T0 + 10_000));
      await vi.advanceTimersByTimeAsync(100);
      expect(controller.timeLeftSeconds).toBe(50);
    });

    it('usa la hora del servidor, no la del navegador, para los temporizadores', async () => {
      const { controller, connection } = setup();
      await controller.connect();

      // El servidor dice que son las 20:00:00 y el navegador va 30 s adelantado.
      vi.setSystemTime(new Date(T0 + 30_000));
      connection.emit('GameStarted', {
        serverTime: iso(0), startedAt: iso(0), endsAt: iso(60), timeLimitSeconds: 60, textId: 't', text: 'hola',
      });

      expect(controller.timeLeftSeconds).toBe(60);
    });

    it('actualiza a los jugadores con el progreso en vivo', async () => {
      const { controller, connection } = await startRace();

      connection.emit('ProgressUpdated', {
        serverTime: iso(10),
        players: [player({ progress: 40, wpm: 55.5 }), player({ userId: 'luis', displayName: 'Luis', progress: 70 })],
      });

      expect(controller.players.map((p) => p.progress)).toEqual([40, 70]);
    });
  });

  describe('lo que escribe el jugador', () => {
    it('lo envía al servidor durante la carrera', async () => {
      const { controller, connection } = await startRace();

      controller.submitTyped('hol');
      await vi.advanceTimersByTimeAsync(0);

      expect(controller.typed).toBe('hol');
      expect(connection.calls('SubmitProgress')).toEqual([['hol']]);
    });

    it('no envía nada fuera de la carrera', async () => {
      const { controller, connection } = setup();
      await controller.connect();

      controller.submitTyped('hola');

      expect(connection.calls('SubmitProgress')).toHaveLength(0);
    });

    it('no envía nada mientras está en pausa, y vuelve a enviar al reanudar', async () => {
      const { controller, connection } = await startRace();

      controller.pause();
      controller.submitTyped('hol');
      expect(connection.calls('SubmitProgress')).toHaveLength(0);

      controller.resume();
      controller.submitTyped('hola');
      await vi.advanceTimersByTimeAsync(0);
      expect(connection.calls('SubmitProgress')).toEqual([['hola']]);
    });

    it('si hay un envío en curso, guarda solo el último texto en lugar de enviar todos los intermedios', async () => {
      const { controller, connection } = await startRace();
      let release!: () => void;
      connection.responses.set('SubmitProgress', () => new Promise<void>((resolve) => (release = resolve)));

      controller.submitTyped('h');
      controller.submitTyped('ho');
      controller.submitTyped('hol');
      expect(connection.calls('SubmitProgress')).toEqual([['h']]); // el primero sigue en vuelo

      connection.responses.set('SubmitProgress', () => undefined);
      release();
      await vi.advanceTimersByTimeAsync(0);

      expect(connection.calls('SubmitProgress')).toEqual([['h'], ['hol']]); // «ho» se salteó
    });
  });

  describe('final de la partida', () => {
    it('muestra la clasificación y el ganador, y detiene los temporizadores', async () => {
      const { controller, connection } = await startRace();
      const standings = [
        { rank: 1, userId: 'ana', displayName: 'Ana', wpm: 62.4, accuracy: 96.1, progress: 100, finished: true, score: 600 },
        { rank: 2, userId: 'luis', displayName: 'Luis', wpm: 40, accuracy: 90, progress: 80, finished: false, score: 360 },
      ];

      connection.emit('GameFinished', { serverTime: iso(40), finishedAt: iso(40), winnerUserId: 'ana', standings, resultSaved: true });

      expect(controller.phase).toBe('finished');
      expect(controller.standings).toEqual(standings);
      expect(controller.winnerUserId).toBe('ana');
      expect(controller.resultSaved).toBe(true);
      expect(controller.timeLeftSeconds).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
    });

    it('la lista de jugadores toma los valores finales de la clasificación', async () => {
      const { controller, connection } = await startRace();
      connection.emit('ProgressUpdated', { serverTime: iso(30), players: [player({ progress: 96.1, wpm: 38 })] });

      connection.emit('GameFinished', {
        serverTime: iso(40),
        finishedAt: iso(40),
        winnerUserId: 'ana',
        resultSaved: true,
        standings: [{ rank: 1, userId: 'ana', displayName: 'Ana', wpm: 39.3, accuracy: 99.2, progress: 100, finished: true, score: 390 }],
      });

      expect(controller.players[0]).toMatchObject({ progress: 100, wpm: 39.3, accuracy: 99.2, finished: true, rank: 1 });
    });

    it('después de terminar ignora las actualizaciones de la sala', async () => {
      const { controller, connection } = await startRace();
      connection.emit('GameFinished', { serverTime: iso(40), finishedAt: iso(40), winnerUserId: null, standings: [], resultSaved: false });

      connection.emit('RoomUpdated', snapshot({ state: 'waiting', players: [] }));

      expect(controller.phase).toBe('finished');
    });

    it('si vuelve cuando la partida ya terminó, reconstruye la clasificación con el resultado guardado', async () => {
      const finished = snapshot({ state: 'finished' });
      const { controller, result } = setup({ joinAs: finished });
      const saved: GameResult = {
        matchId: 'match-001',
        gameType: 'typing',
        players: [
          { userId: 'ana', displayName: 'Ana', score: 600 },
          { userId: 'luis', displayName: 'Luis', score: 360 },
        ],
        startedAt: iso(0),
        finishedAt: iso(40),
        winnerUserId: 'ana',
        metadata: { players: [{ userId: 'ana', wpm: 62.4, accuracy: 96.1, progress: 100, finished: true }] },
      };
      result.mockResolvedValue(saved);

      await controller.connect();
      await vi.advanceTimersByTimeAsync(0);

      expect(controller.phase).toBe('finished');
      expect(controller.winnerUserId).toBe('ana');
      expect(controller.standings.map((s) => [s.rank, s.userId, s.score])).toEqual([[1, 'ana', 600], [2, 'luis', 360]]);
      expect(controller.standings[0].wpm).toBe(62.4);
    });
  });

  describe('conexión inestable', () => {
    it('al reconectar vuelve a entrar a la sala y recupera lo escrito', async () => {
      const { controller, connection } = await startRace();
      controller.submitTyped('hol');
      await vi.advanceTimersByTimeAsync(0);

      connection.simulateReconnecting();
      expect(controller.reconnecting).toBe(true);

      connection.responses.set('JoinGame', () =>
        snapshot({ state: 'running', text: 'hola mundo', textId: 't-01', myTyped: 'hol', endsAt: iso(63), serverTime: iso(5) }));
      connection.simulateReconnected();
      await vi.advanceTimersByTimeAsync(0);

      expect(controller.reconnecting).toBe(false);
      expect(connection.calls('JoinGame')).toHaveLength(2);
      expect(controller.phase).toBe('running');
      expect(controller.typed).toBe('hol');
    });

    it('si la conexión se pierde del todo, pasa a error con un mensaje claro', async () => {
      const { controller, connection } = await startRace();

      connection.simulateClosed();

      expect(controller.phase).toBe('error');
      expect(controller.error).toContain('Se perdió la conexión');
      expect(vi.getTimerCount()).toBe(0);
    });

    it('cerrar la conexión después de terminar no es un error', async () => {
      const { controller, connection } = await startRace();
      connection.emit('GameFinished', { serverTime: iso(40), finishedAt: iso(40), winnerUserId: null, standings: [], resultSaved: false });

      connection.simulateClosed();

      expect(controller.phase).toBe('finished');
      expect(controller.error).toBeNull();
    });
  });

  describe('errores al conectar', () => {
    it('muestra el mensaje que el servidor lanzó a propósito', async () => {
      const { controller, connection } = setup();
      connection.failOn = 'JoinGame';

      await controller.connect();

      expect(controller.phase).toBe('error');
      expect(controller.error).toBe('La partida ya comenzó.');
    });

    it('si no se puede conectar, lo dice sin mostrar el error técnico', async () => {
      const { controller, connection } = setup();
      connection.failOn = 'start';

      await controller.connect();

      expect(controller.phase).toBe('error');
      expect(controller.error).toBe('No se pudo conectar con el servidor del juego.');
    });

    it('después de un error se puede volver a conectar', async () => {
      const { controller, connection } = setup();
      connection.failOn = 'start';
      await controller.connect();

      connection.failOn = null;
      await controller.connect();

      expect(controller.phase).toBe('lobby');
      expect(controller.error).toBeNull();
    });
  });

  describe('dispose', () => {
    it('sale de la sala, desconecta y deja de escuchar al servidor', async () => {
      const { controller, connection } = await startRace();

      await controller.dispose();
      connection.emit('ProgressUpdated', { serverTime: iso(9), players: [player({ progress: 99 })] });

      expect(connection.calls('LeaveGame')).toHaveLength(1);
      expect(connection.stopped).toBe(true);
      expect(controller.phase).toBe('closed');
      expect(controller.players[0].progress).toBe(0); // ignoró el evento de progreso (99 %) posterior a dispose
      expect(vi.getTimerCount()).toBe(0);
    });

    it('si nunca se conectó, no toca el hub', async () => {
      const { controller, connection } = setup();

      await controller.dispose();

      expect(connection.invocations).toHaveLength(0);
      expect(connection.stopped).toBe(false);
      expect(controller.phase).toBe('closed');
    });
  });
});

describe('describeError', () => {
  it('extrae el mensaje de un HubException del servidor', () => {
    const error = new Error("An unexpected error occurred invoking 'JoinGame' on the server. HubException: La sala está llena.");

    expect(describeError(error)).toBe('La sala está llena.');
  });

  it('traduce un 401 y los fallos de red', () => {
    expect(describeError(new Error("Failed to complete negotiation with the server: Error: Unauthorized: Status code '401'"))).toContain('identidad');
    expect(describeError(new Error('Failed to start the connection: TypeError: Failed to fetch'))).toContain('No se pudo conectar');
  });

  it('deja pasar otros mensajes y nunca devuelve algo vacío', () => {
    expect(describeError(new Error('algo raro'))).toBe('algo raro');
    expect(describeError('texto suelto')).toBe('texto suelto');
    expect(describeError(new Error(''))).toBe('Ocurrió un error inesperado.');
  });
});

describe('standingsFromResult', () => {
  it('numera según el orden guardado y completa lo que falta con ceros', () => {
    const standings = standingsFromResult({
      matchId: 'm',
      gameType: 'typing',
      players: [{ userId: 'a', displayName: 'A', score: 10 }, { userId: 'b', displayName: 'B', score: 5 }],
      startedAt: iso(0),
      finishedAt: iso(1),
      winnerUserId: 'a',
      metadata: { players: [{ userId: 'a', wpm: 30, accuracy: 99, progress: 100, finished: true }, 'basura', null] },
    });

    expect(standings).toEqual([
      { rank: 1, userId: 'a', displayName: 'A', wpm: 30, accuracy: 99, progress: 100, finished: true, score: 10 },
      { rank: 2, userId: 'b', displayName: 'B', wpm: 0, accuracy: 0, progress: 0, finished: false, score: 5 },
    ]);
  });
});
