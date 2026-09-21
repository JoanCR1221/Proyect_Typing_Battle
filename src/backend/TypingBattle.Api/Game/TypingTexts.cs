namespace TypingBattle.Api.Game;

/// <summary>Texto que los jugadores deben copiar. El <see cref="Id"/> queda en el <c>metadata</c> del resultado.</summary>
public sealed record TypingText(string Id, string Text);

/// <summary>Origen de los textos de las partidas.</summary>
public interface ITextProvider
{
    TypingText Next();
}

/// <summary>Elige al azar entre un conjunto fijo de frases en español, sin saltos de línea y con puntuación simple.</summary>
public sealed class StaticTextProvider : ITextProvider
{
    public static IReadOnlyList<TypingText> Texts { get; } =
    [
        new("t-01", "La programación es el arte de explicarle a una computadora, paso a paso y sin ambigüedades, exactamente lo que queremos que haga."),
        new("t-02", "Costa Rica es un país de volcanes, playas y bosques nubosos donde la naturaleza se puede disfrutar durante todo el año."),
        new("t-03", "Un buen programador escribe código pensando en las personas que lo van a leer, porque la computadora entiende cualquier cosa que compile."),
        new("t-04", "La velocidad al teclear se mide en palabras por minuto, pero escribir sin errores es tan importante como escribir rápido."),
        new("t-05", "Las bases de datos guardan la información de manera ordenada para que podamos consultarla, actualizarla y compartirla con seguridad."),
        new("t-06", "En un juego multijugador en tiempo real cada milisegundo cuenta, y el servidor decide quién avanza y quién se queda atrás."),
        new("t-07", "El café costarricense crece a la sombra de las montañas y su aroma acompaña las mañanas de miles de familias en todo el país."),
        new("t-08", "Aprender a programar es como aprender un idioma nuevo: al principio parece imposible y con la práctica diaria todo empieza a fluir."),
        new("t-09", "Una pantalla en blanco no es un problema, es una invitación, porque cada línea de código que escribes construye algo que antes no existía."),
        new("t-10", "Los microservicios dividen una aplicación grande en piezas pequeñas e independientes que se comunican entre sí a través de la red."),
        new("t-11", "La lluvia de la tarde cae sobre el Valle Central mientras las nubes se posan lentamente sobre las montañas verdes de Cartago."),
        new("t-12", "Probar el código antes de entregarlo ahorra horas de trabajo, porque un error encontrado a tiempo cuesta mucho menos que uno en producción."),
        new("t-13", "El trabajo en equipo funciona cuando cada persona hace su parte, revisa la de sus compañeros y ayuda cuando alguien se atasca."),
        new("t-14", "Un servidor amable siempre responde con un código claro: doscientos si todo salió bien y cuatrocientos cuatro si no encuentra lo que buscas."),
        new("t-15", "El teclado es el instrumento de quien programa: con práctica los dedos aprenden el camino y la mente se libera para pensar mejor."),
        new("t-16", "Cada versión de un programa es el resultado de muchas pequeñas decisiones, y las mejores se anotan para que el equipo las recuerde."),
        new("t-17", "Al final de cada carrera solo importa una cosa: cruzar la meta con las manos firmes, los ojos en el texto y la mente tranquila."),
        new("t-18", "Los ríos de Costa Rica bajan con fuerza desde las cordilleras hasta llegar al mar, llevando consigo la vida de los bosques."),
        new("t-19", "La paciencia es una de las habilidades más valiosas de quien desarrolla software, porque los errores más difíciles se resuelven con calma."),
        new("t-20", "Escribir con precisión es más veloz que corregir errores: quien avanza sin equivocarse llega primero aunque parezca que va despacio."),
    ];

    public TypingText Next() => Texts[Random.Shared.Next(Texts.Count)];
}
