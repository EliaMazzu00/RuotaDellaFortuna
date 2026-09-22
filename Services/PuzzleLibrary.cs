using RuotaDellaFortuna.Models;

namespace RuotaDellaFortuna.Services;

/// <summary>
/// Legge le frasi: quelle incluse nel programma (un file <c>.txt</c> per tema nella
/// cartella <c>frasi/</c>), quelle di un file caricato dall'utente e quelle incollate
/// a mano. Si occupa solo di leggere e interpretare il testo: dello stato della
/// partita non sa nulla.
/// </summary>
/// <remarks>
/// Ogni riga di un file di frasi ha la forma <c>CATEGORIA | FRASE</c>. Righe vuote e
/// righe che iniziano con <c>#</c> sono ignorate, tranne <c># TEMA: Nome</c> che dà
/// il nome leggibile al tema. Senza il separatore <c>|</c> la categoria diventa
/// <c>FRASE MISTERIOSA</c>.
/// <para>
/// Le frasi dei temi vengono lette dal disco una volta sola e tenute in memoria: in
/// regia si spuntano e si togliono temi in continuazione, e rileggere venti file a
/// ogni clic non avrebbe senso.
/// </para>
/// </remarks>
public sealed class PuzzleLibrary
{
    /// <summary>Categoria usata per le frasi scritte senza <c>CATEGORIA |</c>.</summary>
    public const string DefaultCategory = "FRASE MISTERIOSA";

    private const string ThemeNameTag = "# TEMA:";

    /// <summary>Frasi di ogni tema, lette dal disco al primo utilizzo.</summary>
    private readonly Dictionary<string, List<Puzzle>> _puzzlesByTheme = new(StringComparer.OrdinalIgnoreCase);

    private List<PuzzleThemeInfo> _themes = new();

    /// <summary>
    /// Crea la libreria leggendo i temi presenti nella cartella indicata.
    /// </summary>
    /// <param name="themesPath">Cartella che contiene i file <c>.txt</c> dei temi.</param>
    public PuzzleLibrary(string themesPath)
    {
        ThemesPath = themesPath;
        Reload();
    }

    /// <summary>Cartella da cui vengono letti i temi del programma.</summary>
    public string ThemesPath { get; }

    /// <summary>I temi disponibili, in ordine di nome file.</summary>
    public IReadOnlyList<PuzzleThemeInfo> Themes => _themes;

    /// <summary>
    /// Rilegge la cartella dei temi da zero, svuotando la cache. Utile se i file
    /// delle frasi vengono modificati mentre l'applicazione è in esecuzione.
    /// </summary>
    public void Reload()
    {
        _puzzlesByTheme.Clear();
        var found = new List<PuzzleThemeInfo>();

        try
        {
            if (Directory.Exists(ThemesPath))
            {
                var files = Directory
                    .EnumerateFiles(ThemesPath, "*.txt")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

                foreach (var path in files)
                {
                    var lines = File.ReadAllLines(path);
                    var id = Path.GetFileNameWithoutExtension(path);
                    var puzzles = ParsePuzzles(lines);

                    // Le frasi finiscono subito in cache: le abbiamo già lette per contarle.
                    _puzzlesByTheme[id] = puzzles;
                    found.Add(new PuzzleThemeInfo(id, ReadThemeName(lines, id), puzzles.Count));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cartella illeggibile: si resta senza temi inclusi. L'utente può sempre
            // caricare un file o incollare le frasi a mano.
            found.Clear();
            _puzzlesByTheme.Clear();
        }

        _themes = found;
    }

    /// <summary>
    /// Unisce le frasi dei temi indicati, mantenendo l'ordine in cui i temi
    /// appaiono nella cartella. Gli identificatori sconosciuti vengono ignorati.
    /// </summary>
    public List<Puzzle> GetPuzzles(IEnumerable<string> themeIds)
    {
        var wanted = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        var combined = new List<Puzzle>();

        foreach (var theme in _themes)
            if (wanted.Contains(theme.Id) && _puzzlesByTheme.TryGetValue(theme.Id, out var puzzles))
                combined.AddRange(puzzles);

        return combined;
    }

    /// <summary>I nomi leggibili dei temi indicati, nell'ordine della cartella.</summary>
    public List<string> GetThemeNames(IEnumerable<string> themeIds)
    {
        var wanted = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        return _themes.Where(t => wanted.Contains(t.Id)).Select(t => t.Name).ToList();
    }

    /// <summary>
    /// Interpreta un testo a più righe come elenco di frasi, una per riga.
    /// Accetta indifferentemente fine-riga Windows o Unix.
    /// </summary>
    public static List<Puzzle> ParsePuzzles(string text) =>
        ParsePuzzles(text.ReplaceLineEndings("\n").Split('\n'));

    /// <summary>
    /// Interpreta righe di testo nel formato <c>CATEGORIA | FRASE</c>, scartando
    /// righe vuote, commenti e righe senza frase.
    /// </summary>
    public static List<Puzzle> ParsePuzzles(IEnumerable<string> lines)
    {
        var puzzles = new List<Puzzle>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            string category, phrase;
            int separator = trimmed.IndexOf('|');

            if (separator >= 0)
            {
                category = trimmed[..separator].Trim();
                phrase = trimmed[(separator + 1)..].Trim();
            }
            else
            {
                category = DefaultCategory;
                phrase = trimmed;
            }

            if (phrase.Length == 0)
                continue;

            // Il tabellone è tutto in maiuscolo: normalizziamo qui, una volta sola.
            puzzles.Add(new Puzzle(category.ToUpperInvariant(), phrase.ToUpperInvariant()));
        }

        return puzzles;
    }

    /// <summary>
    /// Ricava il nome del tema dalla riga <c># TEMA: Nome</c>; se manca, lo deduce
    /// dal nome del file (<c>01-modi-di-dire</c> → <c>Modi di dire</c>).
    /// </summary>
    private static string ReadThemeName(IEnumerable<string> lines, string fileName)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(ThemeNameTag, StringComparison.OrdinalIgnoreCase))
                continue;

            var declared = trimmed[ThemeNameTag.Length..].Trim();
            if (declared.Length > 0)
                return declared;
        }

        return PrettifyFileName(fileName);
    }

    /// <summary><c>01-modi-di-dire</c> → <c>Modi di dire</c>.</summary>
    private static string PrettifyFileName(string fileName)
    {
        var name = fileName;

        // Via il prefisso numerico che serve solo a ordinare i file.
        int dash = name.IndexOf('-');
        if (dash > 0 && int.TryParse(name[..dash], out _))
            name = name[(dash + 1)..];

        name = name.Replace('-', ' ').Replace('_', ' ').Trim();

        return name.Length > 0
            ? char.ToUpperInvariant(name[0]) + name[1..]
            : fileName;
    }
}
