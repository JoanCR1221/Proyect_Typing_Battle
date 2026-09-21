import Aurelia from 'aurelia';
import { DevHost } from './dev/dev-host';

// Modo standalone: la página de desarrollo hace de Shell. Cuando el Shell real carga el juego NO pasa por aquí:
// usa lo que expone remoteEntry.js (game-module.ts y game-view.ts).
const host = document.querySelector<HTMLElement>('#app');
if (!host) {
  throw new Error('Falta el elemento #app en index.html.');
}

void Aurelia.app({ host, component: DevHost }).start();
