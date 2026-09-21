import { describe, expect, it } from 'vitest';
import {
  formatClock,
  formatDate,
  formatInteger,
  formatNumber,
  formatPercent,
  formatRatio,
  ordinal,
} from '../src/components/format';
import { buildChars, updateChars } from '../src/components/typing-chars';

describe('formato', () => {
  it('los números usan la coma decimal del español y muestran un guion si faltan', () => {
    expect(formatNumber(61.5)).toBe('61,5');
    expect(formatNumber(null)).toBe('—');
    expect(formatNumber(undefined)).toBe('—');
    expect(formatInteger(615.4)).toBe('615');
    expect(formatInteger(null)).toBe('—');
  });

  it('los porcentajes llevan el símbolo y las fracciones se convierten', () => {
    expect(formatPercent(96.1)).toBe('96,1 %');
    expect(formatPercent(null)).toBe('—');
    expect(formatRatio(0.333)).toBe('33,3 %');
    expect(formatRatio(1)).toBe('100 %');
  });

  it('las fechas se muestran en hora local y tolera valores inválidos', () => {
    expect(formatDate('2026-09-02T20:00:00Z')).not.toBe('—');
    expect(formatDate('2026-09-02T20:00:00Z')).toMatch(/\d/);
    expect(formatDate(null)).toBe('—');
    expect(formatDate('no es una fecha')).toBe('—');
  });

  it('el puesto va en ordinal masculino', () => {
    expect(ordinal(1)).toBe('1.º');
    expect(ordinal(12)).toBe('12.º');
  });

  it('el reloj muestra minutos y segundos con dos dígitos', () => {
    expect(formatClock(60)).toBe('1:00');
    expect(formatClock(59)).toBe('0:59');
    expect(formatClock(5)).toBe('0:05');
    expect(formatClock(-3)).toBe('0:00');
    expect(formatClock(9.9)).toBe('0:09');
  });
});

describe('estado de cada carácter', () => {
  const text = 'hola mundo';

  const states = (typed: string) => {
    const chars = buildChars(text);
    updateChars(chars, text, typed);
    return chars.map((c) => c.state[0]).join('');
  };

  it('al principio todo está pendiente salvo el primer carácter, que es el actual', () => {
    expect(states('')).toBe('cppppppppp');
  });

  it('lo escrito bien es done, el siguiente es current y el resto pending', () => {
    expect(states('hola')).toBe('ddddcppppp');
  });

  it('un error queda marcado como wrong en su posición aunque se siga escribiendo', () => {
    expect(states('hxla m')).toBe('dwddddcppp');
  });

  it('con el texto completo no queda ninguno actual', () => {
    expect(states('hola mundo')).toBe('dddddddddd');
  });

  it('cada carácter conserva el que hay que escribir (no el equivocado)', () => {
    const chars = buildChars(text);
    updateChars(chars, text, 'x');

    expect(chars[0]).toEqual({ char: 'h', state: 'wrong' });
  });

  it('actualiza los mismos objetos en lugar de crear nuevos, para que la interfaz solo repinte lo que cambió', () => {
    const chars = buildChars(text);
    const first = chars[0];

    updateChars(chars, text, 'ho');

    expect(chars[0]).toBe(first);
    expect(first.state).toBe('done');
  });

  it('al borrar, los caracteres vuelven a pendiente', () => {
    const chars = buildChars(text);
    updateChars(chars, text, 'hola');
    updateChars(chars, text, 'ho');

    expect(chars.map((c) => c.state[0]).join('')).toBe('ddcppppppp');
  });
});
