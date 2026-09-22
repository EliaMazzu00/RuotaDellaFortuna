namespace RuotaDellaFortuna.Models;

/// <summary>Una lettera chiamata durante la frase e quante celle ha scoperto.</summary>
/// <param name="Letter">La lettera chiamata, sempre maiuscola.</param>
/// <param name="Count">Quante celle ha rivelato: <c>0</c> significa che non c'era.</param>
public readonly record struct CalledLetter(char Letter, int Count)
{
    /// <summary>Vero se la lettera ha scoperto almeno una cella.</summary>
    public bool Found => Count > 0;

    /// <summary>Vero se e' una vocale (utile per colorarla in modo diverso).</summary>
    public bool IsVowel => Alphabet.Vowels.Contains(Letter);
}
