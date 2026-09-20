// Shell SIMULADO, solo para desarrollo: un host mínimo de Module Federation que carga el remote de Typing Battle
// como lo haría el Shell real. Sirve para comprobar la integración (sobre todo que Aurelia se comparta como singleton)
// sin depender de que el Equipo 3 tenga su Shell listo. No es parte del entregable.
//
//   npm start          → remote en http://localhost:4004
//   npm run start:shell → Shell simulado en http://localhost:4010
const path = require('node:path');
const webpack = require('webpack');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const { aureliaShared, rules } = require('./webpack.shared');

const { ModuleFederationPlugin } = webpack.container;
const remoteUrl = process.env.TYPING_REMOTE_URL ?? 'http://localhost:4004';

module.exports = (env, argv) => ({
  entry: './src/host-mock/index.ts',
  output: {
    path: path.resolve(__dirname, 'dist-host-mock'),
    filename: '[name].js',
    publicPath: 'auto',
    clean: true,
  },
  devtool: argv.mode === 'production' ? 'source-map' : 'eval-source-map',
  resolve: { extensions: ['.ts', '.js'] },
  module: { rules },
  plugins: [
    new ModuleFederationPlugin({
      name: 'shellMock',
      remotes: { typingGame: `typingGame@${remoteUrl}/remoteEntry.js` },
      shared: aureliaShared(),
    }),
    new HtmlWebpackPlugin({ template: './host-mock.html' }),
  ],
  devServer: { port: 4010, hot: false, historyApiFallback: true },
  performance: { hints: false },
});
