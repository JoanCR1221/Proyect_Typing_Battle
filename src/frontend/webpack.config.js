// Typing Battle como microfrontend: un remote de Module Federation que carga el Shell (Equipo 3),
// y a la vez una aplicación independiente («modo standalone») para desarrollar sin el Shell.
const path = require('node:path');
const webpack = require('webpack');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const { aureliaShared, rules } = require('./webpack.shared');

const { ModuleFederationPlugin } = webpack.container;

module.exports = (env, argv) => {
  const production = argv.mode === 'production';

  return {
    // index.ts solo hace import() dinámico de bootstrap.ts: Module Federation necesita ese límite asíncrono
    // para poder negociar los módulos compartidos antes de ejecutar el código de la aplicación.
    entry: './src/index.ts',
    output: {
      path: path.resolve(__dirname, 'dist'),
      filename: production ? '[name].[contenthash].js' : '[name].js',
      publicPath: 'auto', // el remote carga sus chunks desde donde esté publicado remoteEntry.js
      clean: true,
    },
    devtool: production ? 'source-map' : 'eval-source-map',
    resolve: { extensions: ['.ts', '.js'] },
    module: { rules },
    plugins: [
      new webpack.DefinePlugin({
        // URL base de la API del juego. Se cambia al compilar: TYPING_API_URL=https://... npm run build
        __API_URL__: JSON.stringify(process.env.TYPING_API_URL ?? 'http://localhost:5080'),
      }),
      new ModuleFederationPlugin({
        name: 'typingGame',
        filename: 'remoteEntry.js',
        exposes: {
          './GameModule': './src/game-module.ts',
          './GameView': './src/game-view.ts',
        },
        shared: aureliaShared(),
      }),
      new HtmlWebpackPlugin({ template: './index.html' }),
    ],
    devServer: {
      port: 4004,
      hot: false,
      historyApiFallback: true,
      // El Shell carga remoteEntry.js desde otro origen.
      headers: { 'Access-Control-Allow-Origin': '*' },
    },
    performance: { hints: false },
  };
};
