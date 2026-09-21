import { describe, expect, it, vi } from 'vitest';
import { TypingHubClient } from '../src/services/hub-client';
import { FakeConnection, NOW, snapshot } from './fakes';

function setup() {
  const connection = new FakeConnection();
  return { connection, client: new TypingHubClient(connection) };
}

describe('TypingHubClient', () => {
  it('invoca los métodos del hub con los argumentos del contrato', async () => {
    const { connection, client } = setup();
    connection.responses.set('JoinGame', () => snapshot());

    const room = await client.join('match-001', 'Ana');
    await client.ready();
    await client.submit('hola');
    await client.leave();

    expect(room.matchId).toBe('match-001');
    expect(connection.calls('JoinGame')).toEqual([['match-001', 'Ana']]);
    expect(connection.calls('SetReady')).toEqual([[]]);
    expect(connection.calls('SubmitProgress')).toEqual([['hola']]);
    expect(connection.calls('LeaveGame')).toEqual([[]]);
  });

  it('envía siempre los dos argumentos de JoinGame, aunque no haya nombre', async () => {
    const { connection, client } = setup();

    await client.join('match-001', null);

    expect(connection.calls('JoinGame')).toEqual([['match-001', null]]);
  });

  it('entrega los eventos del servidor a quien se suscribió', () => {
    const { connection, client } = setup();
    const handler = vi.fn();
    client.on('GameStarted', handler);
    const payload = { serverTime: NOW, startedAt: NOW, endsAt: NOW, timeLimitSeconds: 60, textId: 't-01', text: 'hola' };

    connection.emit('GameStarted', payload);

    expect(handler).toHaveBeenCalledWith(payload);
  });

  it('cada evento llega solo a sus suscriptores', () => {
    const { connection, client } = setup();
    const started = vi.fn();
    const finished = vi.fn();
    client.on('GameStarted', started);
    client.on('GameFinished', finished);

    connection.emit('GameFinished', { serverTime: NOW, finishedAt: NOW, winnerUserId: null, standings: [], resultSaved: false });

    expect(started).not.toHaveBeenCalled();
    expect(finished).toHaveBeenCalledOnce();
  });

  it('permite cancelar una suscripción', () => {
    const { connection, client } = setup();
    const handler = vi.fn();
    const unsubscribe = client.on('RoomUpdated', handler);

    unsubscribe();
    connection.emit('RoomUpdated', snapshot());

    expect(handler).not.toHaveBeenCalled();
  });

  it('escucha los seis eventos del hub', () => {
    const { connection, client } = setup();
    const seen: string[] = [];
    for (const name of ['RoomUpdated', 'GameStarting', 'GameStarted', 'ProgressUpdated', 'PlayerFinished', 'GameFinished'] as const) {
      client.on(name, () => seen.push(name));
      connection.emit(name, {});
    }

    expect(seen).toHaveLength(6);
  });

  it('informa los cambios de la conexión', () => {
    const { connection, client } = setup();
    const events: string[] = [];
    client.onConnection('reconnecting', () => events.push('reconnecting'));
    client.onConnection('reconnected', () => events.push('reconnected'));
    client.onConnection('closed', () => events.push('closed'));

    connection.simulateReconnecting();
    connection.simulateReconnected();
    connection.simulateClosed();

    expect(events).toEqual(['reconnecting', 'reconnected', 'closed']);
  });

  it('conecta y desconecta', async () => {
    const { connection, client } = setup();

    await client.connect();
    await client.disconnect();

    expect(connection.started).toBe(true);
    expect(connection.stopped).toBe(true);
  });
});
