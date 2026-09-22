using RuotaDellaFortuna.Models;

namespace RuotaDellaFortuna.Services;

/// <summary>
/// Lo stato della partita, condiviso da tutte le pagine aperte.
/// </summary>
/// <remarks>
/// <para>
/// È registrato come <b>singleton</b>: esiste una sola partita per processo e tutte
/// le pagine ne vedono la stessa istanza. La regia (<c>/admin</c>) comanda, il
/// tabellone (<c>/display</c>) mostra, telefono (<c>/remote</c>) ed endpoint HTTP
/// (<c>/api/...</c>) possono chiamare lettere. Chi vuole restare aggiornato si
/// iscrive a <see cref="OnChange"/>.
/// </para>
/// <para>
/// <b>Concorrenza.</b> Le pagine aperte sono più di una e gli endpoint HTTP arrivano
/// da thread diversi, quindi ogni scrittura passa da <c>_lock</c>. Le due regole da
/// rispettare quando si aggiunge un metodo:
/// </para>
/// <list type="number">
///   <item>
///     un metodo che scrive prende il lock e delega a una variante <c>...Locked()</c>
///     se deve riusare altra logica di scrittura, così il lock non viene mai preso due volte;
///   </item>
///   <item>
///     <see cref="Notify"/> si chiama <b>fuori</b> dal lock: se lo si chiamasse dentro,
///     il re-render dei componenti avverrebbe a lock tenuto.
///   </item>
/// </list>
/// <para>
/// Le raccolte esposte ai componenti (<see cref="Rows"/>, <see cref="CalledLetters"/>,
/// <see cref="Players"/>) sono array sostituiti in blocco, non liste modificate sul
/// posto: un componente può quindi scorrerle mentre un altro thread aggiorna la
/// partita, senza rischiare l'eccezione da "collezione modificata".
/// </para>
/// </remarks>
public sealed class BoardService : IDisposable
{
    /// <summary>Quanto resta accesa in giallo una cella appena rivelata.</summary>
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(1500);

    /// <summary>Numero minimo e massimo di concorrenti.</summary>
    private const int MinPlayers = 1;

    /// <inheritdoc cref="MinPlayers"/>
    private const int MaxPlayers = 4;

    /// <summary>Caratteri ammessi come separatore fra le larghezze di riga.</summary>
    private static readonly char[] RowSeparators = { ' ', ',', ';', '\t', '·', 'x', 'X', '*' };

    private readonly object _lock = new();
    private readonly Random _random = new();
    private readonly PuzzleLibrary _library;
    private readonly ILogger<BoardService> _logger;

    private System.Timers.Timer? _flashTimer;
    private bool _disposed;

    /// <summary>
    /// Crea il servizio e seleziona il primo tema disponibile, così la regia si apre
    /// già con delle frasi pronte invece che vuota.
    /// </summary>
    public BoardService(IWebHostEnvironment environment, ILogger<BoardService> logger)
    {
        _logger = logger;
        _library = new PuzzleLibrary(Path.Combine(environment.ContentRootPath, "frasi"));

        if (_library.Themes.Count > 0)
            UseProgramThemes(new[] { _library.Themes[0].Id });
        else
            _logger.LogWarning("Nessun tema trovato in {Path}: la regia partirà senza frasi.", _library.ThemesPath);
    }

    /// <summary>Scatta a ogni cambiamento di stato: le pagine si ridisegnano.</summary>
    public event Action? OnChange;

    // ============================================================
    //  Configurazione del tabellone
    // ============================================================

    /// <summary>Se mostrare la categoria sopra al tabellone.</summary>
    public bool ShowCategory { get; private set; } = true;

    /// <summary>Se mostrare sul tabellone la striscia delle lettere già chiamate.</summary>
    public bool ShowCalledOnDisplay { get; private set; } = true;

    /// <summary>Se mostrare sul tabellone i punteggi dei concorrenti.</summary>
    public bool ShowScoresOnDisplay { get; private set; } = true;

    /// <summary>Se mandare a tabellone le frasi in ordine casuale.</summary>
    public bool Shuffle { get; private set; }

    /// <summary>Forma del tabellone: televisiva o personalizzata.</summary>
    public BoardLayout Layout { get; private set; } = BoardLayout.Classic;

    /// <summary>Larghezze delle righe del tabellone personalizzato.</summary>
    public int[] CustomRowWidths { get; private set; } = { 12, 14, 14, 12 };

    /// <summary>Le righe personalizzate come testo, per il campo in regia.</summary>
    public string CustomRowsText => string.Join(" ", CustomRowWidths);

    /// <summary>
    /// Lettere rivelate appena la frase va a tabellone, in stile "RSTLNE" della
    /// manche finale. Stringa vuota per non regalare nulla.
    /// </summary>
    public string PreRevealed { get; private set; } = "";

    /// <summary>Da dove arrivano le frasi attualmente caricate.</summary>
    public PuzzleSource Source { get; private set; } = PuzzleSource.Program;

    /// <summary>Le righe del tabellone attivo, secondo il layout scelto.</summary>
    private int[] ActiveRowWidths =>
        Layout == BoardLayout.Classic ? BoardLayoutEngine.ClassicRowWidths : CustomRowWidths;

    /// <summary>Descrizione del layout attivo, per la regia.</summary>
    public string LayoutDescription => Layout == BoardLayout.Classic
        ? "Classico 12·14·14·12"
        : $"Personalizzato {string.Join("·", CustomRowWidths)}";

    // ============================================================
    //  Frasi caricate
    // ============================================================

    private Puzzle[] _loadedPuzzles = Array.Empty<Puzzle>();
    private Puzzle[] _playOrder = Array.Empty<Puzzle>();
    private PuzzleStatus[] _statuses = Array.Empty<PuzzleStatus>();
    private HashSet<string> _selectedThemeIds = new(StringComparer.OrdinalIgnoreCase);
    private int _puzzleIndex = -1;

    /// <summary>I temi inclusi nel programma.</summary>
    public IReadOnlyList<PuzzleThemeInfo> AvailableThemes => _library.Themes;

    /// <summary>Gli identificatori dei temi attualmente spuntati in regia.</summary>
    public IReadOnlyCollection<string> SelectedThemeIds => _selectedThemeIds;

    /// <summary>Quante frasi sono caricate.</summary>
    public int PuzzleCount => _loadedPuzzles.Length;

    /// <summary>Riepilogo leggibile di cosa è caricato, mostrato in regia.</summary>
    public string PuzzlesInfo { get; private set; } = "Nessuna frase caricata";

    /// <summary>Per ogni frase caricata, se entra nel tabellone configurato.</summary>
    public IReadOnlyList<PuzzleStatus> PuzzleStatuses => _statuses;

    /// <summary>Quante frasi entrano nel tabellone configurato.</summary>
    public int FittingCount { get; private set; }

    /// <summary>Quante frasi richiedono la griglia estesa.</summary>
    public int OversizeCount => _statuses.Length - FittingCount;

    /// <summary>Le frasi che richiedono la griglia estesa.</summary>
    public IEnumerable<PuzzleStatus> OversizePuzzles => _statuses.Where(s => !s.Fits);

    // ============================================================
    //  Stato della frase in gioco
    // ============================================================

    /// <summary>Fase corrente del tabellone.</summary>
    public BoardStatus Status { get; private set; } = BoardStatus.Idle;

    /// <summary>Le righe di celle da disegnare. Vuoto quando non c'è nessuna frase.</summary>
    public BoardCell?[][] Rows { get; private set; } = Array.Empty<BoardCell?[]>();

    /// <summary>Categoria della frase in gioco.</summary>
    public string Category { get; private set; } = "";

    /// <summary>Testo della frase in gioco, in chiaro: da mostrare solo in regia.</summary>
    public string Phrase { get; private set; } = "";

    private CalledLetter[] _called = Array.Empty<CalledLetter>();

    /// <summary>Le lettere chiamate in questa frase, nell'ordine di chiamata.</summary>
    public IReadOnlyList<CalledLetter> CalledLetters => _called;

    /// <summary>L'ultima lettera chiamata, o <c>null</c> se non ne è stata chiamata nessuna.</summary>
    public char? LastCalled { get; private set; }

    /// <summary>Quante celle ha scoperto l'ultima lettera chiamata.</summary>
    public int LastCalledCount { get; private set; }

    /// <summary>Numero della frase in gioco, contando da 1; <c>0</c> se la partita non è avviata.</summary>
    public int PuzzleNumber => _puzzleIndex + 1;

    /// <summary>Quante frasi compongono la partita avviata.</summary>
    public int PuzzlesTotal => _playOrder.Length;

    /// <summary>Vero se c'è un'altra frase dopo quella in gioco.</summary>
    public bool HasNextPuzzle => _puzzleIndex >= 0 && _puzzleIndex + 1 < _playOrder.Length;

    /// <summary>Vero se c'è una frase a tabellone, risolta o ancora da indovinare.</summary>
    public bool IsActive => Status is BoardStatus.Playing or BoardStatus.Solved;

    /// <summary>Quante celle restano coperte.</summary>
    public int LettersRemaining =>
        Rows.Sum(row => row.Count(cell => cell is { IsLetter: true, Revealed: false }));

    /// <summary>Quante celle sono state scoperte, sul totale delle lettere della frase.</summary>
    public int LettersRevealed =>
        Rows.Sum(row => row.Count(cell => cell is { IsLetter: true, Revealed: true }));

    /// <summary>Vero se la lettera è già stata chiamata in questa frase.</summary>
    public bool WasCalled(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        var called = _called;   // copia il riferimento: l'array non viene mai modificato sul posto

        foreach (var entry in called)
            if (entry.Letter == upper)
                return true;

        return false;
    }

    /// <summary>Vero se la lettera si può chiamare adesso.</summary>
    public bool CanCall(char letter) => Status == BoardStatus.Playing && !WasCalled(letter);

    // ============================================================
    //  Concorrenti
    // ============================================================

    private Player[] _players =
    {
        new() { Name = "Giocatore 1" },
        new() { Name = "Giocatore 2" },
        new() { Name = "Giocatore 3" },
    };

    /// <summary>I concorrenti, nell'ordine in cui si alternano.</summary>
    public IReadOnlyList<Player> Players => _players;

    /// <summary>Indice del concorrente di turno.</summary>
    public int ActivePlayer { get; private set; }

    // ============================================================
    //  Configurazione
    // ============================================================

    /// <summary>
    /// Applica i settaggi del tabellone. Il layout vale dalla frase successiva:
    /// quella già a tabellone non viene reimpaginata sotto gli occhi dei concorrenti.
    /// </summary>
    /// <param name="showCategory">Mostrare la categoria sul tabellone.</param>
    /// <param name="showCalled">Mostrare le lettere chiamate sul tabellone.</param>
    /// <param name="showScores">Mostrare i punteggi sul tabellone.</param>
    /// <param name="shuffle">Mandare le frasi in ordine casuale.</param>
    /// <param name="layout">Forma del tabellone.</param>
    /// <param name="customRows">Larghezze delle righe personalizzate, es. <c>"12 14 14 12"</c>.</param>
    /// <param name="preRevealed">Lettere da regalare a inizio frase, es. <c>"RSTLNE"</c>.</param>
    public void Configure(bool showCategory, bool showCalled, bool showScores, bool shuffle,
                          BoardLayout layout, string? customRows, string? preRevealed)
    {
        lock (_lock)
        {
            ShowCategory = showCategory;
            ShowCalledOnDisplay = showCalled;
            ShowScoresOnDisplay = showScores;
            Shuffle = shuffle;
            Layout = layout;

            // Se le righe scritte a mano sono illeggibili si tengono le precedenti,
            // invece di ritrovarsi con un tabellone senza righe.
            var parsed = ParseRowWidths(customRows);
            if (parsed.Length > 0)
                CustomRowWidths = parsed;

            PreRevealed = NormalizePreRevealed(preRevealed);

            // Cambiare layout cambia quali frasi ci entrano: ricontrolliamole.
            RevalidatePuzzlesLocked();
        }

        Notify();
    }

    /// <summary>
    /// Legge le larghezze di riga da una stringa tipo <c>"12 14 14 12"</c>, tollerando
    /// virgole, punti e separatori vari. I valori fuori scala vengono riportati nei
    /// limiti ammessi e le righe in eccesso scartate.
    /// </summary>
    private static int[] ParseRowWidths(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<int>();

        return text
            .Split(RowSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var width)
                ? Math.Clamp(width, BoardLayoutEngine.MinRowWidth, BoardLayoutEngine.MaxRowWidth)
                : 0)
            .Where(width => width > 0)
            .Take(BoardLayoutEngine.MaxRows)
            .ToArray();
    }

    /// <summary>Tiene solo lettere A–Z, in maiuscolo e senza ripetizioni.</summary>
    private static string NormalizePreRevealed(string? text) =>
        new((text ?? "")
            .ToUpperInvariant()
            .Where(char.IsAsciiLetterUpper)
            .Distinct()
            .ToArray());

    // ============================================================
    //  Caricamento delle frasi
    // ============================================================

    /// <summary>Carica le frasi dei temi indicati, unendole nell'ordine della cartella.</summary>
    /// <returns>Se c'è almeno una frase, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) UseProgramThemes(IEnumerable<string> themeIds)
    {
        var ids = new HashSet<string>(themeIds, StringComparer.OrdinalIgnoreCase);
        var puzzles = _library.GetPuzzles(ids);
        var names = _library.GetThemeNames(ids);

        var message = names.Count == 0
            ? "Nessun tema selezionato"
            : $"{(names.Count == 1 ? "Tema" : "Temi")}: {string.Join(", ", names)} — {puzzles.Count} frasi";

        lock (_lock)
        {
            _selectedThemeIds = ids;
            LoadPuzzlesLocked(puzzles, PuzzleSource.Program, message);
        }

        Notify();
        return (puzzles.Count > 0, message);
    }

    /// <summary>Carica le frasi da un file <c>.txt</c> scelto dall'utente.</summary>
    /// <returns>Se c'è almeno una frase, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) SetPuzzlesFromUpload(string fileName, string content)
    {
        var puzzles = PuzzleLibrary.ParsePuzzles(content);
        var message = $"File \"{fileName}\" — {puzzles.Count} frasi";

        lock (_lock)
            LoadPuzzlesLocked(puzzles, PuzzleSource.File, message);

        Notify();
        return (puzzles.Count > 0, message);
    }

    /// <summary>Carica le frasi incollate a mano in regia.</summary>
    /// <returns>Se c'è almeno una frase, e il riepilogo da mostrare in regia.</returns>
    public (bool Ok, string Message) SetPuzzlesFromText(string text)
    {
        var puzzles = PuzzleLibrary.ParsePuzzles(text);
        var message = $"Frasi inserite a mano — {puzzles.Count} frasi";

        lock (_lock)
            LoadPuzzlesLocked(puzzles, PuzzleSource.Manual, message);

        Notify();
        return (puzzles.Count > 0, message);
    }

    private void LoadPuzzlesLocked(List<Puzzle> puzzles, PuzzleSource source, string message)
    {
        _loadedPuzzles = puzzles.ToArray();
        Source = source;
        PuzzlesInfo = message;
        RevalidatePuzzlesLocked();
    }

    /// <summary>
    /// Ricalcola, per ogni frase caricata, se entra nel tabellone configurato.
    /// Va chiamato tenendo il lock: legge le frasi e le righe attive.
    /// </summary>
    private void RevalidatePuzzlesLocked()
    {
        var widths = ActiveRowWidths;
        var statuses = new PuzzleStatus[_loadedPuzzles.Length];
        int fitting = 0;

        for (int i = 0; i < _loadedPuzzles.Length; i++)
        {
            var puzzle = _loadedPuzzles[i];
            var (fits, lineCount) = BoardLayoutEngine.Measure(puzzle.Phrase, widths);

            statuses[i] = new PuzzleStatus(puzzle, fits, lineCount);
            if (fits)
                fitting++;
        }

        _statuses = statuses;
        FittingCount = fitting;
    }

    // ============================================================
    //  Conduzione della partita
    // ============================================================

    /// <summary>
    /// Avvia la partita: fissa l'ordine delle frasi (eventualmente mescolandole) e
    /// manda la prima a tabellone. Senza frasi caricate non fa nulla.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_loadedPuzzles.Length == 0)
                return;

            _playOrder = Shuffle ? ShuffledPuzzles() : _loadedPuzzles.ToArray();
            _puzzleIndex = 0;
            LoadCurrentPuzzleLocked();
        }

        ScheduleFlashClear();
        Notify();
    }

    /// <summary>Mescola le frasi caricate senza toccarne l'ordine originale.</summary>
    private Puzzle[] ShuffledPuzzles()
    {
        var shuffled = _loadedPuzzles.ToArray();

        // Fisher-Yates: ogni ordine è equiprobabile.
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// <summary>Manda a tabellone la frase successiva, se c'è.</summary>
    public void NextPuzzle()
    {
        lock (_lock)
        {
            if (!HasNextPuzzle)
                return;

            _puzzleIndex++;
            LoadCurrentPuzzleLocked();
        }

        ScheduleFlashClear();
        Notify();
    }

    /// <summary>Ricopre la frase in gioco e la riparte da zero.</summary>
    public void RestartPuzzle()
    {
        lock (_lock)
        {
            if (_puzzleIndex < 0)
                return;

            LoadCurrentPuzzleLocked();
        }

        ScheduleFlashClear();
        Notify();
    }

    private void LoadCurrentPuzzleLocked()
    {
        var puzzle = _playOrder[_puzzleIndex];

        Category = puzzle.Category;
        Phrase = puzzle.Phrase;
        Rows = BoardLayoutEngine.BuildBoard(puzzle.Phrase, ActiveRowWidths);
        Status = BoardStatus.Playing;

        _called = Array.Empty<CalledLetter>();
        LastCalled = null;
        LastCalledCount = 0;

        // Le lettere regalate si scoprono subito, senza contare come chiamate.
        foreach (var letter in PreRevealed)
            RevealMatchingCellsLocked(letter);

        UpdateSolvedStateLocked();
    }

    /// <summary>
    /// Chiama una lettera: scopre tutte le celle corrispondenti e la registra fra le
    /// chiamate. Se la lettera non c'è, il turno passa al concorrente successivo.
    /// </summary>
    /// <returns>
    /// <c>Ok</c> = la chiamata è stata accettata (falso se la partita non è in corso
    /// o la lettera era già stata chiamata); <c>Count</c> = celle scoperte.
    /// </returns>
    public (bool Ok, int Count) CallLetter(char letter)
    {
        int revealed;

        lock (_lock)
        {
            if (Status != BoardStatus.Playing)
                return (false, 0);

            if (BoardLayoutEngine.NormalizeLetter(letter) is not { } upper || WasCalled(upper))
                return (false, 0);

            ClearFlashesLocked();
            revealed = RevealMatchingCellsLocked(upper);

            _called = _called.Append(new CalledLetter(upper, revealed)).ToArray();
            LastCalled = upper;
            LastCalledCount = revealed;

            // Lettera presente: il turno resta. Lettera assente: passa al prossimo.
            if (revealed == 0)
                AdvanceTurnLocked();

            UpdateSolvedStateLocked();
        }

        ScheduleFlashClear();
        Notify();
        return (true, revealed);
    }

    /// <summary>
    /// Scopre una singola cella, per dare un aiuto mirato senza regalare
    /// tutte le celle con quella lettera.
    /// </summary>
    public void RevealCell(int row, int column)
    {
        lock (_lock)
        {
            if (Status != BoardStatus.Playing)
                return;

            if (row < 0 || row >= Rows.Length || column < 0 || column >= Rows[row].Length)
                return;

            if (Rows[row][column] is not { IsLetter: true, Revealed: false } cell)
                return;

            ClearFlashesLocked();
            cell.Revealed = true;
            cell.Flash = true;

            UpdateSolvedStateLocked();
        }

        ScheduleFlashClear();
        Notify();
    }

    /// <summary>Scopre tutta la frase: un concorrente ha risolto, o la manche è finita.</summary>
    public void Solve()
    {
        lock (_lock)
        {
            if (Status != BoardStatus.Playing)
                return;

            ClearFlashesLocked();

            foreach (var row in Rows)
                foreach (var cell in row)
                    if (cell is { IsLetter: true, Revealed: false })
                    {
                        cell.Revealed = true;
                        cell.Flash = true;
                    }

            Status = BoardStatus.Solved;
        }

        ScheduleFlashClear();
        Notify();
    }

    /// <summary>Svuota il tabellone e riporta la partita al punto di partenza.</summary>
    /// <remarks>I punteggi non vengono azzerati: per quello c'è <see cref="ResetScores"/>.</remarks>
    public void Reset()
    {
        lock (_lock)
        {
            Status = BoardStatus.Idle;
            Rows = Array.Empty<BoardCell?[]>();
            Category = "";
            Phrase = "";
            _called = Array.Empty<CalledLetter>();
            LastCalled = null;
            LastCalledCount = 0;
            _puzzleIndex = -1;
            _playOrder = Array.Empty<Puzzle>();
        }

        Notify();
    }

    /// <summary>Scopre tutte le celle con questa lettera e restituisce quante erano.</summary>
    private int RevealMatchingCellsLocked(char upper)
    {
        int count = 0;

        foreach (var row in Rows)
            foreach (var cell in row)
                if (cell is { IsLetter: true, Revealed: false } && cell.Match == upper)
                {
                    cell.Revealed = true;
                    cell.Flash = true;
                    count++;
                }

        return count;
    }

    private void ClearFlashesLocked()
    {
        foreach (var row in Rows)
            foreach (var cell in row)
                if (cell is not null)
                    cell.Flash = false;
    }

    /// <summary>Se non resta nessuna cella coperta, la frase è risolta.</summary>
    private void UpdateSolvedStateLocked()
    {
        foreach (var row in Rows)
            foreach (var cell in row)
                if (cell is { IsLetter: true, Revealed: false })
                    return;

        Status = BoardStatus.Solved;
    }

    /// <summary>
    /// Programma lo spegnimento dell'evidenziazione gialla. Il timer viene sostituito
    /// a ogni chiamata, così più lettere chiamate di seguito non si pestano i piedi.
    /// </summary>
    private void ScheduleFlashClear()
    {
        System.Timers.Timer timer;

        lock (_lock)
        {
            if (_disposed)
                return;

            _flashTimer?.Dispose();

            timer = new System.Timers.Timer(FlashDuration.TotalMilliseconds) { AutoReset = false };
            timer.Elapsed += (_, _) =>
            {
                lock (_lock)
                    ClearFlashesLocked();

                Notify();
            };

            _flashTimer = timer;
        }

        timer.Start();
    }

    private void Notify() => OnChange?.Invoke();

    // ============================================================
    //  Concorrenti e turni
    // ============================================================

    /// <summary>
    /// Cambia il numero di concorrenti (da 1 a 4). Aggiungendone si parte da zero
    /// punti; togliendone si perdono i punti di quelli in coda.
    /// </summary>
    public void SetPlayerCount(int count)
    {
        lock (_lock)
        {
            count = Math.Clamp(count, MinPlayers, MaxPlayers);
            if (count == _players.Length)
                return;

            var players = new Player[count];
            for (int i = 0; i < count; i++)
                players[i] = i < _players.Length
                    ? _players[i]
                    : new Player { Name = $"Giocatore {i + 1}" };

            _players = players;

            if (ActivePlayer >= count)
                ActivePlayer = 0;
        }

        Notify();
    }

    /// <summary>Rinomina un concorrente; un nome vuoto torna a "Giocatore N".</summary>
    public void SetPlayerName(int index, string? name)
    {
        lock (_lock)
        {
            if (!IsValidPlayer(index))
                return;

            _players[index].Name = string.IsNullOrWhiteSpace(name)
                ? $"Giocatore {index + 1}"
                : name.Trim();
        }

        Notify();
    }

    /// <summary>Somma (o sottrae) punti a un concorrente, senza scendere sotto zero.</summary>
    public void AddScore(int index, int delta)
    {
        lock (_lock)
        {
            if (!IsValidPlayer(index))
                return;

            _players[index].Score = Math.Max(0, _players[index].Score + delta);
        }

        Notify();
    }

    /// <summary>Azzera il punteggio di un solo concorrente.</summary>
    public void ClearScore(int index)
    {
        lock (_lock)
        {
            if (!IsValidPlayer(index))
                return;

            _players[index].Score = 0;
        }

        Notify();
    }

    /// <summary>Azzera i punteggi di tutti.</summary>
    public void ResetScores()
    {
        lock (_lock)
            foreach (var player in _players)
                player.Score = 0;

        Notify();
    }

    /// <summary>Assegna il turno a un concorrente, se è attivo.</summary>
    public void SetActivePlayer(int index)
    {
        lock (_lock)
        {
            if (!IsValidPlayer(index) || !_players[index].Enabled)
                return;

            ActivePlayer = index;
        }

        Notify();
    }

    /// <summary>
    /// Attiva o disattiva un concorrente. Chi è disattivato resta sul tabellone in
    /// grigio ma viene saltato nel giro dei turni.
    /// </summary>
    public void TogglePlayerEnabled(int index)
    {
        lock (_lock)
        {
            if (!IsValidPlayer(index))
                return;

            _players[index].Enabled = !_players[index].Enabled;

            // Se ho appena messo fuori chi era di turno, il turno passa.
            if (!_players[index].Enabled && index == ActivePlayer)
                AdvanceTurnLocked();
        }

        Notify();
    }

    /// <summary>Passa il turno al prossimo concorrente attivo.</summary>
    public void AdvanceTurn()
    {
        lock (_lock)
            AdvanceTurnLocked();

        Notify();
    }

    /// <summary>
    /// Sposta il turno sul prossimo concorrente attivo. Se non ce n'è nessun altro
    /// (per esempio sono tutti disattivati) il turno resta dov'è.
    /// </summary>
    private void AdvanceTurnLocked()
    {
        for (int step = 1; step <= _players.Length; step++)
        {
            int candidate = (ActivePlayer + step) % _players.Length;
            if (_players[candidate].Enabled)
            {
                ActivePlayer = candidate;
                return;
            }
        }
    }

    private bool IsValidPlayer(int index) => index >= 0 && index < _players.Length;

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _flashTimer?.Dispose();
            _flashTimer = null;
        }
    }
}
