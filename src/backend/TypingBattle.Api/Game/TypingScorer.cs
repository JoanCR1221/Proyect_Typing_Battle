namespace TypingBattle.Api.Game;

/// <summary>
/// Fórmulas del juego. Se calculan siempre en el servidor (ver docs/hub-typing.md): el cliente solo envía
/// lo que tecleó y nunca su propia velocidad, precisión o puntaje.
/// </summary>
public static class TypingScorer
{
    /// <summary>Por debajo de este tiempo la velocidad se dispara sin sentido (5 letras en 0,1 s serían 600 ppm).</summary>
    private const double MinElapsedSeconds = 1;

    /// <summary>Palabras por minuto netas: una «palabra» son 5 caracteres correctos.</summary>
    public static double Wpm(int correctChars, TimeSpan elapsed)
    {
        var minutes = Math.Max(elapsed.TotalSeconds, MinElapsedSeconds) / 60.0;
        return Math.Round(correctChars / 5.0 / minutes, 1);
    }

    /// <summary>
    /// Porcentaje de teclas nuevas que fueron correctas. Un error cuenta aunque se corrija después.
    /// Sin ninguna tecla es 0: quien no escribió no tiene «100 % de precisión».
    /// </summary>
    public static double Accuracy(int keystrokes, int errors) =>
        keystrokes <= 0 ? 0 : Math.Round((keystrokes - errors) * 100.0 / keystrokes, 1);

    /// <summary>Porcentaje del texto ya escrito correctamente (0–100).</summary>
    public static double Progress(int correctChars, int textLength) =>
        textLength <= 0 ? 0 : Math.Round(Math.Min(100, correctChars * 100.0 / textLength), 1);

    /// <summary>Puntaje entero de la partida: velocidad neta × precisión × 10 (60 ppm al 100 % = 600).</summary>
    public static int Score(double wpm, double accuracy) => (int)Math.Round(wpm * accuracy / 100.0 * 10);
}
