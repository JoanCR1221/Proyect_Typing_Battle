import { BrowserPlatform } from '@aurelia/platform-browser';
import { setPlatform } from '@aurelia/testing';

// Aurelia necesita conocer la «plataforma» (window, document, colas de tareas) del entorno de pruebas (jsdom).
const platform = new BrowserPlatform(window);
setPlatform(platform);
BrowserPlatform.set(globalThis, platform);
