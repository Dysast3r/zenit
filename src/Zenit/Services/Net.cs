using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace Zenit.Services;

/// <summary>Cliente HTTP compartido: solo HTTPS, tiempo de espera corto y sin cookies ni credenciales.</summary>
public static class Net
{
    /// <summary>Las respuestas JSON más grandes se descartan (protege de APIs de widgets mal configuradas).</summary>
    private const long MaxJsonBytes = 4 * 1024 * 1024;

    /// <summary>Límite de <see cref="GetBytesAsync"/> (archivos pequeños como SHA256SUMS.txt).</summary>
    private const long MaxBytes = 1024 * 1024;

    /// <summary>Tiempo máximo de una petición completa, cuerpo incluido.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);

    private static readonly HttpClient Client = CreateClient(untrusted: false);

    /// <summary>Para URLs que vienen de archivos del usuario (widgets): no puede llegar a la red local.</summary>
    private static readonly HttpClient UntrustedClient = CreateClient(untrusted: true);

    private static HttpClient CreateClient(bool untrusted)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        if (untrusted)
        {
            // Con un proxy se validaría el proxy y no el destino real: se conecta directo.
            handler.UseProxy = false;
            handler.ConnectCallback = ConnectPublicAsync;
        }
        var client = new HttpClient(handler) { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Zenit/{AppInfo.Version.Split('+')[0]} (+{AppInfo.RepositoryUrl})");
        return client;
    }

    /// <summary>Resuelve el host y solo conecta a direcciones públicas (también en cada redirección).</summary>
    private static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken cancellation)
    {
        var addresses = (await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellation)).Where(IsPublic).ToArray();
        if (addresses.Length == 0) throw new HttpRequestException("Destino no permitido: " + context.DnsEndPoint.Host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellation);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Descarta loopback, redes privadas, enlace local, CGNAT, ULA y direcciones sin especificar.</summary>
    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(b[0] is 0 or 10 or 127 or >= 224
                || (b[0] == 172 && (b[1] & 0xF0) == 16)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && (b[1] & 0xC0) == 64));
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(b.All(x => x == 0)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC);
        }
        return false;
    }

    private static void RequireHttps(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Solo HTTPS", nameof(url));
    }

    /// <summary>
    /// JSON de una API, o null si falla. <paramref name="untrusted"/> se usa para URLs del usuario (widgets):
    /// no permite direcciones locales ni privadas.
    /// </summary>
    public static async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellation = default, bool untrusted = false)
    {
        RequireHttps(url);
        try
        {
            // HttpClient.Timeout solo cubre las cabeceras con ResponseHeadersRead: el límite cubre también el cuerpo.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            cts.CancelAfter(RequestTimeout);
            using var response = await (untrusted ? UntrustedClient : Client).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode) return null;
            var body = await ReadLimitedAsync(response.Content, MaxJsonBytes, cts.Token);
            return body is null ? null : JsonDocument.Parse(body);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Texto de una URL (p. ej. un calendario iCal), o null si falla o supera <paramref name="maxBytes"/>.</summary>
    public static async Task<string?> GetTextAsync(string url, long maxBytes, CancellationToken cancellation = default, bool untrusted = false)
    {
        RequireHttps(url);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            cts.CancelAfter(RequestTimeout);
            using var response = await (untrusted ? UntrustedClient : Client).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode) return null;
            var body = await ReadLimitedAsync(response.Content, maxBytes, cts.Token);
            return body is null ? null : System.Text.Encoding.UTF8.GetString(body);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<byte[]> GetBytesAsync(string url)
    {
        RequireHttps(url);
        using var cts = new CancellationTokenSource(RequestTimeout);
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        return await ReadLimitedAsync(response.Content, MaxBytes, cts.Token)
            ?? throw new HttpRequestException("Respuesta demasiado grande");
    }

    /// <summary>Lee el cuerpo ya descomprimido; null si supera <paramref name="max"/> (Content-Length puede faltar o mentir).</summary>
    private static async Task<byte[]?> ReadLimitedAsync(HttpContent content, long max, CancellationToken cancellation)
    {
        if (content.Headers.ContentLength > max) return null;
        await using var stream = await content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellation)) > 0)
        {
            if (buffer.Length + count > max) return null;
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }

    /// <summary>Descarga un archivo grande con progreso 0..1 (sin el tiempo de espera corto de las APIs).</summary>
    public static async Task DownloadAsync(string url, string path, IProgress<double>? progress)
    {
        RequireHttps(url);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;

        await using var input = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var output = File.Create(path);
        var buffer = new byte[81920];
        long read = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cts.Token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count), cts.Token);
            read += count;
            if (total > 0) progress?.Report((double)read / total);
        }
    }

    public static double? Number(this JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    public static string? Text(this JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
