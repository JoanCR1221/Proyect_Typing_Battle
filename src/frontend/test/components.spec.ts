import { createFixture } from '@aurelia/testing';
import { runTasks } from 'aurelia';
import { describe, expect, it, vi } from 'vitest';
import { TbCountdown } from '../src/components/tb-countdown';
import { TbLobby } from '../src/components/tb-lobby';
import { rankPlayers, TbRaceLanes } from '../src/components/tb-race-lanes';
import { describeOutcome, TbResults } from '../src/components/tb-results';
import { TbResultsPanel, toHistoryRow, toStatLines } from '../src/components/tb-results-panel';
import { TbTypingArea } from '../src/components/tb-typing-area';
import type { Standing } from '../src/contracts/hub';
import type { PlayerHistoryItem, PlayerStats } from '../src/contracts/results';
import type { ResultsApi } from '../src/services/results-api';
import { player } from './fakes';

const standing = (overrides: Partial<Standing> = {}): Standing => ({
  rank: 1, userId: 'ana', displayName: 'Ana', wpm: 61.5, accuracy: 96.1, progress: 100, finished: true, score: 615, ...overrides,
});

describe('<tb-typing-area>', () => {
  async function render(props: { text?: string; typed?: string; disabled?: boolean } = {}) {
    const onTyped = vi.fn();
    const fixture = await createFixture(
      '<tb-typing-area text.bind="text" typed.bind="typed" disabled.bind="disabled" on-typed.bind="onTyped"></tb-typing-area>',
      class {
        text = props.text ?? 'hola mundo';
        typed = props.typed ?? '';
        disabled = props.disabled ?? false;
        onTyped = onTyped;
      },
      [TbTypingArea],
    ).started;
    const input = fixture.appHost.querySelector('textarea')!;
    const states = () => [...fixture.appHost.querySelectorAll('.tb-ch')].map((c) => c.className.replace('tb-ch tb-ch--', '')[0]).join('');
    const type = (value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      runTasks();
    };
    return { fixture, input, onTyped, states, type };
  }

  it('dibuja un elemento por carácter, con el primero como el actual', async () => {
    const { fixture, states } = await render();

    expect(fixture.appHost.querySelectorAll('.tb-ch')).toHaveLength(10);
    expect(fixture.appHost.querySelector('.tb-text')?.textContent).toBe('hola mundo');
    expect(states()).toBe('cppppppppp');
    await fixture.stop(true);
  });

  it('al escribir pinta lo correcto, lo equivocado y el siguiente, e informa lo escrito', async () => {
    const { fixture, onTyped, states, type } = await render();

    type('hox');

    expect(states()).toBe('ddwcpppppp'); // «h» y «o» bien, «x» mal, «a» es la siguiente
    expect(onTyped).toHaveBeenLastCalledWith('hox');
    await fixture.stop(true);
  });

  it('recupera lo escrito cuando cambia desde afuera (por ejemplo, tras reconectar)', async () => {
    const { fixture, input, states } = await render();

    (fixture.component as unknown as { typed: string }).typed = 'hola';
    runTasks();

    expect(input.value).toBe('hola');
    expect(states()).toBe('ddddcppppp');
    await fixture.stop(true);
  });

  it('cancela Enter, pegar y arrastrar, pero deja pasar las teclas normales y borrar', async () => {
    const { fixture, input } = await render();
    const prevented = (event: Event) => {
      input.dispatchEvent(event);
      return event.defaultPrevented;
    };

    expect(prevented(new KeyboardEvent('keydown', { key: 'Enter', cancelable: true, bubbles: true }))).toBe(true);
    expect(prevented(new Event('paste', { cancelable: true, bubbles: true }))).toBe(true);
    expect(prevented(new Event('drop', { cancelable: true, bubbles: true }))).toBe(true);
    expect(prevented(new KeyboardEvent('keydown', { key: 'a', cancelable: true, bubbles: true }))).toBe(false);
    expect(prevented(new KeyboardEvent('keydown', { key: 'Backspace', cancelable: true, bubbles: true }))).toBe(false);
    await fixture.stop(true);
  });

  it('tiene una etiqueta accesible, está asociado al texto y limita el largo al del texto', async () => {
    const { fixture, input } = await render();

    expect(input.getAttribute('aria-label')).toContain('Escribe');
    expect(input.getAttribute('aria-describedby')).toBe('tb-text');
    expect(input.getAttribute('maxlength')).toBe('10');
    await fixture.stop(true);
  });

  it('recibe el foco cuando la carrera está en marcha y no cuando está en pausa', async () => {
    const running = await render();
    await Promise.resolve();
    expect(document.activeElement).toBe(running.input);
    await running.fixture.stop(true);

    const paused = await render({ disabled: true });
    expect(paused.input.disabled).toBe(true);
    expect(paused.fixture.appHost.querySelector('.tb-typing--disabled')).not.toBeNull();
    await paused.fixture.stop(true);
  });
});

describe('carriles de progreso', () => {
  it('el puesto pone primero a quienes terminaron por orden de llegada y luego al resto por avance', () => {
    const positions = rankPlayers([
      player({ userId: 'a', progress: 80 }),
      player({ userId: 'b', progress: 100, finished: true, rank: 2 }),
      player({ userId: 'c', progress: 100, finished: true, rank: 1 }),
      player({ userId: 'd', progress: 95 }),
    ]);

    expect([...positions.entries()].sort(([, x], [, y]) => x - y).map(([id]) => id)).toEqual(['c', 'b', 'd', 'a']);
  });

  async function render(players = [player({ userId: 'ana', displayName: 'Ana', progress: 40, wpm: 55.5, accuracy: 97 }), player({ userId: 'luis', displayName: 'Luis' })]) {
    const fixture = await createFixture(
      '<tb-race-lanes players.bind="players" me-id.bind="me"></tb-race-lanes>',
      class {
        players = players;
        me = 'ana';
      },
      [TbRaceLanes],
    ).started;
    return fixture;
  }

  it('muestra cada jugador con su puesto, «Tú» en el propio y una barra con el avance', async () => {
    const fixture = await render();

    const lanes = fixture.appHost.querySelectorAll('.tb-lane');
    expect(lanes).toHaveLength(2);
    expect(lanes[0].textContent).toContain('1.º');
    expect(lanes[0].textContent).toContain('Ana');
    expect(lanes[0].querySelector('.tb-badge')?.textContent).toBe('Tú');
    expect(lanes[1].querySelector('.tb-badge')).toBeNull();

    const bar = lanes[0].querySelector('[role="progressbar"]')!;
    expect(bar.getAttribute('aria-valuenow')).toBe('40');
    expect(bar.getAttribute('aria-label')).toBe('Avance de Ana');
    expect(lanes[0].querySelector<HTMLElement>('.tb-lane-fill')!.style.width).toBe('40%');
    await fixture.stop(true);
  });

  it('al llegar progreso nuevo actualiza la barra sin recrearla (así se anima)', async () => {
    const fixture = await render();
    const fill = fixture.appHost.querySelector<HTMLElement>('.tb-lane-fill')!;

    (fixture.component as unknown as { players: unknown }).players = [
      player({ userId: 'ana', displayName: 'Ana', progress: 75 }),
      player({ userId: 'luis', displayName: 'Luis' }),
    ];
    runTasks();

    expect(fixture.appHost.querySelector('.tb-lane-fill')).toBe(fill);
    expect(fill.style.width).toBe('75%');
    await fixture.stop(true);
  });

  it('marca a quien terminó y a quien perdió la conexión', async () => {
    const fixture = await render([
      player({ userId: 'ana', displayName: 'Ana', finished: true, rank: 1, progress: 100 }),
      player({ userId: 'luis', displayName: 'Luis', connected: false }),
    ]);

    const text = fixture.appHost.textContent ?? '';
    expect(text).toContain('Terminó');
    expect(text).toContain('Sin conexión');
    await fixture.stop(true);
  });
});

describe('<tb-lobby>', () => {
  async function render(count: number, min = 2) {
    const players = Array.from({ length: count }, (_, i) => player({ userId: `u${i}`, displayName: `Jugador ${i}`, ready: i === 0 }));
    return createFixture(
      '<tb-lobby players.bind="players" min-players.bind="min" max-players.bind="10" me-id.bind="\'u0\'"></tb-lobby>',
      class {
        players = players;
        min = min;
      },
      [TbLobby],
    ).started;
  }

  it('dice cuántos jugadores faltan, en singular y en plural', async () => {
    const one = await render(1, 2);
    expect(one.appHost.textContent).toContain('Falta 1 jugador para poder empezar (mínimo 2)');
    await one.stop(true);

    const many = await render(1, 4);
    expect(many.appHost.textContent).toContain('Faltan 3 jugadores');
    await many.stop(true);
  });

  it('cuando ya son suficientes lo dice y lista a todos con su estado', async () => {
    const fixture = await render(3, 2);

    const text = fixture.appHost.textContent ?? '';
    expect(text).toContain('Ya son suficientes');
    expect(fixture.appHost.querySelectorAll('.tb-lobby-item')).toHaveLength(3);
    expect(text).toContain('Listo');
    expect(text).toContain('Conectándose');
    expect(text).toContain('3 de 10 lugares');
    await fixture.stop(true);
  });
});

describe('<tb-countdown>', () => {
  it('muestra los segundos que faltan y se actualiza', async () => {
    const fixture = await createFixture(
      '<tb-countdown seconds.bind="seconds"></tb-countdown>',
      class {
        seconds = 3;
      },
      [TbCountdown],
    ).started;
    expect(fixture.appHost.querySelector('.tb-countdown-number')?.textContent).toBe('3');

    (fixture.component as unknown as { seconds: number }).seconds = 2;
    runTasks();

    expect(fixture.appHost.querySelector('.tb-countdown-number')?.textContent).toBe('2');
    expect(fixture.appHost.querySelector('[aria-live]')).not.toBeNull();
    await fixture.stop(true);
  });
});

describe('resultado de la carrera', () => {
  const both = [standing(), standing({ rank: 2, userId: 'luis', displayName: 'Luis', score: 390, wpm: 39.3 })];

  it('describe el resultado según cómo le fue al jugador local', () => {
    expect(describeOutcome(both, 'ana', 'ana')).toEqual({ headline: '¡Ganaste!', detail: 'Terminaste primero con 615 puntos.' });
    expect(describeOutcome(both, 'ana', 'luis')).toEqual({ headline: 'Quedaste 2.º de 2', detail: 'Ganó Ana con 615 puntos.' });
    expect(describeOutcome(both, 'ana', 'otro')).toEqual({ headline: 'Terminó la carrera', detail: 'Ganó Ana.' });
    expect(describeOutcome(both, null, 'ana').headline).toBe('Nadie escribió nada');
    expect(describeOutcome([], null, 'ana').headline).toBe('Nadie escribió nada');
  });

  async function render(saved = true, standings = both) {
    return createFixture(
      '<tb-results standings.bind="standings" winner-user-id.bind="winner" me-id.bind="me" result-saved.bind="saved"></tb-results>',
      class {
        standings = standings;
        winner = 'ana';
        me = 'luis';
        saved = saved;
      },
      [TbResults],
    ).started;
  }

  it('muestra el titular y la clasificación con la fila propia resaltada', async () => {
    const fixture = await render();

    expect(fixture.appHost.querySelector('h2')?.textContent).toBe('Quedaste 2.º de 2');
    const rows = fixture.appHost.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('61,5 ppm');
    expect(rows[0].querySelector('.tb-win')).not.toBeNull(); // el ganador lleva trofeo
    expect(rows[1].classList.contains('tb-row--me')).toBe(true);
    expect(rows[1].textContent).toContain('Tú');
    expect(fixture.appHost.querySelector('[role="status"]')).toBeNull(); // se guardó: sin aviso
    await fixture.stop(true);
  });

  it('avisa si el resultado no se pudo guardar', async () => {
    const fixture = await render(false);

    expect(fixture.appHost.querySelector('[role="status"]')?.textContent).toContain('no aparecerá en tu historial');
    await fixture.stop(true);
  });

  it('a quien no terminó le muestra su avance en lugar de «Terminó»', async () => {
    const fixture = await render(true, [standing(), standing({ rank: 2, userId: 'luis', displayName: 'Luis', finished: false, progress: 42.5 })]);

    expect(fixture.appHost.querySelectorAll('tbody tr')[1].textContent).toContain('42,5 %');
    await fixture.stop(true);
  });
});

describe('historial y estadísticas', () => {
  const item = (overrides: Partial<PlayerHistoryItem> = {}): PlayerHistoryItem => ({
    matchId: 'm1', startedAt: '2026-09-02T20:00:00Z', finishedAt: '2026-09-02T20:00:47Z', score: 615, wpm: 61.5, accuracy: 96.1, position: 1, playersCount: 2, won: true, ...overrides,
  });
  const stats = (overrides: Partial<PlayerStats> = {}): PlayerStats => ({
    userId: 'ana', gamesPlayed: 3, wins: 1, winRate: 0.333, averageScore: 450, bestScore: 600, averageWpm: 45, bestWpm: 60, averageAccuracy: 95, lastPlayedAt: '2026-09-02T22:00:00Z', ...overrides,
  });

  it('convierte una fila del historial a texto para mostrar', () => {
    expect(toHistoryRow(item())).toMatchObject({ outcome: 'Ganaste', won: true, score: '615', speed: '61,5 ppm', accuracy: '96,1 %' });
    expect(toHistoryRow(item({ won: false, position: 2, playersCount: 3, wpm: null, accuracy: null }))).toMatchObject({
      outcome: '2.º de 3', won: false, speed: '—', accuracy: '—',
    });
  });

  it('convierte las estadísticas a líneas, con guion donde falta el dato', () => {
    const lines = Object.fromEntries(toStatLines(stats()).map((l) => [l.label, l.value]));
    expect(lines['Partidas jugadas']).toBe('3');
    expect(lines['Victorias']).toBe('1 (33,3 %)');
    expect(lines['Mejor velocidad']).toBe('60 ppm');

    const empty = Object.fromEntries(toStatLines(stats({ averageWpm: null, bestWpm: null, averageAccuracy: null, lastPlayedAt: null })).map((l) => [l.label, l.value]));
    expect(empty['Velocidad promedio']).toBe('—');
    expect(empty['Última partida']).toBe('—');
  });

  function fakeApi(handlers: Partial<Pick<ResultsApi, 'history' | 'stats'>>) {
    return {
      history: vi.fn(handlers.history ?? (async () => [item()])),
      stats: vi.fn(handlers.stats ?? (async () => stats())),
    };
  }

  async function render(api: ReturnType<typeof fakeApi>) {
    return createFixture(
      '<tb-results-panel api.bind="api" user-id.bind="\'ana\'" refresh-key.bind="key"></tb-results-panel>',
      class {
        api = api as unknown as ResultsApi;
        key = 'a';
      },
      [TbResultsPanel],
    ).started;
  }

  it('muestra «cargando» y luego el historial y las estadísticas', async () => {
    let respond!: (rows: PlayerHistoryItem[]) => void;
    const api = fakeApi({ history: () => new Promise<PlayerHistoryItem[]>((resolve) => (respond = resolve)) });
    const fixture = await render(api);
    expect(fixture.appHost.textContent).toContain('Cargando');
    expect(fixture.appHost.querySelector('table')).toBeNull();

    respond([item()]);
    await vi.waitFor(() => expect(fixture.appHost.querySelector('table')).not.toBeNull());

    expect(api.history).toHaveBeenCalledWith('ana', 20);
    expect(api.stats).toHaveBeenCalledWith('ana');
    expect(fixture.appHost.querySelectorAll('tbody tr')).toHaveLength(1);
    expect(fixture.appHost.textContent).toContain('Ganaste');
    expect(fixture.appHost.textContent).toContain('Partidas jugadas');
    await fixture.stop(true);
  });

  it('sin partidas muestra un mensaje en lugar de una tabla vacía', async () => {
    const fixture = await render(fakeApi({ history: async () => [], stats: async () => stats({ gamesPlayed: 0 }) }));

    await vi.waitFor(() => expect(fixture.appHost.textContent).toContain('Todavía no has jugado'));

    expect(fixture.appHost.querySelector('table')).toBeNull();
    await fixture.stop(true);
  });

  it('si falla muestra el error y permite reintentar', async () => {
    let attempt = 0;
    const api = fakeApi({
      history: async () => {
        if (attempt++ === 0) throw new Error('El servidor del juego tuvo un problema.');
        return [item()];
      },
    });
    const fixture = await render(api);

    await vi.waitFor(() => expect(fixture.appHost.querySelector('[role="alert"]')).not.toBeNull());
    expect(fixture.appHost.textContent).toContain('tuvo un problema');

    fixture.appHost.querySelector<HTMLButtonElement>('button')!.click();
    await vi.waitFor(() => expect(fixture.appHost.querySelector('table')).not.toBeNull());
    await fixture.stop(true);
  });

  it('se vuelve a cargar cuando cambia la clave (al terminar una partida)', async () => {
    const api = fakeApi({});
    const fixture = await render(api);
    await vi.waitFor(() => expect(fixture.appHost.querySelector('table')).not.toBeNull());

    (fixture.component as unknown as { key: string }).key = 'b';
    runTasks();

    await vi.waitFor(() => expect(api.history).toHaveBeenCalledTimes(2));
    await fixture.stop(true);
  });
});
