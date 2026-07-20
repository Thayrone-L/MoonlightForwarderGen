using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MFG.App.Services;

public sealed class LocalNetworkAddressProvider
{
    public string GetStreamingAddress()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                              network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));
        return address?.ToString()
            ?? throw new InvalidOperationException("Não encontramos o endereço de rede deste computador. Conecte-se à mesma rede Wi-Fi do Switch e tente novamente.");
    }
}
