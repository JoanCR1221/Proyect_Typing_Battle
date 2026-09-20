/** Cómo se pinta cada carácter del texto según lo que el jugador lleva escrito. */
export type CharState = 'done' | 'wrong' | 'current' | 'pending';

export interface CharView {
  /** El carácter que hay que escribir. */
  char: string;
  state: CharState;
}

/** Un objeto por carácter, todos pendientes. Se crean una vez por texto y después solo cambia su `state`. */
export function buildChars(text: string): CharView[] {
  return Array.from(text, (char) => ({ char, state: 'pending' as CharState }));
}

/**
 * Actualiza los estados en el mismo arreglo (sin crear nodos nuevos, para que la interfaz solo repinte lo que cambió):
 * lo escrito bien es `done`, lo escrito mal es `wrong` (se muestra el carácter esperado, no el equivocado), el siguiente
 * es `current` y el resto `pending`.
 */
export function updateChars(chars: CharView[], text: string, typed: string): void {
  for (let i = 0; i < chars.length; i++) {
    const state: CharState = i < typed.length ? (typed[i] === text[i] ? 'done' : 'wrong') : i === typed.length ? 'current' : 'pending';
    if (chars[i].state !== state) {
      chars[i].state = state;
    }
  }
}
