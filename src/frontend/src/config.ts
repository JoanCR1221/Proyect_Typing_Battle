export interface TypingConfig {
  /** URL base de la API del juego, sin «/» final. */
  apiBaseUrl: string;
  /** Ruta del hub dentro de esa API. */
  hubPath: string;
}

type ConfigHolder = { __TYPING_BATTLE_CONFIG__?: Partial<TypingConfig> };

const DEFAULT_API_URL = 'http://localhost:5080';

/**
 * Configuración efectiva, de menor a mayor prioridad:
 * 1. Lo definido al compilar (`TYPING_API_URL`, ver webpack.config.js).
 * 2. `globalThis.__TYPING_BATTLE_CONFIG__`, por si quien aloja la página quiere cambiarla sin recompilar.
 * 3. Los valores que pase quien crea el módulo.
 */
export function resolveConfig(overrides: Partial<TypingConfig> = {}): TypingConfig {
  const runtime = (globalThis as ConfigHolder).__TYPING_BATTLE_CONFIG__ ?? {};
  const buildTime = typeof __API_URL__ === 'string' ? __API_URL__ : DEFAULT_API_URL;

  const merged = { apiBaseUrl: buildTime, hubPath: '/hubs/typing', ...runtime, ...overrides };
  return { ...merged, apiBaseUrl: merged.apiBaseUrl.replace(/\/+$/, '') };
}
