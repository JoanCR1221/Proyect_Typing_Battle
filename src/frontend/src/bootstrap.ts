import Aurelia from 'aurelia';

class TypingBattleApp {
  static $au = { type: 'custom-element' as const, name: 'typing-battle-app', template: '<h1>Typing Battle</h1>' };
}

const host = document.querySelector<HTMLElement>('#app');
if (!host) {
  throw new Error('Falta el elemento #app en index.html.');
}

void Aurelia.app({ host, component: TypingBattleApp }).start();
