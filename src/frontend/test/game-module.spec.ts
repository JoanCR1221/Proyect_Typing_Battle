import { describe, expect, it, vi } from 'vitest';
import type { GameModule } from '../src/contracts/game-module';
import { TypingGameModule } from '../src/game-module';
import { TypingHubClient } from '../src/services/hub-client';
import { CONTEXT, FakeConnection, snapshot } from './fakes';

function setup() {
  const connection = new FakeConnection();
  connection.responses.set('JoinGame', () => snapshot());
  const hubFactory = vi.fn(() => new TypingHubClient(connection));
  const module = new TypingGameModule({ hubFactory });
  return { connection, hubFactory, module };
}

describe('TypingGameModule (contrato GameModule)', () => {
  it('cumple la interfaz de ciclo de vida que espera el Shell', () => {
    const module: GameModule = new TypingGameModule();

    expect(typeof module.initialize).toBe('function');
    expect(typeof module.start).toBe('function');
    expect(typeof module.pause).toBe('function');
    expect(typeof module.dispose).toBe('function');
  });

  describe('initialize', () => {
    it('prepara el estado pero NO conecta todavía', async () => {
      const { connection, hubFactory, module } = setup();

      await module.initialize(CONTEXT);

      expect(module.controller).not.toBeNull();
      expect(module.controller?.phase).toBe('idle');
      expect(connection.started).toBe(false);
      expect(hubFactory).toHaveBeenCalledWith('http://localhost:5080/hubs/typing', CONTEXT);
    });

    it('usa la URL de la API que entregue el Shell en el contexto', async () => {
      const { hubFactory, module } = setup();

      await module.initialize({ ...CONTEXT, apiBaseUrl: 'https://api.ejemplo.cr/' });

      expect(hubFactory).toHaveBeenCalledWith('https://api.ejemplo.cr/hubs/typing', expect.anything());
    });

    it.each([
      ['sin matchId', { ...CONTEXT, matchId: '  ' }, 'matchId'],
      ['sin id de usuario', { ...CONTEXT, currentUser: { id: '', displayName: 'Ana' } }, 'currentUser.id'],
      ['de otro juego', { ...CONTEXT, gameType: 'trivia' }, "'trivia'"],
    ])('rechaza un contexto %s', async (_caso, context, texto) => {
      const { module } = setup();

      await expect(module.initialize(context)).rejects.toThrow(texto);
      expect(module.controller).toBeNull();
    });

    it('inicializar de nuevo libera la partida anterior', async () => {
      const { connection, module } = setup();
      await module.initialize(CONTEXT);
      await module.start();

      await module.initialize({ ...CONTEXT, matchId: 'match-002' });

      expect(connection.stopped).toBe(true);
      expect(module.controller?.phase).toBe('idle');
    });
  });

  describe('start', () => {
    it('conecta y entra a la sala', async () => {
      const { connection, module } = setup();
      await module.initialize(CONTEXT);

      await module.start();

      expect(connection.started).toBe(true);
      expect(connection.calls('JoinGame')).toEqual([['match-001', 'Ana']]);
      expect(module.controller?.phase).toBe('lobby');
    });

    it('antes de initialize da un error que explica qué hacer', async () => {
      const { module } = setup();

      await expect(module.start()).rejects.toThrow('initialize');
    });
  });

  describe('pause', () => {
    it('pausa la entrada local; start la reanuda sin volver a conectar', async () => {
      const { connection, module } = setup();
      await module.initialize(CONTEXT);
      await module.start();

      await module.pause();
      expect(module.controller?.paused).toBe(true);

      await module.start();
      expect(module.controller?.paused).toBe(false);
      expect(connection.calls('JoinGame')).toHaveLength(1);
    });

    it('antes de initialize da un error que explica qué hacer', async () => {
      await expect(setup().module.pause()).rejects.toThrow('initialize');
    });
  });

  describe('dispose', () => {
    it('desconecta del hub y suelta el controlador', async () => {
      const { connection, module } = setup();
      await module.initialize(CONTEXT);
      await module.start();

      await module.dispose();

      expect(connection.calls('LeaveGame')).toHaveLength(1);
      expect(connection.stopped).toBe(true);
      expect(module.controller).toBeNull();
    });

    it('se puede llamar varias veces y antes de initialize', async () => {
      const { module } = setup();

      await module.dispose();
      await module.initialize(CONTEXT);
      await module.dispose();
      await module.dispose();

      expect(module.controller).toBeNull();
    });
  });
});
