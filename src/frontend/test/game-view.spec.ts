import { createFixture } from '@aurelia/testing';
import { runTasks } from 'aurelia';
import { describe, expect, it } from 'vitest';
import { TypingGameModule } from '../src/game-module';
import { TypingGameView } from '../src/game-view';
import { TypingHubClient } from '../src/services/hub-client';
import { CONTEXT, FakeConnection, NOW, player, snapshot } from './fakes';

/** Un módulo de verdad, con el hub falso, ya inicializado y conectado. */
async function connectedModule() {
  const connection = new FakeConnection();
  connection.responses.set('JoinGame', () => snapshot({ players: [player(), player({ userId: 'luis', displayName: 'Luis' })] }));
  const module = new TypingGameModule({ hubFactory: () => new TypingHubClient(connection) });
  await module.initialize(CONTEXT);
  await module.start();
  return { connection, module };
}

async function render(module: TypingGameModule) {
  const fixture = await createFixture(
    '<typing-game module.bind="module"></typing-game>',
    class {
      module = module;
    },
    [TypingGameView],
  ).started;
  return fixture;
}

describe('<typing-game> (elemento de Aurelia)', () => {
  it('se dibuja con el estado y los jugadores del controlador', async () => {
    const { module } = await connectedModule();

    const fixture = await render(module);

    const text = fixture.appHost.textContent ?? '';
    expect(text).toContain('estado: lobby');
    expect(text).toContain('Ana');
    expect(text).toContain('Luis');
    await fixture.stop(true);
  });

  it('no dibuja nada hasta que el módulo esté inicializado', async () => {
    const fixture = await render(new TypingGameModule());

    expect(fixture.appHost.querySelector('section')).toBeNull();
    await fixture.stop(true);
  });

  it('se actualiza solo cuando llegan eventos del servidor', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);

    connection.emit('GameStarted', {
      serverTime: NOW, startedAt: NOW, endsAt: '2026-09-02T20:01:00.000Z', timeLimitSeconds: 60, textId: 't-01', text: 'hola mundo',
    });
    runTasks();

    const text = fixture.appHost.textContent ?? '';
    expect(text).toContain('estado: running');
    expect(text).toContain('hola mundo');
    expect(fixture.appHost.querySelector('textarea')).not.toBeNull();
    await fixture.stop(true);
  });

  it('lo que se escribe en el área de texto llega al servidor', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);
    connection.emit('GameStarted', {
      serverTime: NOW, startedAt: NOW, endsAt: '2026-09-02T20:01:00.000Z', timeLimitSeconds: 60, textId: 't-01', text: 'hola mundo',
    });
    runTasks();

    fixture.type('textarea', 'hol');
    await Promise.resolve();

    expect(connection.calls('SubmitProgress')).toEqual([['hol']]);
    await fixture.stop(true);
  });

  it('muestra la clasificación cuando termina la partida', async () => {
    const { connection, module } = await connectedModule();
    const fixture = await render(module);

    connection.emit('GameFinished', {
      serverTime: NOW,
      finishedAt: NOW,
      winnerUserId: 'ana',
      resultSaved: true,
      standings: [{ rank: 1, userId: 'ana', displayName: 'Ana', wpm: 62.4, accuracy: 96.1, progress: 100, finished: true, score: 600 }],
    });
    runTasks();

    expect(fixture.appHost.textContent).toContain('1. Ana — 600 puntos');
    await fixture.stop(true);
  });
});
