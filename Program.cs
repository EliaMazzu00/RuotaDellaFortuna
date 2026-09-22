using RuotaDellaFortuna.Components;
using RuotaDellaFortuna.Models;
using RuotaDellaFortuna.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server: l'interfaccia vive sul server e arriva al browser via SignalR,
// così tutte le pagine aperte restano sincronizzate senza scrivere una riga di API.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Una sola partita per processo, condivisa da regia, tabellone e telefono.
builder.Services.AddSingleton<BoardService>();

// In ascolto su tutte le interfacce di rete, così gli altri dispositivi della LAN
// possono aprire /display o /remote puntando all'IP di questo PC.
// La porta si cambia da appsettings.json ("Server:Port") o con la variabile
// d'ambiente Server__Port, senza ricompilare.
int port = builder.Configuration.GetValue("Server:Port", 5090);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Niente redirect a HTTPS: l'app gira in HTTP semplice sulla rete locale, dove un
// certificato non sarebbe verificabile dagli altri dispositivi.
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

MapGameApi(app);

app.Logger.LogInformation("La Ruota della Fortuna è in ascolto sulla porta {Port}.", port);
app.Run();

// ================================================================
//  API di gioco
// ================================================================

// Espone i comandi di gioco come endpoint HTTP, per chiamare le lettere da qualcosa
// che non sia una pagina web: una pulsantiera, un ESP32, un tasto macro, uno script.
//
// Gli endpoint sono SENZA AUTENTICAZIONE e senza antiforgery: è una scelta voluta,
// perché un microcontrollore non può gestire né login né token. Vale finché
// l'applicazione resta su una rete locale di cui si ha il controllo: non va esposta
// su Internet.
static void MapGameApi(WebApplication app)
{
    var api = app.MapGroup("/api").DisableAntiforgery();

    // GET|POST /api/lettera/{lettera} — chiama una lettera e dice quante celle ha scoperto.
    api.MapMethods("/lettera/{lettera}", new[] { "GET", "POST" }, (string lettera, BoardService board) =>
    {
        if (string.IsNullOrWhiteSpace(lettera))
            return Results.BadRequest(new { ok = false, errore = "lettera mancante" });

        // Si accetta anche una lettera accentata: conta la lettera base.
        if (BoardLayoutEngine.NormalizeLetter(lettera[0]) is not { } letter)
            return Results.BadRequest(new { ok = false, errore = $"\"{lettera}\" non è una lettera" });

        var (ok, count) = board.CallLetter(letter);

        return Results.Json(new
        {
            ok,
            lettera = letter.ToString(),
            trovate = count,
            rimaste = board.LettersRemaining,
            stato = board.Status.ToString()
        });
    });

    // GET|POST /api/risolvi — scopre l'intera frase.
    api.MapMethods("/risolvi", new[] { "GET", "POST" }, (BoardService board) =>
    {
        board.Solve();
        return Results.Json(new { ok = true, stato = board.Status.ToString() });
    });

    // GET /api/stato — fotografia della partita, per pannelli e display esterni.
    // La frase in chiaro non viene mai restituita: la sa solo la regia.
    api.MapGet("/stato", (BoardService board) => Results.Json(new
    {
        stato = board.Status.ToString(),
        categoria = board.IsActive ? board.Category : null,
        frase = board.IsActive ? $"{board.PuzzleNumber}/{board.PuzzlesTotal}" : null,
        scoperte = board.LettersRevealed,
        rimaste = board.LettersRemaining,
        chiamate = board.CalledLetters.Select(c => new { lettera = c.Letter.ToString(), trovate = c.Count }),
        turno = board.Players.Count > 0 ? board.Players[board.ActivePlayer].Name : null,
        punteggi = board.Players.Select(p => new { nome = p.Name, punti = p.Score, attivo = p.Enabled })
    }));
}
