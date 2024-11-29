using CommandLine;

namespace SimpleCrawler;

class Program
{
    private static List<Task> _flushTasks = [];
    private static bool _verbose;
    private static string _output = null!;

    public static async Task Main(string[] args)
    {
        var result = Parser.Default.ParseArguments<Options>(args);
        foreach (var err in result.Errors)
        {
            if (err is null)
                continue;
            Console.WriteLine(err.Tag);
        }
        if (result.Errors.Any())
        {
            Console.WriteLine(
                "Error parsing arguments. Must resolve all errors before proceeding."
            );
            return;
        }
        var options = result.Value;
        _verbose = options.Verbose;
        if (options.ThreadCount is not null)
        {
            ThreadPool.SetMinThreads((int)options.ThreadCount, (int)options.ThreadCount);
            ThreadPool.SetMaxThreads((int)options.ThreadCount, (int)options.ThreadCount);
        }
        if (options.OutputPath.EndsWith('/'))
            _output = options.OutputPath[0..^1];
        else
            _output = options.OutputPath;
        await App(options);
    }

    private static async Task App(Options options)
    {
        using var crawler = new Crawler(
            options.Verbose,
            options.Timeout,
            options.LogPath,
            options.FollowRedirect,
            options.UseDiskCache ? SearchDiskCache : null
        );
        crawler.OnResourceCrawled += UpdateProgressCrawled;
        crawler.OnResourceDiscovered += UpdateProgressDiscovered;
        if (options.WriteSimultaneously)
            crawler.OnResourceCrawled += Crawler_OnResourceCrawled_Write;
        var results = await crawler.CrawlAsync(
            options.EntryPoint,
            options.ForceDiscover,
            options.CrawlDepth
        );
        if (!options.WriteSimultaneously)
        {
            foreach (var result in results)
            {
                _flushTasks.Add(FlushResultToDisk(result));
            }
        }
        Console.WriteLine("Waiting results to be flushed to disk...");
        await Task.WhenAll(_flushTasks);
        Console.WriteLine($"Crawling finished with {crawler.ErrorTaskCount} errors.");
        if (options.UseDiskCache)
            Console.WriteLine($"{crawler.ExternallyResolvedCount} files were resolved with disk cache.");
    }

    private static void Crawler_OnResourceCrawled_Write(CrawlResult? crawlResult, Crawler _)
    {
        if (crawlResult is not null)
            _flushTasks.Add(FlushResultToDisk(crawlResult));
    }

    private static void UpdateProgressCrawled(CrawlResult? _, Crawler crawler)
    {
        UpdateProgress(_verbose, crawler);
    }

    private static void UpdateProgressDiscovered(Uri _, Crawler crawler)
    {
        UpdateProgress(_verbose, crawler);
    }

    private static async Task FlushResultToDisk(CrawlResult result)
    {
        var filePath = DetermineLocalFilePath(result.ResourceUri, result.MimeType);
        await File.WriteAllBytesAsync(filePath, result.ResourceData);
    }

    private static string DetermineLocalFilePath(Uri uri, string? mimeType)
    {
        var remoteAbsolutePath = uri.AbsolutePath;
        if (remoteAbsolutePath.EndsWith('/'))
            remoteAbsolutePath += "index";
        var baseDirPath = $"{_output}/{uri.Host}-{uri.Port}";
        var filePath = $"{baseDirPath}{remoteAbsolutePath}";
        var dir = Directory.GetParent(filePath);
        if (dir is null)
            throw new Exception("unknown error");
        if (!dir.Exists)
            dir.Create();
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext) && mimeType is not null)
        {
            filePath += $".{mimeType.Split('/').Last()}";
        }
        return filePath;
    }

    private static void UpdateProgress(bool verbose, Crawler crawler)
    {
        if (!verbose)
            Console.Clear();
        int maxBarWidth = Math.Min(60, Console.BufferWidth - 10);
        double progress = crawler.FinishedTaskCount * 1.0 / crawler.DiscoveredTaskCount;
        int barWidth = (int)(progress * maxBarWidth);
        Console.WriteLine($"  Finished: {crawler.FinishedTaskCount}");
        Console.WriteLine($"Discovered: {crawler.DiscoveredTaskCount}");
        Console.WriteLine(
            $"[{new('=', barWidth)}{new(' ', maxBarWidth - barWidth)}] {progress,7:P2}"
        );
    }

    private static ContentRequestResponse SearchDiskCache(Uri uri)
    {
        var filePath = DetermineLocalFilePath(uri, null);
        if (!File.Exists(filePath))
            return new(false);
        var ext = new FileInfo(filePath).Extension;
        var content = File.ReadAllText(filePath);
        if (string.IsNullOrEmpty(ext))
            return new(true, false, content);
        if (ext.StartsWith('.'))
            ext = ext[1..];
        if (string.IsNullOrEmpty(ext))
            return new(true, false, content);
        if (Crawler.KnownTextFileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return new(true, true, content);
        return new(true, false, content);
    }
}
