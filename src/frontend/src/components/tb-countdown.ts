import template from './tb-countdown.html?raw';

/** Cuenta regresiva antes de la carrera. Cada número entra con un pequeño «pop»: es el momento con movimiento del diseño. */
export class TbCountdown {
  static $au = {
    type: 'custom-element' as const,
    name: 'tb-countdown',
    template,
    bindables: ['seconds'],
  };

  seconds = 0;
  number!: HTMLElement;

  /** Reinicia la animación de CSS cada vez que cambia el número. */
  secondsChanged(): void {
    const element = this.number;
    if (!element) {
      return;
    }
    element.classList.remove('tb-countdown-number');
    void element.offsetWidth; // fuerza al navegador a procesar el cambio de clase antes de volver a ponerla
    element.classList.add('tb-countdown-number');
  }
}
