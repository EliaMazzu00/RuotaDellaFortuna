namespace RuotaDellaFortuna.Models;

/// <summary>
/// Una singola casella del tabellone.
/// </summary>
/// <remarks>
/// Le celle si distinguono in tre tipi, che il Display disegna in modo diverso:
/// <list type="bullet">
///   <item><b>lettera</b> (<see cref="IsLetter"/> = <c>true</c>): bianca se coperta,
///   girata a mostrare <see cref="Display"/> quando viene rivelata;</item>
///   <item><b>vuota</b> (<see cref="IsLetter"/> = <c>false</c> e <see cref="Display"/> = spazio):
///   la casella verde che riempie il tabellone intorno alla frase;</item>
///   <item><b>fissa</b> (<see cref="IsLetter"/> = <c>false</c> con un <see cref="Display"/> stampabile):
///   punteggiatura, apostrofi e cifre, sempre visibili e mai da chiamare.</item>
/// </list>
/// Fuori dal tabellone (le righe corte centrate in una griglia piu' larga) non c'e'
/// nessuna cella: la riga contiene <c>null</c>.
/// </remarks>
public sealed class BoardCell
{
    /// <summary>Vero se la casella nasconde una lettera da indovinare.</summary>
    public bool IsLetter { get; init; }

    /// <summary>Carattere mostrato quando la cella e' visibile, accenti compresi (es. <c>À</c>).</summary>
    public char Display { get; init; }

    /// <summary>
    /// Lettera base A–Z usata per il confronto con quella chiamata.
    /// Per <c>À</c> vale <c>A</c>; per le celle non-lettera e' uno spazio.
    /// </summary>
    public char Match { get; init; }

    /// <summary>Vero quando la cella e' girata e mostra il proprio carattere.</summary>
    public bool Revealed { get; set; }

    /// <summary>Vero per un istante dopo la rivelazione, per l'animazione gialla del Display.</summary>
    public bool Flash { get; set; }
}
