namespace SimpleCrawler;

public class ContentRequestResponse
{
    public bool Skip { get; set; }
    public string? Content { get; set; }
    public bool? IsTextFile { get; set; }

    public ContentRequestResponse(bool skip, bool? isTextFile = null, string? content = null)
    {
        Skip = skip;
        Content = content;
        IsTextFile = isTextFile;
    }
}
