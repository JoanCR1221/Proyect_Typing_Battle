// Piezas comunes de las dos configuraciones de Webpack (el remote de Typing Battle y el Shell simulado).
const fs = require('node:fs');
const path = require('node:path');

/**
 * Module Federation: todos los paquetes de Aurelia se comparten como singleton.
 * Aurelia exige UNA sola copia de su runtime. Si el Shell y el juego cargaran cada uno la suya, falla con
 * «Conflicting @aurelia/metadata module import detected». Por eso no basta con compartir solo `aurelia`:
 * se comparten también todos los `@aurelia/*` que trae (kernel, runtime, runtime-html, metadata...).
 * El Shell debe compartir los mismos paquetes con versiones compatibles (ver docs/integracion-shell.md).
 */
function aureliaShared() {
  const scope = path.join(__dirname, 'node_modules', '@aurelia');
  const names = [
    'aurelia',
    ...fs.readdirSync(scope).filter((name) => name !== 'testing').map((name) => `@aurelia/${name}`),
  ];

  return Object.fromEntries(
    names.map((name) => {
      const { version } = JSON.parse(fs.readFileSync(path.join(__dirname, 'node_modules', name, 'package.json'), 'utf8'));
      return [name, { singleton: true, requiredVersion: `^${version}` }];
    }),
  );
}

/**
 * Reglas de carga.
 * - `.ts` con ts-loader (también revisa los tipos: si hay un error de tipos, el build falla).
 * - `x.html?raw` entrega el HTML como texto, para usarlo como `template` de un elemento de Aurelia.
 *   Es la misma sintaxis que entiende Vitest, así el código se prueba sin configuración extra.
 * - `.css` se inyecta con style-loader.
 */
const rules = [
  { test: /\.ts$/, use: 'ts-loader', exclude: /node_modules/ },
  { test: /\.html$/, resourceQuery: /raw/, type: 'asset/source' },
  { test: /\.css$/, use: ['style-loader', 'css-loader'] },
];

module.exports = { aureliaShared, rules };
