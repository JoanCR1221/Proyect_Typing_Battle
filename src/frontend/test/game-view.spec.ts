import { createFixture } from '@aurelia/testing';
import { runTasks } from 'aurelia';
import { describe, expect, it, vi } from 'vitest';
import { TypingGameModule } from '../src/game-module';
import { TypingGameView } from '../src/game-view';
import { TypingHubClient } from '../src/services/hub-client';
import type { ResultsApi } from '../src/services/results-api';
import { CONTEXT, FakeConnection, NOW, player, snapshot } from './fakes';

const LATER = '2026-09-02T20:01:00.000Z';

/** Un módulo de verdad, con el hub falso, ya inicializado y conectado. */
async function connectedModule(joinAs = snapshot({ players: [player(), player({ userId: 'luis', displayName: 'Luis' })] })) {
  const connection = new FakeConnection();
  connection.responses.set('JoinGame', () => joinAs);
  const module = new TypingGameModule({ hubFactory: () => new TypingHubClient(connection) });
  await module.initialize(CONTEXT);
  await module.start();
  return { connection, module };
}

async function render(module: TypingGameModule) {
  return createFixture(
    '<typing-game module.bind="module"></typing-game>',
    class {
      module = module;
    },
    [TypingGameView],
  ).started;
}

const start = (connection: FakeConnection) => {
  connection.emit('GameStarting', { serverTime: NOW, startsAt: NOW, countdownSeconds: 3 });
  connection.emit('GameStarted', { serverTime: NOW, startedAt: NOW, endsAt: LATER, timeLimitSeconds: 60, textId: 't-01', text: 'hola mundo' });
  runTasks();
};

const text = (fixture: { appHost: HTMLElement }) => fixture.appHost.textContent ?? '';

describe('<typing-game> (la interfaz completa)', () => {
  it('no dibuja nada hasta que el módulo esté inicializado', async () => {
    const fixture = await render(new TypingGameModule());

    expect(fixture.appHost.querySelector('.tb-app')).toBeNull();
    await fixture.stop(true);
  });

  it('en la sala de espera muestra el nombre del juego, la partida y a los jugadores', async () => {
    const { module } = await connectedModule();

    const fixture = await render(module);

    expect(text(fixture)).toContain('Typing Battle');
    expect(text(fixture)).toContain('Partida match-001');
    expect(text(fixture)).toContain('Sala de espera');
    expect(text(fixture)).toContain('Luis');
    expect(fixture.appHost.querySelector('.tb-timer')).toBeNull();
    await fixture.stop(true);
  });

  it('al empezar la cuenta regresiva muestra el número y los carriles', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);

    connection.emit('GameStarting', { serverTime: NOW, startsAt: new Date(Date.parse(NOW) + 3000).toISOString(), countdownSeconds: 3 });
    runTasks();

    expect(fixture.appHost.querySelector('.tb-countdown')).not.toBeNull();
    expect(fixture.appHost.querySelectorAll('.tb-lane')).toHaveLength(2);
    expect(fixture.appHost.querySelector('.tb-main--race')).not.toBeNull();
    await fixture.stop(true);
  });

  it('durante la carrera muestra el texto, el área de escritura, el temporizador y los números en vivo', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);

    start(connection);

    expect(fixture.appHost.querySelector('.tb-text')?.textContent).toBe('hola mundo');
    expect(fixture.appHost.querySelector('textarea')).not.toBeNull();
    expect(fixture.appHost.querySelector('[role="timer"]')?.textContent).toMatch(/\d:\d\d/);
    expect(text(fixture)).toContain('palabras por minuto');
    expect(fixture.appHost.querySelectorAll('.tb-lane')).toHaveLength(2);
    await fixture.stop(true);
  });

  it('lo que se escribe llega al servidor y el progreso de los demás actualiza los carriles', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);
    start(connection);

    const input = fixture.appHost.querySelector('textarea')!;
    input.value = 'hol';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await Promise.resolve();
    connection.emit('ProgressUpdated', {
      serverTime: NOW,
      players: [player({ progress: 30, wpm: 18 }), player({ userId: 'luis', displayName: 'Luis', progress: 60, wpm: 40 })],
    });
    runTasks();

    expect(connection.calls('SubmitProgress')).toEqual([['hol']]);
    const fills = [...fixture.appHost.querySelectorAll<HTMLElement>('.tb-lane-fill')].map((f) => f.style.width);
    expect(fills).toEqual(['30%', '60%']);
    await fixture.stop(true);
  });

  it('durante la carrera no se puede salir a «Mis resultados»', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);

    const mine = () => [...fixture.appHost.querySelectorAll<HTMLButtonElement>('.tb-nav-btn')].find((b) => b.textContent?.includes('Mis resultados'))!;
    expect(mine().disabled).toBe(false); // sala de espera: se puede

    start(connection);

    expect(mine().disabled).toBe(true);
    await fixture.stop(true);
  });

  it('al pausar avisa que la carrera sigue y permite continuar', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);
    start(connection);

    await module.pause();
    runTasks();
    expect(text(fixture)).toContain('La carrera sigue para los demás');
    expect(fixture.appHost.querySelector('textarea')!.disabled).toBe(true);

    fixture.appHost.querySelector<HTMLButtonElement>('.tb-pause button')!.click();
    runTasks();

    expect(module.controller?.paused).toBe(false);
    expect(fixture.appHost.querySelector('.tb-pause')).toBeNull();
    await fixture.stop(true);
  });

  it('al terminar muestra el resultado y «Mis resultados» carga el historial', async () => {
    const { connection, module } = await connectedModule();
    const api = {
      history: vi.fn(async () => []),
      stats: vi.fn(async () => ({ userId: 'ana', gamesPlayed: 0, wins: 0, winRate: 0, averageScore: 0, bestScore: 0, averageWpm: null, bestWpm: null, averageAccuracy: null, lastPlayedAt: null })),
    };
    Object.defineProperty(module.controller!, 'resultsApi', { get: () => api as unknown as ResultsApi });
    const fixture = await render(module);
    start(connection);

    connection.emit('GameFinished', {
      serverTime: NOW,
      finishedAt: NOW,
      winnerUserId: 'ana',
      resultSaved: true,
      standings: [{ rank: 1, userId: 'ana', displayName: 'Ana', wpm: 62.4, accuracy: 96.1, progress: 100, finished: true, score: 600 }],
    });
    runTasks();
    expect(text(fixture)).toContain('¡Ganaste!');
    expect(fixture.appHost.querySelector('.tb-timer')).toBeNull();

    [...fixture.appHost.querySelectorAll<HTMLButtonElement>('.tb-nav-btn')].find((b) => b.textContent?.includes('Mis resultados'))!.click();
    runTasks();

    await vi.waitFor(() => expect(text(fixture)).toContain('Todavía no has jugado'));
    expect(api.history).toHaveBeenCalledWith('ana', 20);
    await fixture.stop(true);
  });

  it('si algo falla muestra el error y «Reintentar» vuelve a conectar', async () => {
    const connection = new FakeConnection();
    connection.failOn = 'start';
    const module = new TypingGameModule({ hubFactory: () => new TypingHubClient(connection) });
    await module.initialize(CONTEXT);
    await module.start();
    const fixture = await render(module);

    expect(fixture.appHost.querySelector('[role="alert"]')?.textContent).toContain('No se pudo conectar con el servidor del juego');

    connection.failOn = null;
    connection.responses.set('JoinGame', () => snapshot());
    fixture.appHost.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    await vi.waitFor(() => expect(module.controller?.phase).toBe('lobby'));
    runTasks();

    expect(text(fixture)).toContain('Sala de espera');
    await fixture.stop(true);
  });

  it('si se cae la conexión a mitad de una carrera lo indica en el encabezado', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);
    start(connection);

    connection.simulateReconnecting();
    runTasks();

    expect(text(fixture)).toContain('Reconectando');
    await fixture.stop(true);
  });
});
