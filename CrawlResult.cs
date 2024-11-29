namespace SimpleCrawler;

public class CrawlResult
{
    public required Uri ResourceUri { get; set; }
    public required byte[] ResourceData { get; set; }
    public string? MimeType { get; set; }
}
