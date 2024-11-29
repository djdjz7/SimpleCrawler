using System.Data;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace SimpleCrawler;

public class Crawler : IDisposable
{
    private bool _verbose;
    private bool _followRedirect;
    private int _timeout;
    private StreamWriter _logWriter;
    public event Action<CrawlResult?, Crawler>? OnResourceCrawled;
    public event Action<Uri, Crawler>? OnResourceDiscovered;
    private Func<Uri, ContentRequestResponse>? _externalResourceRequest;

    public Crawler(
        bool verbose,
        int timeout,
        string logPath,
        bool? followRedirect = true,
        Func<Uri, ContentRequestResponse>? resourceRequested = null
    )
    {
        _verbose = verbose;
        _followRedirect = followRedirect ?? true;
        _timeout = timeout;
        _logWriter = new(logPath, true);
        _externalResourceRequest = resourceRequested;
    }

    public static string[] KnownTextFileExtensions { get; set; } =
        ["txt", "html", "css", "js", "php", "aspx", "asp", "htm", "xml", "jsp"];
    public Dictionary<string, HttpClient> _clients = [];
    public Dictionary<Uri, bool> _tasks = new();
    public int DiscoveredTaskCount { get; private set; }
    public int FinishedTaskCount { get; private set; }
    public int ErrorTaskCount { get; private set; }
    public int ExternallyResolvedCount { get; private set; }
    private bool _isCrawling = false;
    private static readonly Regex _linkRegex = new(
        @"(href *=|HREF *=|src *=|SRC *=|url *\(|URL *\() *\(? *[""'](.*?)[""']"
    );

    public async Task<IEnumerable<CrawlResult>> CrawlAsync(
        string entryPoint,
        bool forceDiscover,
        uint depth = 1
    )
    {
        if (_isCrawling)
            throw new NotSupportedException(
                "Parallel crawling with single instance is not supported."
            );
        ArgumentOutOfRangeException.ThrowIfZero(depth, nameof(depth));
        _isCrawling = true;
        var result = await InternalCrawlAsync(new Uri(entryPoint), depth, forceDiscover);
        _isCrawling = false;
        return result;
    }

    private async Task<IEnumerable<CrawlResult>> InternalCrawlAsync(
        Uri entryPoint,
        uint depth,
        bool forceDiscover
    )
    {
        lock (_tasks)
        {
            if (_tasks.ContainsKey(entryPoint))
                return [];
            _tasks.Add(entryPoint, false);
        }
        DiscoveredTaskCount++;
        Output($"[DSCVR] Now on {entryPoint}");
        OnResourceDiscovered?.Invoke(entryPoint, this);
        if (_externalResourceRequest is not null)
        {
            var requestResponse = _externalResourceRequest(entryPoint);
            if (requestResponse.Skip)
            {
                FinishedTaskCount++;
                ExternallyResolvedCount++;
                Output($"[CACHE] Using cached data of {entryPoint}");
                OnResourceCrawled?.Invoke(null, this);
                if (depth == 1)
                    return [];
                var content = requestResponse.Content;
                if (content is null)
                    return [];
                if (!forceDiscover && requestResponse.IsTextFile != true)
                    return [];
                var tasks = DiscoverTasks(content, entryPoint, depth, forceDiscover);
                return (await Task.WhenAll(tasks)).SelectMany(x => x);
            }
        }

        HttpClient? hostClient;
        lock (_clients)
        {
            if (!_clients.TryGetValue(entryPoint.Host, out hostClient))
            {
                if (_followRedirect)
                    hostClient = new(new HttpClientHandler() { AllowAutoRedirect = true })
                    {
                        Timeout = TimeSpan.FromSeconds(_timeout),
                    };
                else
                    hostClient = new(new HttpClientHandler() { AllowAutoRedirect = false })
                    {
                        Timeout = TimeSpan.FromSeconds(_timeout),
                    };
                _clients.Add(entryPoint.Host, hostClient);
            }
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await hostClient.GetAsync(entryPoint);
            response.EnsureSuccessStatusCode();
            Output($"[DONE.] Successfully fetched {entryPoint}");
            var data = await response.Content.ReadAsByteArrayAsync();
            var thisResult = new CrawlResult()
            {
                ResourceUri = entryPoint,
                ResourceData = data,
                MimeType = response.Content.Headers.ContentType?.MediaType,
            };
            FinishedTaskCount++;
            OnResourceCrawled?.Invoke(thisResult, this);

            if (depth == 1)
            {
                return [thisResult];
            }
            if (!forceDiscover && !IsTextFile(response, entryPoint))
            {
                return [thisResult];
            }
            var text = await response.Content.ReadAsStringAsync();
            var tasks = DiscoverTasks(text, entryPoint, depth, forceDiscover);
            var result = await Task.WhenAll(tasks);
            return [thisResult, .. result.SelectMany(x => x)];
        }
        catch (HttpRequestException ex)
            when (response is not null
                && _followRedirect
                && (ex.StatusCode == HttpStatusCode.Moved || ex.StatusCode == HttpStatusCode.Found)
            )
        {
            var location = response.Headers.Location;
            if (location is null)
            {
                throw new Exception(
                    $"Code {(int?)ex.StatusCode} at {entryPoint} tried to redirect but no location was found."
                );
            }
            Output($"[REDIR] Code {(int?)ex.StatusCode} at {entryPoint} redirected to {location}");
            FinishedTaskCount++;
            OnResourceCrawled?.Invoke(null, this);
            return await InternalCrawlAsync(location, depth, forceDiscover);
        }
        catch (Exception ex)
        {
            Output(
                $"[ERROR] Failed to crawl {entryPoint},{Environment.NewLine}        Exception: {ex.Message}{(ex.InnerException is null ? "" : $"{Environment.NewLine}        Inner: {ex.InnerException.Message}")}"
            );
        }
        ErrorTaskCount++;
        FinishedTaskCount++;
        OnResourceCrawled?.Invoke(null, this);
        return [];
    }

    private static bool IsTextFile(HttpResponseMessage response, Uri requestUri)
    {
        var media = response.Content.Headers.ContentType?.MediaType;
        if (media?.Contains("text", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        var absolute = requestUri.AbsolutePath;
        var ext = absolute.Split('/').Last().Split('.').Last();
        foreach (var knownExt in KnownTextFileExtensions)
        {
            if (ext.Equals(knownExt, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private IEnumerable<Task<IEnumerable<CrawlResult>>> DiscoverTasks(
        string content,
        Uri currentUri,
        uint currentDepth,
        bool forceDiscover
    )
    {
        var matches = _linkRegex.Matches(content);
        return matches.Select(x =>
        {
            var newUriString = x.Groups[2].Value;
            var newUri = new Uri(currentUri, newUriString);
            return InternalCrawlAsync(newUri, currentDepth - 1, forceDiscover);
        });
    }

    private void Output(string content)
    {
        if (_verbose)
            Console.WriteLine(content);
        _logWriter.WriteLine(content);
    }

    public void Dispose()
    {
        _logWriter.Flush();
        _logWriter.Dispose();
    }
}
