using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RuotaDellaFortuna.Services;

/// <summary>
/// Trova gli indirizzi con cui questo PC è raggiungibile dagli altri dispositivi
/// della rete locale, così il menu può suggerirli invece di costringere l'utente
/// a cercarli con <c>ipconfig</c>.
/// </summary>
public static class NetworkInfo
{
    /// <summary>
    /// Gli indirizzi IPv4 delle schede di rete attive, esclusi loopback e schede
    /// virtuali. In genere è uno solo: quello del Wi-Fi o del cavo.
    /// </summary>
    public static IReadOnlyList<string> GetLanAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsUsableInterface)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(address => address.Address)
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString())
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            // Se il sistema non espone le schede di rete si rinuncia al suggerimento:
            // non è un errore che debba fermare l'applicazione.
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Scarta le schede spente, il loopback e le schede virtuali di tunnel: quelle
    /// non servono a raggiungere un PC nella stessa stanza.
    /// </summary>
    private static bool IsUsableInterface(NetworkInterface nic) =>
        nic.OperationalStatus == OperationalStatus.Up &&
        nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel);
}
