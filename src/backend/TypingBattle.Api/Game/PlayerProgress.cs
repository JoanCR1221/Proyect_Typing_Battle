namespace TypingBattle.Api.Game;

/// <summary>Estado de un jugador dentro de una sala. Lo modifica solo <see cref="GameRoom"/>, bajo su candado.</summary>
public sealed class PlayerProgress(string userId, string displayName, int joinOrder)
{
    public string UserId { get; } = userId;
    public string DisplayName { get; set; } = displayName;

    /// <summary>Orden de llegada a la sala; desempata las clasificaciones.</summary>
    public int JoinOrder { get; } = joinOrder;

    public bool Ready { get; set; }

    /// <summary>Un jugador puede tener varias conexiones (pestañas); está conectado mientras quede alguna.</summary>
    public HashSet<string> Connections { get; } = [];

    public bool Connected => Connections.Count > 0;

    /// <summary>Lo último que el jugador tiene escrito (ya recortado al largo del texto).</summary>
    public string Typed { get; private set; } = "";

    /// <summary>Largo del prefijo correcto: los caracteres escritos bien antes del primer error.</summary>
    public int CorrectChars { get; private set; }

    /// <summary>Teclas nuevas escritas (borrar no cuenta).</summary>
    public int Keystrokes { get; private set; }

    /// <summary>Teclas nuevas que no coincidían con el texto.</summary>
    public int Errors { get; private set; }

    public bool Finished { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    /// <summary>Puesto de llegada (1 = primero en terminar). Solo lo tienen quienes terminaron.</summary>
    public int? FinishRank { get; set; }

    /// <summary>
    /// Aplica lo que el jugador tiene escrito ahora. Devuelve <c>true</c> si algo cambió.
    /// Cada carácter que se agrega al final cuenta como una tecla y, si no coincide con el texto, como un error.
    /// Termina al escribir el texto completo y sin errores.
    /// </summary>
    public bool Apply(string text, string typed, DateTime now)
    {
        if (Finished)
        {
            return false;
        }

        if (typed.Length > text.Length)
        {
            typed = typed[..text.Length];
        }

        if (typed == Typed)
        {
            return false;
        }

        for (var i = Typed.Length; i < typed.Length; i++)
        {
            Keystrokes++;
            if (typed[i] != text[i])
            {
                Errors++;
            }
        }

        Typed = typed;
        CorrectChars = CommonPrefixLength(typed, text);

        if (typed.Length == text.Length && CorrectChars == text.Length)
        {
            Finished = true;
            FinishedAt = now;
        }

        return true;
    }

    private static int CommonPrefixLength(string typed, string text)
    {
        var length = Math.Min(typed.Length, text.Length);
        var i = 0;
        while (i < length && typed[i] == text[i])
        {
            i++;
        }

        return i;
    }
}
