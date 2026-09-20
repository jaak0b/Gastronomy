using System.Net;
using System.Net.NetworkInformation;

namespace GastronomyApp.Api.Values;

public sealed record CandidateNetworkAddress(string InterfaceName, NetworkInterfaceType InterfaceType, OperationalStatus InterfaceStatus, IPAddress Address);
