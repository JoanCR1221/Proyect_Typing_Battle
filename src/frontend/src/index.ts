// Punto de entrada de la aplicación standalone. El import() dinámico es el «límite asíncrono» que exige
// Module Federation: le da tiempo de negociar los módulos compartidos (Aurelia) antes de ejecutar la aplicación.
import('./bootstrap').catch((error: unknown) => {
  console.error('No se pudo iniciar Typing Battle', error);
});
