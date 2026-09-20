import { afterEach, describe, expect, it } from 'vitest';
import { resolveConfig } from '../src/config';
import { ServerClock } from '../src/services/clock';

describe('resolveConfig', () => {
  afterEach(() => {
    delete (globalThis as { __TYPING_BATTLE_CONFIG__?: unknown }).__TYPING_BATTLE_CONFIG__;
  });

  it('usa los valores de compilación por defecto', () => {
    expect(resolveConfig()).toEqual({ apiBaseUrl: 'http://localhost:5080', hubPath: '/hubs/typing' });
  });

  it('permite cambiarla en tiempo de ejecución con la variable global', () => {
    (globalThis as { __TYPING_BATTLE_CONFIG__?: unknown }).__TYPING_BATTLE_CONFIG__ = { apiBaseUrl: 'https://api.ejemplo.cr' };

    expect(resolveConfig().apiBaseUrl).toBe('https://api.ejemplo.cr');
  });

  it('lo que pasa quien crea el módulo gana sobre todo lo demás', () => {
    (globalThis as { __TYPING_BATTLE_CONFIG__?: unknown }).__TYPING_BATTLE_CONFIG__ = { apiBaseUrl: 'https://global' };

    expect(resolveConfig({ apiBaseUrl: 'https://del-modulo' }).apiBaseUrl).toBe('https://del-modulo');
  });

  it('quita las barras finales de la URL', () => {
    expect(resolveConfig({ apiBaseUrl: 'https://api.ejemplo.cr///' }).apiBaseUrl).toBe('https://api.ejemplo.cr');
  });
});

describe('ServerClock', () => {
  it('corrige la diferencia entre el reloj local y el del servidor', () => {
    const clock = new ServerClock();
    const local = Date.parse('2026-09-02T20:00:00Z');

    clock.sync('2026-09-02T20:00:05Z', local); // el servidor va 5 s adelantado

    expect(clock.now() - Date.now()).toBeGreaterThanOrEqual(4990);
    expect(clock.now() - Date.now()).toBeLessThanOrEqual(5010);
  });

  it('ignora una hora inválida', () => {
    const clock = new ServerClock();

    clock.sync('no es una fecha');

    expect(Math.abs(clock.now() - Date.now())).toBeLessThan(50);
  });
});
