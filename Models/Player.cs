namespace RuotaDellaFortuna.Models;

/// <summary>Un concorrente, col suo punteggio e il suo stato di partecipazione.</summary>
public sealed class Player
{
    /// <summary>Nome mostrato in regia e sul tabellone.</summary>
    public string Name { get; set; } = "";

    /// <summary>Punteggio corrente; non scende mai sotto zero.</summary>
    public int Score { get; set; }

    /// <summary>
    /// Vero se partecipa al giro dei turni. Un concorrente disattivato resta
    /// visibile sul tabellone (in grigio) ma viene saltato quando il turno passa.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
