import Aurelia from 'aurelia';
import { MockShell } from './mock-shell';

const host = document.querySelector<HTMLElement>('#app');
if (!host) {
  throw new Error('Falta el elemento #app en host-mock.html.');
}

void Aurelia.app({ host, component: MockShell }).start();
