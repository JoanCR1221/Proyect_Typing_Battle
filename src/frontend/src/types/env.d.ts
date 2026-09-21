// Constante que Webpack (DefinePlugin) y Vitest (define) reemplazan al compilar: URL base de la API del juego.
declare const __API_URL__: string;

// `import template from './x.html?raw'` entrega el HTML como texto.
declare module '*.html?raw' {
  const template: string;
  export default template;
}

// Módulos que expone este remote, tal como los ve un host de Module Federation (ver webpack.config.js).
declare module 'typingGame/GameModule' {
  const TypingGameModule: typeof import('../game-module').TypingGameModule;
  export default TypingGameModule;
}

declare module 'typingGame/GameView' {
  export const TypingGameView: typeof import('../game-view').TypingGameView;
}
