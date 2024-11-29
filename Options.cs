using CommandLine;

namespace SimpleCrawler;

class Options
{
    [Value(
        1,
        MetaName = "EntryPoint",
        Required = true,
        HelpText = "Where the crawler starts crawling."
    )]
    public required string EntryPoint { get; set; }

    [Option(
        'm',
        "thread",
        Required = false,
        Default = null,
        HelpText = "Thread count in thread pool. Avoid setting this property as .NET manages the thread pool automatically and may cause unexpected results."
    )]
    public int? ThreadCount { get; set; }

    [Option(
        'd',
        "crawl-depth",
        Default = 2u,
        Required = false,
        HelpText = "Depth of recursion of the crawler. Be careful setting the value as it may cause heavy network load."
    )]
    public uint CrawlDepth { get; set; }

    [Option(
        'w',
        "write-simul",
        Default = false,
        Required = false,
        HelpText = "Flush result simultaneously to the disk, instead of writing after crawling has finished."
    )]
    public bool WriteSimultaneously { get; set; }

    [Option(
        'f',
        "force-discover",
        Default = false,
        Required = false,
        HelpText = "Take all discovered files as plain text, search for links in all of the files. Useful when the crawler accidentally treat index files as assets."
    )]
    public bool ForceDiscover { get; set; }

    [Option('v', "verbose", Default = false, Required = false, HelpText = "Verbose mode.")]
    public bool Verbose { get; set; }

    [Option(
        'r',
        "follow-redirect",
        Required = false,
        Default = true,
        HelpText = "Automatically follow redirect (301, 302)."
    )]
    public bool? FollowRedirect { get; set; }

    [Option(
        't',
        "timeout",
        Required = false,
        Default = 10,
        HelpText = "Timeout for internal HttpClient (in seconds)."
    )]
    public int Timeout { get; set; }

    [Option(
        'o',
        "output-path",
        Required = false,
        Default = "crawl-result",
        HelpText = "Base output directory to write result to."
    )]
    public required string OutputPath { get; set; }

    [Option(
        'l',
        "log",
        Required = false,
        Default = "crawler.log",
        HelpText = "Where to write log."
    )]
    public required string LogPath { get; set; }

    [Option(
        'c',
        "disk-cache",
        Required = false,
        Default = false,
        HelpText = "Whether to skip cached resources."
    )]
    public bool UseDiskCache { get; set; }
}
