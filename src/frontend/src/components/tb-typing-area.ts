import template from './tb-typing-area.html?raw';
import { buildChars, updateChars, type CharView } from './typing-chars';

/**
 * Área de escritura: muestra el texto carácter por carácter (bien, mal, actual, pendiente) y captura el teclado con un
 * `<textarea>` invisible que cubre toda el área. No calcula velocidad ni precisión: solo informa lo escrito con
 * `onTyped`; el servidor hace las cuentas (docs/hub-typing.md).
 */
export class TbTypingArea {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-typing-area',
    template,
    bindables: ['text', 'typed', 'disabled', 'onTyped'],
  };

  /** Texto que hay que copiar. */
  text = '';

  /** Lo que el jugador lleva escrito (por ejemplo, al recuperarlo tras una reconexión). */
  typed = '';

  disabled = false;

  /** Se llama con todo lo escrito cada vez que cambia. */
  onTyped: ((typed: string) => void) | null = null;

  chars: CharView[] = [];
  focused = false;
  input!: HTMLTextAreaElement;

  binding(): void {
    this.rebuild();
  }

  attached(): void {
    this.input.value = this.typed ?? '';
    this.focusIfPossible();
  }

  textChanged(): void {
    this.rebuild();
  }

  typedChanged(value: string): void {
    if (this.input && this.input.value !== (value ?? '')) {
      this.input.value = value ?? '';
    }
    this.refresh();
  }

  disabledChanged(): void {
    this.focusIfPossible();
  }

  /** El navegador dispara `input` con cada tecla, borrado o corrección. */
  changed(): void {
    const value = this.input.value;
    this.typed = value;
    this.refresh();
    this.onTyped?.(value);
  }

  /**
   * Enter no se escribe: el texto no tiene saltos de línea. En Aurelia 2 `.trigger` NO cancela el evento por sí solo
   * (en la versión 1 sí lo hacía), así que se cancela aquí de forma explícita.
   */
  keydown(event: KeyboardEvent): boolean {
    if (event.key === 'Enter') {
      event.preventDefault();
    }
    return true;
  }

  /** Pegar y arrastrar texto no tienen sentido en una carrera (el servidor además rechaza avances imposibles). */
  block(event: Event): boolean {
    event.preventDefault();
    return true;
  }

  focus(): void {
    this.input?.focus();
  }

  private rebuild(): void {
    this.chars = buildChars(this.text ?? '');
    this.refresh();
  }

  private refresh(): void {
    updateChars(this.chars, this.text ?? '', this.typed ?? '');
  }

  private focusIfPossible(): void {
    if (this.input && !this.disabled) {
      // Se espera un instante: el campo aún puede estar deshabilitado por el cambio que acaba de llegar.
      queueMicrotask(() => this.input?.focus());
    }
  }
}
