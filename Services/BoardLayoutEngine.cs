using System.Globalization;
using System.Text;
using RuotaDellaFortuna.Models;

namespace RuotaDellaFortuna.Services;

/// <summary>
/// Impagina una frase sul tabellone. È una classe puramente funzionale: non ha
/// stato, non legge file e non dipende da nulla, così la logica più delicata del
/// progetto (dove andare a capo, come centrare, cosa fare con le frasi lunghissime)
/// si può leggere e verificare in isolamento.
/// </summary>
/// <remarks>
/// La collocazione di una frase segue due strade, nell'ordine:
/// <list type="number">
///   <item>
///     <b>Tabellone configurato.</b> Si prova a far stare la frase nelle righe
///     scelte (classiche o personalizzate) senza spezzare nessuna parola,
///     provando tutte le partenze verticali e tenendo la più centrata.
///   </item>
///   <item>
///     <b>Griglia estesa.</b> Se non ci sta, invece di scartare o tagliare la
///     frase si costruisce una griglia larga quanto serve (almeno la parola più
///     lunga) e alta quanto serve. La frase resta sempre giocabile.
///   </item>
/// </list>
/// </remarks>
public static class BoardLayoutEngine
{
    /// <summary>Le righe del tabellone televisivo: 12 · 14 · 14 · 12 celle.</summary>
    public static int[] ClassicRowWidths => new[] { 12, 14, 14, 12 };

    /// <summary>Larghezza di ripiego quando non è configurata nessuna riga.</summary>
    public const int DefaultWidth = 14;

    /// <summary>Larghezza minima ammessa per una riga.</summary>
    public const int MinRowWidth = 1;

    /// <summary>Larghezza massima ammessa per una riga.</summary>
    public const int MaxRowWidth = 30;

    /// <summary>Numero massimo di righe configurabili a mano.</summary>
    public const int MaxRows = 8;

    /// <summary>Tetto di righe della griglia estesa: una frase più lunga di così non esiste.</summary>
    private const int ExpandedMaxRows = 400;

    // ============================================================
    //  Lettere
    // ============================================================

    /// <summary>
    /// Riconduce un carattere alla lettera base A–Z usata per il confronto:
    /// <c>à</c> e <c>À</c> diventano <c>A</c>. Restituisce <c>null</c> per
    /// spazi, cifre e punteggiatura, cioè per tutto ciò che non si chiama.
    /// </summary>
    public static char? NormalizeLetter(char c)
    {
        var upper = char.ToUpperInvariant(c);

        // Via rapida per il caso normale: le lettere dell'alfabeto latino sono già
        // a posto. Vale per quasi tutti i caratteri di quasi tutte le frasi, e
        // risparmia una stringa e una normalizzazione Unicode a ogni cella.
        if (upper is >= 'A' and <= 'Z')
            return upper;

        // Tutto ciò che non è una lettera non si chiama: spazi, cifre, punteggiatura.
        // Il controllo serve anche a non passare alla normalizzazione Unicode i
        // caratteri che non ne hanno una — surrogati spaiati e non-caratteri come
        // U+FFFF — che la farebbero fallire con un'eccezione.
        if (!char.IsLetter(upper))
            return null;

        // Scompone il carattere (À → A + accento) e tiene il primo pezzo che non
        // sia un segno diacritico: quello è la lettera base.
        var decomposed = upper.ToString().Normalize(NormalizationForm.FormD);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            var stripped = char.ToUpperInvariant(ch);
            return stripped is >= 'A' and <= 'Z' ? stripped : null;
        }

        return null;
    }

    /// <summary>Divide la frase in parole, scartando gli spazi multipli.</summary>
    public static string[] SplitWords(string phrase) =>
        phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // ============================================================
    //  Costruzione del tabellone
    // ============================================================

    /// <summary>
    /// Costruisce la griglia di celle per una frase, usando il tabellone configurato
    /// se la frase ci entra e la griglia estesa altrimenti.
    /// </summary>
    /// <param name="phrase">La frase, già in maiuscolo.</param>
    /// <param name="rowWidths">Larghezze delle righe configurate, in celle.</param>
    /// <returns>
    /// Le righe di celle, di forma fissa: una volta costruita, la griglia non cambia
    /// più struttura e solo lo stato delle singole celle si muove. Questo permette a
    /// Display, Admin e telefono di leggerla insieme senza sincronizzazione.
    /// </returns>
    public static BoardCell?[][] BuildBoard(string phrase, int[] rowWidths)
    {
        var words = SplitWords(phrase);

        // 1) La frase entra nel tabellone configurato: la si colloca centrata.
        if (TryPlace(words, rowWidths) is { } placed)
        {
            var (lines, offset) = placed;
            int totalCols = MaxWidthOrDefault(rowWidths);
            var grid = new BoardCell?[rowWidths.Length][];

            for (int row = 0; row < rowWidths.Length; row++)
            {
                int lineIndex = row - offset;
                var text = lineIndex >= 0 && lineIndex < lines.Count ? lines[lineIndex] : "";
                grid[row] = BuildRow(text, rowWidths[row], totalCols);
            }

            return grid;
        }

        // 2) Non entra: griglia estesa, che non taglia e non spezza mai nulla.
        int cols = ExpandedWidth(words, rowWidths);
        var wrapped = Wrap(words, RowSpec.Uniform(cols, ExpandedMaxRows)) ?? HardWrap(phrase, cols);
        return wrapped.Select(line => BuildRow(line, cols, cols)).ToArray();
    }

    /// <summary>
    /// Misura una frase rispetto al tabellone configurato, per dire in regia quali
    /// frasi entrano e quante righe servirebbero alle altre.
    /// </summary>
    /// <returns>
    /// <c>Fits</c> = la frase entra nel tabellone configurato;
    /// <c>LineCount</c> = righe occupate se entra, righe necessarie sulla griglia
    /// estesa se non entra.
    /// </returns>
    public static (bool Fits, int LineCount) Measure(string phrase, int[] rowWidths)
    {
        var words = SplitWords(phrase);

        if (TryPlace(words, rowWidths) is { } placed)
            return (true, placed.Lines.Count);

        int cols = ExpandedWidth(words, rowWidths);
        int lines = Wrap(words, RowSpec.Uniform(cols, ExpandedMaxRows))?.Count ?? 1;
        return (false, lines);
    }

    /// <summary>
    /// Cerca la collocazione verticale più centrata della frase nelle righe date.
    /// Restituisce le righe impaginate e da quale riga del tabellone partono,
    /// oppure <c>null</c> se la frase non ci sta in nessun modo.
    /// </summary>
    private static (List<string> Lines, int Offset)? TryPlace(string[] words, int[] rowWidths)
    {
        if (rowWidths.Length == 0)
            return null;

        List<string>? best = null;
        int bestOffset = 0;
        double bestDistanceFromCenter = double.MaxValue;

        // Provo a far partire la frase da ogni riga e tengo la collocazione il cui
        // centro cade più vicino al centro del tabellone.
        for (int offset = 0; offset < rowWidths.Length; offset++)
        {
            var lines = Wrap(words, RowSpec.Slice(rowWidths, offset));
            if (lines is null)
                continue;

            double distance = Math.Abs(offset + lines.Count / 2.0 - rowWidths.Length / 2.0);
            if (distance < bestDistanceFromCenter)
            {
                bestDistanceFromCenter = distance;
                best = lines;
                bestOffset = offset;
            }
        }

        return best is null ? null : (best, bestOffset);
    }

    /// <summary>
    /// Larghezza della griglia estesa: la più grande fra le righe configurate e la
    /// parola più lunga, così nessuna parola viene mai spezzata.
    /// </summary>
    private static int ExpandedWidth(string[] words, int[] rowWidths)
    {
        int configured = MaxWidthOrDefault(rowWidths);
        int longestWord = words.Length == 0 ? 0 : words.Max(w => w.Length);
        return Math.Max(configured, longestWord);
    }

    private static int MaxWidthOrDefault(int[] rowWidths) =>
        rowWidths.Length == 0 ? DefaultWidth : rowWidths.Max();

    /// <summary>
    /// Manda a capo le parole nelle righe date, senza spezzarne nessuna.
    /// Restituisce <c>null</c> se le parole non ci stanno.
    /// </summary>
    /// <remarks>
    /// Una riga troppo stretta per la parola successiva viene lasciata vuota e si
    /// passa alla seguente: è così che una parola da 14 lettere "salta" la riga
    /// da 12 del tabellone classico.
    /// </remarks>
    private static List<string>? Wrap(string[] words, RowSpec rows)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        int row = 0;

        foreach (var word in words)
        {
            // Nessuna riga è larga abbastanza, o le righe sono finite: non ci sta.
            if (row >= rows.Count || word.Length > rows.MaxWidth)
                return null;

            if (current.Length == 0)
            {
                if (!SkipTooNarrowRows(word, rows, lines, ref row))
                    return null;

                current.Append(word);
            }
            else if (current.Length + 1 + word.Length <= rows[row])
            {
                // La parola sta in coda a quella corrente, spazio incluso.
                current.Append(' ').Append(word);
            }
            else
            {
                // Riga piena: la chiudo e ricomincio da quella dopo.
                lines.Add(current.ToString());
                current.Clear();
                row++;

                if (!SkipTooNarrowRows(word, rows, lines, ref row))
                    return null;

                current.Append(word);
            }
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }

    /// <summary>
    /// Salta, lasciandole vuote, le righe troppo strette per la parola.
    /// Restituisce <c>false</c> se finiscono le righe disponibili.
    /// </summary>
    private static bool SkipTooNarrowRows(string word, RowSpec rows, List<string> lines, ref int row)
    {
        while (row < rows.Count && word.Length > rows[row])
        {
            lines.Add("");
            row++;
        }

        return row < rows.Count;
    }

    /// <summary>
    /// Rete di sicurezza: spezza la frase per carattere in righe larghe
    /// <paramref name="width"/>, senza perdere nulla. Si usa solo per frasi anomale,
    /// per esempio una parola sola più lunga dell'intera griglia estesa.
    /// </summary>
    private static List<string> HardWrap(string phrase, int width)
    {
        width = Math.Max(1, width);
        var lines = new List<string>();

        for (int i = 0; i < phrase.Length; i += width)
            lines.Add(phrase.Substring(i, Math.Min(width, phrase.Length - i)));

        return lines.Count > 0 ? lines : new List<string> { "" };
    }

    // ============================================================
    //  Celle
    // ============================================================

    /// <summary>
    /// Costruisce una riga di celle: il testo centrato nella riga, la riga centrata
    /// nella griglia. Le posizioni fuori dalla riga valgono <c>null</c>, così una
    /// riga da 12 celle resta centrata in una griglia da 14 senza celle finte ai lati.
    /// </summary>
    private static BoardCell?[] BuildRow(string text, int rowWidth, int totalCols)
    {
        var row = new List<BoardCell?>(totalCols);
        int innerPad = (rowWidth - text.Length) / 2;   // testo centrato nella riga
        int outerPad = (totalCols - rowWidth) / 2;     // riga centrata nella griglia

        for (int i = 0; i < outerPad; i++) row.Add(null);
        for (int i = 0; i < innerPad; i++) row.Add(CreateEmptyCell());

        foreach (var ch in text)
            row.Add(CreateCell(ch));

        while (row.Count < outerPad + rowWidth) row.Add(CreateEmptyCell());
        while (row.Count < totalCols) row.Add(null);

        return row.ToArray();
    }

    /// <summary>Una casella verde: dentro al tabellone, ma senza carattere.</summary>
    private static BoardCell CreateEmptyCell() => new() { Display = ' ', Match = ' ' };

    /// <summary>Costruisce la cella giusta per un carattere della frase.</summary>
    private static BoardCell CreateCell(char ch)
    {
        // Lettera, accenti compresi: da indovinare.
        if (NormalizeLetter(ch) is { } baseLetter)
            return new BoardCell { IsLetter = true, Display = ch, Match = baseLetter };

        if (ch == ' ')
            return CreateEmptyCell();

        // Punteggiatura, apostrofi e cifre: in chiaro dall'inizio.
        return new BoardCell { Display = ch, Match = ' ', Revealed = true };
    }

    // ============================================================
    //  Righe del tabellone
    // ============================================================

    /// <summary>
    /// Le righe su cui impaginare, viste come sola lettura. Evita di copiare array
    /// a ogni tentativo di collocazione e tiene pronta la larghezza massima, che
    /// serve a ogni parola.
    /// </summary>
    private readonly struct RowSpec
    {
        private readonly int[]? _widths;   // null = tutte le righe larghe uguali
        private readonly int _offset;
        private readonly int _uniformWidth;

        private RowSpec(int[]? widths, int offset, int uniformWidth, int count, int maxWidth)
        {
            _widths = widths;
            _offset = offset;
            _uniformWidth = uniformWidth;
            Count = count;
            MaxWidth = maxWidth;
        }

        /// <summary>Quante righe sono disponibili.</summary>
        public int Count { get; }

        /// <summary>La larghezza della riga più larga.</summary>
        public int MaxWidth { get; }

        /// <summary>Larghezza della riga <paramref name="index"/>-esima.</summary>
        public int this[int index] => _widths is null ? _uniformWidth : _widths[_offset + index];

        /// <summary>Le righe configurate, a partire dalla <paramref name="offset"/>-esima.</summary>
        public static RowSpec Slice(int[] widths, int offset)
        {
            int max = 0;
            for (int i = offset; i < widths.Length; i++)
                max = Math.Max(max, widths[i]);

            return new RowSpec(widths, offset, 0, widths.Length - offset, max);
        }

        /// <summary><paramref name="count"/> righe tutte larghe <paramref name="width"/>.</summary>
        public static RowSpec Uniform(int width, int count) => new(null, 0, width, count, width);
    }
}
