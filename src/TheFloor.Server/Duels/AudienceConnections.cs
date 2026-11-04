using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TheFloor.Content;

namespace TheFloor.Duels;

public sealed record AudienceDisplay(Guid Id, string Name);
public sealed record AudienceStatus(AudienceDisplay[] Displays, Guid? SelectedAudioDisplayId, Guid? AudioDisplayId);

public sealed class AudienceConnections
{
    private readonly object gate = new();
    private readonly Dictionary<string, Guid> connections = new();
    private Guid? selected;

    public void Connect(string connectionId, Guid displayId)
    {
        lock (gate) connections.Add(connectionId, displayId);
    }

    public void Disconnect(string connectionId)
    {
        lock (gate) connections.Remove(connectionId);
    }

    public void SelectAudio(Guid? displayId)
    {
        lock (gate)
        {
            if (displayId.HasValue && !connections.ContainsValue(displayId.Value))
                throw new ContentConflict("That audience display is no longer connected.");
            selected = displayId;
        }
    }

    public AudienceStatus Read()
    {
        lock (gate)
        {
            var displays = connections.Values.Distinct()
                .Select(id => new AudienceDisplay(id, $"Display {id:N}"[..14].ToUpperInvariant()))
                .ToArray();
            return new AudienceStatus(displays, selected, selected ?? displays.FirstOrDefault()?.Id);
        }
    }
}

public static class AudiencePairing
{
    public static string[] Origins(IServer server)
    {
        var origins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [])
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.IsLoopback) continue;
            if (uri.Host is "0.0.0.0" or "[::]")
            {
                foreach (var ip in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                    .Select(item => item.Address)
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)))
                {
                    origins.Add(new UriBuilder(uri) { Host = ip.ToString() }.Uri.GetLeftPart(UriPartial.Authority));
                }
            }
            else origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        return origins.Order(StringComparer.Ordinal).ToArray();
    }
}
