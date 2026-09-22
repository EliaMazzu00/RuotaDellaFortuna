// Scorciatoie da tastiera del pannello di regia.
//
// L'ascolto è a livello di finestra: chi conduce non deve prima cliccare sulla
// pagina per far funzionare i tasti. I tasti premuti mentre si scrive in un
// campo vengono ignorati, altrimenti rinominare un concorrente chiamerebbe
// lettere a raffica.

/** Riferimento al componente Admin, per richiamarne il metodo OnShortcut. */
let component = null;

/** Il gestore in ascolto, tenuto da parte per poterlo rimuovere. */
let handler = null;

/** Elementi in cui l'utente sta scrivendo: lì i tasti sono suoi, non nostri. */
const TEXT_INPUT_TYPES = ["text", "number", "search", "email", "url", "tel", "password"];

/** Tasti non alfabetici che comandano qualcosa. */
const COMMAND_KEYS = ["Enter", "ArrowRight", "+", "-", "−", "1", "2", "3", "4"];

/**
 * Registra le scorciatoie.
 * @param {object} dotNetComponent riferimento al componente Blazor.
 */
export function registerShortcuts(dotNetComponent) {
    unregisterShortcuts();

    component = dotNetComponent;
    handler = (event) => onKeyDown(event);

    window.addEventListener("keydown", handler);
}

/** Smette di ascoltare la tastiera. */
export function unregisterShortcuts() {
    if (handler) {
        window.removeEventListener("keydown", handler);
        handler = null;
    }

    component = null;
}

function onKeyDown(event) {
    if (!component || event.altKey || event.metaKey || event.repeat) {
        return;
    }

    if (isTyping(event.target)) {
        return;
    }

    const key = event.key;
    const isLetter = key.length === 1 && /\p{L}/u.test(key);
    const isCommand = COMMAND_KEYS.includes(key);

    if (!isLetter && !isCommand) {
        return;
    }

    // Ctrl serve solo per Ctrl+Invio: sulle lettere lascia passare le scorciatoie
    // del browser (Ctrl+R per ricaricare, e così via).
    if (event.ctrlKey && key !== "Enter") {
        return;
    }

    // Invio e barra spaziatrice attiverebbero il bottone che ha il fuoco:
    // la scorciatoia deve valere al posto di quello.
    event.preventDefault();

    component.invokeMethodAsync("OnShortcut", key, event.ctrlKey);
}

/** Vero se il fuoco è su un campo di testo o su un'area modificabile. */
function isTyping(target) {
    if (!target) {
        return false;
    }

    const tag = target.tagName;

    if (tag === "TEXTAREA" || tag === "SELECT" || target.isContentEditable) {
        return true;
    }

    return tag === "INPUT" && TEXT_INPUT_TYPES.includes((target.type || "text").toLowerCase());
}
