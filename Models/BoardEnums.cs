namespace RuotaDellaFortuna.Models;

/// <summary>Fase in cui si trova il tabellone.</summary>
public enum BoardStatus
{
    /// <summary>Nessuna frase a tabellone: il Display mostra la schermata di attesa.</summary>
    Idle,

    /// <summary>Frase a tabellone con ancora lettere da scoprire: si possono chiamare lettere.</summary>
    Playing,

    /// <summary>Frase completamente rivelata: si attende la frase successiva o il reset.</summary>
    Solved
}

/// <summary>Da dove arrivano le frasi caricate in regia.</summary>
public enum PuzzleSource
{
    /// <summary>Temi inclusi nel programma, un file <c>.txt</c> per tema nella cartella <c>frasi/</c>.</summary>
    Program,

    /// <summary>File <c>.txt</c> caricato dall'utente dal pannello Admin.</summary>
    File,

    /// <summary>Frasi incollate a mano nel pannello Admin.</summary>
    Manual
}

/// <summary>Forma del tabellone su cui impaginare le frasi.</summary>
public enum BoardLayout
{
    /// <summary>Tabellone televisivo: quattro righe da 12 · 14 · 14 · 12 celle.</summary>
    Classic,

    /// <summary>Righe di larghezza scelta dall'utente (es. <c>10 12 12 10</c>).</summary>
    Custom
}
