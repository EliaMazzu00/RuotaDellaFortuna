# 🎡 La Ruota della Fortuna

Il **tabellone delle frasi misteriose** della *Ruota della Fortuna*, da giocare in casa
con più schermi: un PC conduce, la TV mostra il tabellone, i telefoni chiamano le lettere.
Tutto si aggiorna in tempo reale, senza installare niente sugli altri dispositivi.

Scritto in **Blazor Server (.NET 10)**. Nessun database, nessun account, nessuna
connessione a Internet necessaria: basta una rete locale.

---

## Indice

- [Come si avvia](#come-si-avvia)
- [Le quattro schermate](#le-quattro-schermate)
- [Giocare su più schermi](#giocare-su-più-schermi)
- [Come si conduce una partita](#come-si-conduce-una-partita)
- [Scorciatoie da tastiera](#scorciatoie-da-tastiera)
- [Il tabellone](#il-tabellone)
- [Le frasi](#le-frasi)
- [Concorrenti, turni e punti](#concorrenti-turni-e-punti)
- [Chiamare le lettere da fuori](#chiamare-le-lettere-da-fuori)
- [Configurazione](#configurazione)
- [Com'è fatto il progetto](#comè-fatto-il-progetto)
- [Nota sulla sicurezza](#nota-sulla-sicurezza)

---

## Come si avvia

### Con l'eseguibile pronto (consigliato per giocare)

Scarica lo `.zip` dalla pagina [Releases](../../releases), estrailo dove vuoi e fai
doppio clic su **`RuotaDellaFortuna.exe`**. Non serve installare .NET.

Poi apri il browser su **<http://localhost:5090>**.

### Dai sorgenti (per chi sviluppa)

Serve l'[SDK .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

> ⚠️ Alla prima esecuzione Windows chiede di **autorizzare l'app nel firewall**:
> consenti l'accesso alle **reti private**, altrimenti gli altri dispositivi non
> riescono a collegarsi.

---

## Le quattro schermate

| Schermata | Indirizzo | A cosa serve | Dove aprirla |
|---|---|---|---|
| **Menu** | `/` | I link alle altre schermate e gli indirizzi di rete già pronti | dove capita |
| **Regia** | `/admin` | Prepara la partita, chiama le lettere, tiene i punti | sul PC che conduce |
| **Tabellone** | `/display` | Il tabellone grande, senza nessuna informazione riservata | TV, proiettore, secondo monitor |
| **Tastiera** | `/remote` | Solo i tasti delle lettere, grandi | telefono o tablet |

La regia vede **tutto**, frase in chiaro compresa. Il tabellone mostra **solo** le celle
che si girano: nessun messaggio tradisce se una lettera c'era o no.

---

## Giocare su più schermi

1. Avvia l'app sul PC che conduce e apri **`/admin`**.
2. Il **menu** (`/`) mostra già gli indirizzi da usare sugli altri dispositivi,
   tipo `http://192.168.1.50:5090/display`: non serve cercarli con `ipconfig`.
3. Sulla TV (o sul secondo monitor) apri quell'indirizzo e metti il browser a
   schermo intero con **F11**.
4. Sui telefoni, stesso indirizzo con `/remote` al posto di `/display`.

Tutti gli schermi restano sincronizzati. Se un dispositivo perde la rete per un
momento, si ricollega da solo: **la partita vive nel server**, non nel browser.

---

## Come si conduce una partita

1. **Scegli i temi** nel pannello di sinistra (o carica un file, o incolla le frasi).
2. **Applica i settaggi**: cosa mostrare sul tabellone, forma del tabellone,
   lettere regalate, ordine delle frasi.
3. **▶ Manda la prima frase** → sul tabellone compaiono la categoria e le celle coperte.
4. **Chiama le lettere** dalla tastiera a video, dal telefono, o premendo i tasti:
   - sul tabellone le celle si girano con un lampo giallo;
   - l'esito ("presente 3 volte" / "non c'è") lo vede **solo la regia**;
   - se la lettera c'è il turno **resta**, se non c'è **passa** al prossimo concorrente.
5. **Assegna i punti** con i bottoni `+1` / `−1`, o scrivendo la cifra che volete.
6. Quando un concorrente **risolve**: premi **💡 Rivela tutto** → sul tabellone appare
   *RISOLTO!* e lo sfondo diventa verde.
7. **⏭ Frase successiva** per continuare, **↻ Ricopri** per rigiocarla,
   **↺ Reset** per ripartire da zero.

Vuoi dare un aiuto piccolo invece di svelare tutto? Nell'anteprima di regia
**clicca una singola cella coperta**: si scopre solo quella.

---

## Scorciatoie da tastiera

Sul pannello di regia, per non dover cercare i tasti col mouse:

| Tasto | Cosa fa |
|---|---|
| `A` – `Z` | chiama la lettera |
| `Invio` | manda la prima frase, poi passa alla successiva |
| `Ctrl` + `Invio` | rivela tutta la frase |
| `→` | passa il turno |
| `1` – `4` | dai il turno a quel concorrente |
| `+` / `−` | un punto in più o in meno al concorrente di turno |

Non valgono mentre stai scrivendo in un campo di testo, così rinominare un
concorrente non chiama lettere per sbaglio.

---

## Il tabellone

- **Layout classico TV**: quattro righe da **12 · 14 · 14 · 12** celle, con la frase
  centrata da sola nel modo più equilibrato.
- **Layout personalizzato**: decidi tu le larghezze, da 1 a 8 righe da 1 a 30 celle
  ciascuna. Si scrivono separate da spazi: `10 12 12 10`, oppure `26 14 14`.
- Le **celle bianche** nascondono le lettere, le **verdi** sono vuote.
- **Apostrofi, punteggiatura e cifre** sono sempre visibili: non si chiamano.
- Le **lettere accentate** si scoprono chiamando la lettera base: con `A` esce anche `À`.
- Le celle si **ridimensionano da sole** in base a righe e colonne del tabellone e alla
  dimensione dello schermo, quindi il tabellone non esce mai dai bordi.
- Una frase **troppo lunga non viene scartata né tagliata**: va su una **griglia estesa**
  con le righe che servono e celle più grandi. La regia ti dice in anticipo quante frasi
  entrano nel tabellone "bello" e quante no, con l'elenco preciso.

---

## Le frasi

Nella cartella [`frasi/`](frasi) ci sono **20 temi da circa 100 frasi ciascuno**
(≈ 2000 frasi): proverbi, cinema, musica, geografia italiana, città del mondo, cibo,
animali, sport, storia, arte e letteratura, scienza, personaggi famosi, televisione,
fiabe e cartoni, mestieri, natura, casa e oggetti, moda, feste, modi di dire.

Si possono selezionare anche **più temi insieme**: le frasi vengono unite.

### Formato di un file di frasi

Un file `.txt`, una frase per riga:

```
# TEMA: Proverbi e detti

PROVERBIO | CHI DORME NON PIGLIA PESCI
PROVERBIO | MEGLIO TARDI CHE MAI
DETTO POPOLARE | AVERE LE MANI BUCATE
```

- la riga `# TEMA: Nome` dà il nome leggibile al tema;
- le righe vuote e quelle che iniziano con `#` sono ignorate;
- senza il separatore `|` la categoria diventa `FRASE MISTERIOSA`;
- maiuscole e minuscole non contano: il tabellone è sempre in maiuscolo;
- per il layout classico conviene tenere le parole entro **14 lettere** e le frasi
  entro una quarantina di caratteri.

Per aggiungere un tema basta mettere un nuovo `.txt` in `frasi/` e riavviare.
In alternativa, dalla regia puoi **caricare un file** dal tuo computer o
**incollare le frasi** a mano senza toccare nessun file.

---

## Concorrenti, turni e punti

- Da **1 a 4 concorrenti**, con il nome che vuoi.
- **Turno automatico**: lettera presente → il turno resta; lettera assente → passa al
  prossimo concorrente attivo. C'è anche il bottone **⏭ Passa turno**.
- **Fuori / Dentro**: un concorrente messo "fuori" resta sul tabellone in grigio ma
  viene saltato nei turni. Comodo quando qualcuno si allontana.
- **Punti**: `+1`, `−1`, `0` per azzerare, più un campo per assegnare la cifra che volete
  (dieci, cento, mille punti: le regole sono le vostre). Il punteggio non scende sotto zero.
- Chi è di turno è evidenziato in oro sia in regia sia sul tabellone.

---

## Chiamare le lettere da fuori

### Dal telefono

Apri `/remote`: quando c'è una frase in gioco compaiono i tasti delle lettere, grandi.
Le lettere già chiamate restano segnate — verdi se trovate (col numero di celle),
rosse e barrate se assenti.

### Via HTTP (pulsantiera, ESP32, script, tasto macro)

| Richiesta | Cosa fa |
|---|---|
| `GET`/`POST` `/api/lettera/{lettera}` | chiama una lettera |
| `GET`/`POST` `/api/risolvi` | rivela tutta la frase |
| `GET` `/api/stato` | fotografia della partita |

```bash
curl http://192.168.1.50:5090/api/lettera/E
# {"ok":true,"lettera":"E","trovate":3,"rimaste":18,"stato":"Playing"}

curl http://192.168.1.50:5090/api/risolvi
# {"ok":true,"stato":"Solved"}

curl http://192.168.1.50:5090/api/stato
# {"stato":"Playing","categoria":"PROVERBI E DETTI","frase":"2/101","scoperte":7,...}
```

`ok: false` significa che la chiamata non è stata accettata: partita non in corso,
oppure lettera già chiamata. Un carattere che non è una lettera riceve `400`.
`/api/stato` **non restituisce mai la frase**: quella la sa solo la regia.

---

## Configurazione

La porta si cambia in [`appsettings.json`](appsettings.json):

```json
{
  "Server": {
    "Port": 5090
  }
}
```

oppure con una variabile d'ambiente, senza toccare i file:

```powershell
$env:Server__Port = "8080"; .\RuotaDellaFortuna.exe
```

L'app resta in ascolto su **tutte** le schede di rete, così gli altri dispositivi
la raggiungono. La porta 5090 è scelta per non dare fastidio ad altri servizi.

---

## Com'è fatto il progetto

```
Models/                    tipi di dati, senza logica
  BoardCell, Player, Puzzle, CalledLetter, Alphabet, enum

Services/
  BoardLayoutEngine.cs     impagina le frasi sul tabellone (statico e puro)
  PuzzleLibrary.cs         legge e interpreta i file di frasi
  BoardService.cs          lo stato della partita, condiviso da tutte le pagine
  NetworkInfo.cs           trova gli indirizzi di rete da suggerire nel menu

Components/
  Pages/                   Home, Admin, Display, Remote (+ CSS e JS accanto)
  Layout/                  cornici delle pagine

wwwroot/app.css            colori, tipografia e spaziature in un posto solo
frasi/                     i 20 temi di frasi
```

Tre idee portanti:

1. **`BoardService` è un singleton** con lo stato della partita. Ogni pagina si iscrive
   al suo evento `OnChange` e si ridisegna: è tutto il "tempo reale" che serve, senza
   scrivere nemmeno un'API. Le scritture passano da un lock, perché le pagine aperte sono
   più di una e gli endpoint HTTP arrivano da altri thread.

2. **`BoardLayoutEngine` è puro**: niente stato, niente file, niente dipendenze. La logica
   delicata (dove andare a capo, come centrare, cosa fare con le frasi lunghissime) sta
   tutta lì e si può ragionare in isolamento.

3. **Niente framework CSS e niente font esterni.** La grafica è CSS scritto a mano, con i
   colori e le misure come variabili in `wwwroot/app.css` e un file *scoped* per pagina.
   Così l'app funziona identica anche senza Internet, che è il caso normale in una sala.

### Compilare e pubblicare

```bash
dotnet build                      # compilazione (deve restare a 0 warning)
dotnet run                        # avvia in locale
dotnet publish -c Release         # eseguibile autonomo per Windows x64
```

Il profilo di pubblicazione è in
[`Properties/PublishProfiles/FolderProfile.pubxml`](Properties/PublishProfiles/FolderProfile.pubxml)
e produce una cartella che gira **senza .NET installato**.

---

## Nota sulla sicurezza

L'app **non ha autenticazione**: chiunque sia sulla stessa rete può aprire `/admin` e
usare gli endpoint `/api/*`. È voluto — un microcontrollore non può gestire un login, e
in casa non serve — ma vuol dire che:

- va usata su una **rete locale di cui hai il controllo**;
- **non va esposta su Internet** né aperta sul router.

Nel repository non ci sono password, chiavi o token: i file di configurazione contengono
soltanto il livello dei log e la porta.

---

## Licenza

Progetto personale, per giocare in famiglia. Il format televisivo e il nome appartengono
ai rispettivi proprietari; questo è un tabellone fatto in casa, senza alcun legame con la
trasmissione.
