// Ver el comentario de src/index.ts: Module Federation necesita este límite asíncrono.
import('./bootstrap').catch((error: unknown) => {
  console.error('No se pudo iniciar el Shell simulado', error);
});
