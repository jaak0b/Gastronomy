using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GastronomyApp.Api.Options;

namespace GastronomyApp.Api.Hosting;

public sealed class LocalNetworkAddressProvider
{
    public IReadOnlyList<string> FindReachableAddresses()
    {
        List<string> addresses = [];

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(unicast.Address))
                {
                    addresses.Add(unicast.Address.ToString());
                }
            }
        }

        return addresses;
    }
}

public sealed class ReachableHostResolver
{
    private const string LoopbackHost = "127.0.0.1";

    private readonly ApiHostOptions hostOptions;
    private readonly LocalNetworkAddressProvider addressProvider;

    public ReachableHostResolver(ApiHostOptions hostOptions, LocalNetworkAddressProvider addressProvider)
    {
        this.hostOptions = hostOptions;
        this.addressProvider = addressProvider;
    }

    public bool BindsEveryAddress()
    {
        string bindAddress = hostOptions.BindAddress;

        return string.IsNullOrWhiteSpace(bindAddress)
            || bindAddress is "0.0.0.0" or "::" or "*" or "+";
    }

    public string ResolveHost()
    {
        if (!BindsEveryAddress())
        {
            return hostOptions.BindAddress;
        }

        IReadOnlyList<string> addresses = addressProvider.FindReachableAddresses();

        return addresses.Count == 0 ? LoopbackHost : addresses[0];
    }

    public IReadOnlyList<string> ReachableAddresses()
    {
        return addressProvider.FindReachableAddresses();
    }
}
