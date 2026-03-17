using System.Buffers;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Cross-platform HTTP server for hosting and sharing ROM files on the local network.
/// Supports two modes: sharing an entire directory or sharing a specific set of files.
/// </summary>
public sealed class RomHostingService : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private readonly object _lock = new();

    private string? _directoryPath;  // Always stored with trailing separator
    private IReadOnlyList<string>? _selectedFiles;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Raised when the server logs an event (connection, error, etc.).
    /// </summary>
    public event Action<string>? LogMessage;

    /// <summary>
    /// Start hosting all ROM files found in the given directory.
    /// </summary>
    public void StartDirectory(string directoryPath, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        lock (_lock)
        {
            if (IsRunning) throw new InvalidOperationException("Server is already running.");
            _directoryPath = NormalizeDirectoryPath(Path.GetFullPath(directoryPath));
            _selectedFiles = null;
            StartListener(port);
        }
    }

    /// <summary>
    /// Start hosting a specific set of ROM files.
    /// </summary>
    public void StartSelectedFiles(IReadOnlyList<string> filePaths, int port)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0) throw new ArgumentException("At least one file must be provided.", nameof(filePaths));

        lock (_lock)
        {
            if (IsRunning) throw new InvalidOperationException("Server is already running.");
            _selectedFiles = filePaths.Select(Path.GetFullPath).ToList();
            _directoryPath = null;
            StartListener(port);
        }
    }

    /// <summary>
    /// Stop the hosting server.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            try { _listener?.Stop(); } catch (ObjectDisposedException) { }

            try { _serverTask?.Wait(TimeSpan.FromSeconds(5)); }
            catch (Exception ex) when (ex is AggregateException or ObjectDisposedException) { }

            _listener?.Close();
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _serverTask = null;
            IsRunning = false;

            Log("Server stopped.");
        }
    }

    /// <summary>
    /// Returns all local (non-loopback) IPv4 addresses suitable for LAN sharing.
    /// </summary>
    public static List<string> GetLocalIPAddresses()
    {
        var addresses = new List<string>();

        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel) continue;

                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        addresses.Add(addr.Address.ToString());
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Fallback: unable to enumerate interfaces
        }

        if (addresses.Count == 0)
            addresses.Add("127.0.0.1");

        return addresses;
    }

    public void Dispose()
    {
        Stop();
    }

    private void StartListener(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            // Fallback: on some platforms, http://+:port/ may require elevated privileges.
            // Try binding to localhost only.
            _listener.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
        }

        IsRunning = true;
        Log($"Server started on port {port}.");

        var token = _cts.Token;
        _serverTask = Task.Run(() => AcceptLoopAsync(token), token);
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException ex)
            {
                Log($"Listener error: {ex.Message}");
                continue;
            }

            // Process each request on its own task to support concurrency
            _ = Task.Run(() => HandleRequestAsync(context, token), token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            string rawUrl = request.Url?.AbsolutePath ?? "/";
            string decodedPath = Uri.UnescapeDataString(rawUrl).TrimStart('/');

            Log($"{request.HttpMethod} {rawUrl} from {request.RemoteEndPoint}");

            if (request.HttpMethod is not "GET" and not "HEAD")
            {
                await SendErrorAsync(response, 405, "Method Not Allowed", token).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrEmpty(decodedPath) || decodedPath == "/")
            {
                await ServeListingAsync(request, response, token).ConfigureAwait(false);
            }
            else
            {
                await ServeFileAsync(request, response, decodedPath, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"Request error: {ex.Message}");
            try { await SendErrorAsync(response, 500, "Internal Server Error", token).ConfigureAwait(false); }
            catch { /* best effort */ }
        }
        finally
        {
            try { response.Close(); }
            catch { /* best effort */ }
        }
    }

    private async Task ServeListingAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken token)
    {
        var files = GetAvailableFiles();
        string html = BuildListingHtml(files);
        byte[] data = Encoding.UTF8.GetBytes(html);

        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = data.Length;
        response.AddHeader("X-Content-Type-Options", "nosniff");
        response.AddHeader("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'");

        if (request.HttpMethod != "HEAD")
            await response.OutputStream.WriteAsync(data, token).ConfigureAwait(false);
    }

    private async Task ServeFileAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        string requestedName,
        CancellationToken token)
    {
        string? filePath = ResolveFilePath(requestedName);

        if (filePath == null || !File.Exists(filePath))
        {
            await SendErrorAsync(response, 404, "File Not Found", token).ConfigureAwait(false);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        long fileLength = fileInfo.Length;
        string contentType = GetContentType(filePath);

        response.ContentType = contentType;
        response.AddHeader("Accept-Ranges", "bytes");
        response.AddHeader("Content-Disposition",
            $"attachment; filename=\"{SanitizeFileName(Path.GetFileName(filePath))}\"");

        // Handle range requests for resumable downloads
        string? rangeHeader = request.Headers["Range"];
        if (rangeHeader != null && TryParseRange(rangeHeader, fileLength, out long rangeStart, out long rangeEnd))
        {
            long rangeLength = rangeEnd - rangeStart + 1;
            response.StatusCode = 206;
            response.ContentLength64 = rangeLength;
            response.AddHeader("Content-Range", $"bytes {rangeStart}-{rangeEnd}/{fileLength}");

            if (request.HttpMethod != "HEAD")
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                fs.Seek(rangeStart, SeekOrigin.Begin);
                await CopyStreamAsync(fs, response.OutputStream, rangeLength, token).ConfigureAwait(false);
            }
        }
        else
        {
            response.StatusCode = 200;
            response.ContentLength64 = fileLength;

            if (request.HttpMethod != "HEAD")
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                await CopyStreamAsync(fs, response.OutputStream, fileLength, token).ConfigureAwait(false);
            }
        }
    }

    private List<FileEntry> GetAvailableFiles()
    {
        var entries = new List<FileEntry>();

        if (_selectedFiles != null)
        {
            foreach (string path in _selectedFiles)
            {
                if (!File.Exists(path)) continue;
                var info = new FileInfo(path);
                entries.Add(new FileEntry(info.Name, info.Length));
            }
        }
        else if (_directoryPath != null && Directory.Exists(_directoryPath))
        {
            foreach (string path in Directory.EnumerateFiles(_directoryPath))
            {
                if (!IsRomFile(path)) continue;
                var info = new FileInfo(path);
                entries.Add(new FileEntry(info.Name, info.Length));
            }
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private string? ResolveFilePath(string requestedName)
    {
        // Prevent path traversal
        if (requestedName.Contains("..") || requestedName.Contains('\\'))
            return null;

        string fileName = Path.GetFileName(requestedName);
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        if (_selectedFiles != null)
        {
            return _selectedFiles.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
        }

        if (_directoryPath != null)
        {
            string candidate = Path.Combine(_directoryPath, fileName);
            string fullCandidate = Path.GetFullPath(candidate);

            // Verify the resolved path is still within the directory (prevent traversal)
            if (!fullCandidate.StartsWith(_directoryPath, StringComparison.OrdinalIgnoreCase))
                return null;

            return File.Exists(fullCandidate) && IsRomFile(fullCandidate) ? fullCandidate : null;
        }

        return null;
    }

    private string BuildListingHtml(List<FileEntry> files)
    {
        long totalSize = files.Sum(f => f.Size);
        string title = _directoryPath != null
            ? $"ROM Directory — {Path.GetFileName(_directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}"
            : $"Shared ROMs ({files.Count} files)";

        var sb = new StringBuilder(4096);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(WebUtility.HtmlEncode(title)).AppendLine("</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
        sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;");
        sb.AppendLine("  background:#1e1e2e;color:#cdd6f4;padding:24px;max-width:960px;margin:0 auto}");
        sb.AppendLine("h1{font-size:1.5rem;margin-bottom:8px}");
        sb.AppendLine(".info{color:#a6adc8;font-size:0.9rem;margin-bottom:20px}");
        sb.AppendLine("table{width:100%;border-collapse:collapse}");
        sb.AppendLine("th,td{text-align:left;padding:10px 12px;border-bottom:1px solid #313244}");
        sb.AppendLine("th{background:#181825;color:#a6adc8;font-size:0.85rem;font-weight:600;text-transform:uppercase}");
        sb.AppendLine("tr:hover{background:#313244}");
        sb.AppendLine("a{color:#89b4fa;text-decoration:none}a:hover{text-decoration:underline}");
        sb.AppendLine(".size{color:#a6adc8;font-variant-numeric:tabular-nums;white-space:nowrap}");
        sb.AppendLine("@media(max-width:600px){th,td{padding:8px 6px;font-size:0.9rem}}");
        sb.AppendLine("</style></head><body>");
        sb.Append("<h1>").Append(WebUtility.HtmlEncode(title)).AppendLine("</h1>");
        sb.Append("<p class=\"info\">").Append(files.Count).Append(" file(s) — ")
          .Append(FileUtils.FormatFileSize(totalSize)).AppendLine(" total</p>");

        if (files.Count == 0)
        {
            sb.AppendLine("<p>No ROM files available.</p>");
        }
        else
        {
            sb.AppendLine("<table><thead><tr><th>File Name</th><th>Size</th></tr></thead><tbody>");
            foreach (var file in files)
            {
                string encodedName = Uri.EscapeDataString(file.Name);
                string htmlName = WebUtility.HtmlEncode(file.Name);
                sb.Append("<tr><td><a href=\"/").Append(encodedName).Append("\">")
                  .Append(htmlName).Append("</a></td><td class=\"size\">")
                  .Append(FileUtils.FormatFileSize(file.Size)).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<p class=\"info\" style=\"margin-top:24px\">Served by Retro Multi Tools</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static async Task CopyStreamAsync(
        Stream source, Stream destination, long length, CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            long remaining = length;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = await source.ReadAsync(buffer.AsMemory(0, toRead), token).ConfigureAwait(false);
                if (read == 0) break;
                await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task SendErrorAsync(HttpListenerResponse response, int statusCode, string message, CancellationToken token = default)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"<!DOCTYPE html><html><body><h1>{statusCode} {WebUtility.HtmlEncode(message)}</h1></body></html>");
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = data.Length;
        response.AddHeader("X-Content-Type-Options", "nosniff");
        await response.OutputStream.WriteAsync(data, token).ConfigureAwait(false);
    }

    private static bool TryParseRange(string rangeHeader, long fileLength, out long start, out long end)
    {
        start = 0;
        end = fileLength - 1;

        if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            return false;

        string rangeSpec = rangeHeader["bytes=".Length..];
        string[] parts = rangeSpec.Split('-', 2);
        if (parts.Length != 2) return false;

        bool hasStart = long.TryParse(parts[0].Trim(), out long parsedStart);
        bool hasEnd = long.TryParse(parts[1].Trim(), out long parsedEnd);

        if (hasStart && hasEnd)
        {
            start = parsedStart;
            end = Math.Min(parsedEnd, fileLength - 1);
        }
        else if (hasStart)
        {
            start = parsedStart;
            end = fileLength - 1;
        }
        else if (hasEnd)
        {
            // Suffix range: last N bytes
            start = Math.Max(0, fileLength - parsedEnd);
            end = fileLength - 1;
        }
        else
        {
            return false;
        }

        return start >= 0 && start <= end && start < fileLength;
    }

    private static string GetContentType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".zip" => "application/zip",
            ".7z" => "application/x-7z-compressed",
            ".gz" or ".tgz" => "application/gzip",
            ".iso" or ".cue" or ".bin" or ".chd" => "application/octet-stream",
            _ => "application/octet-stream"
        };
    }

    internal static bool IsRomFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".nes" or ".smc" or ".sfc" or ".z64" or ".n64" or ".v64"
            or ".gb" or ".gbc" or ".gba" or ".vb" or ".vboy"
            or ".sms" or ".md" or ".gen"
            or ".bin" or ".32x" or ".gg" or ".a26" or ".a52" or ".a78"
            or ".j64" or ".jag" or ".lnx" or ".lyx"
            or ".pce" or ".tg16" or ".iso" or ".cue" or ".3do" or ".chd"
            or ".rvz" or ".gcm"
            or ".ngp" or ".ngc" or ".col" or ".cv" or ".int"
            or ".mx1" or ".mx2" or ".dsk" or ".cdt" or ".sna"
            or ".tap" or ".mo5" or ".k7" or ".fd" or ".sv" or ".ccc"
            or ".nds" or ".3ds" or ".cia"
            or ".cdi" or ".gdi"
            or ".zip" or ".7z" or ".gz";
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove characters that are problematic in Content-Disposition headers:
        // quotes, backslashes, and control characters (prevents HTTP header injection).
        var sb = new StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            if (c == '"' || c == '\\')
                sb.Append('_');
            else if (char.IsControl(c))
                continue; // strip control characters entirely
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string NormalizeDirectoryPath(string path)
    {
        // Trim any existing trailing separators, then add the platform separator
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
             + Path.DirectorySeparatorChar;
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(message);
    }

    private readonly record struct FileEntry(string Name, long Size);
}
