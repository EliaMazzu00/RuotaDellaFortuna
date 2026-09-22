namespace RuotaDellaFortuna.Models;

/// <summary>Una frase da mandare a tabellone, con la categoria che la introduce.</summary>
/// <param name="Category">Categoria mostrata sopra al tabellone (es. <c>PROVERBIO</c>).</param>
/// <param name="Phrase">La frase da indovinare, gia' in maiuscolo.</param>
public readonly record struct Puzzle(string Category, string Phrase);

/// <summary>Esito del controllo di una frase rispetto al tabellone configurato.</summary>
/// <param name="Puzzle">La frase controllata.</param>
/// <param name="Fits">Vero se entra nel tabellone configurato senza allargarlo.</param>
/// <param name="LineCount">
/// Righe occupate se entra, oppure righe necessarie sulla griglia estesa se non entra.
/// </param>
public readonly record struct PuzzleStatus(Puzzle Puzzle, bool Fits, int LineCount)
{
    /// <summary>Scorciatoia per la categoria della frase.</summary>
    public string Category => Puzzle.Category;

    /// <summary>Scorciatoia per il testo della frase.</summary>
    public string Phrase => Puzzle.Phrase;
}

/// <summary>Un tema di frasi incluso nel programma, cioe' un file <c>.txt</c> in <c>frasi/</c>.</summary>
/// <param name="Id">Nome del file senza estensione, usato come identificatore.</param>
/// <param name="Name">Nome leggibile mostrato in regia.</param>
/// <param name="Count">Quante frasi contiene.</param>
public readonly record struct PuzzleThemeInfo(string Id, string Name, int Count);
