namespace RuotaDellaFortuna.Models;

/// <summary>Le lettere chiamabili, divise come sulla tastiera di regia.</summary>
public static class Alphabet
{
    /// <summary>Le 21 consonanti dell'alfabeto usato in gioco.</summary>
    public const string Consonants = "BCDFGHJKLMNPQRSTVWXYZ";

    /// <summary>Le 5 vocali.</summary>
    public const string Vowels = "AEIOU";

    /// <summary>Tutte le lettere, consonanti prima e vocali dopo, come in regia.</summary>
    public const string All = Consonants + Vowels;
}
