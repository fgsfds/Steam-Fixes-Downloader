using System.Text.Json.Serialization;

namespace Common.Entities;

public sealed class NewsEntity
{
    /// <summary>
    /// Date of the news article
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// News article text
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Is newer than the last read version
    /// </summary>
    [JsonIgnore]
    public bool IsNewer { get; set; }

    [JsonIgnore]
    public string DateFormatted => Date.ToString("dd.MM.yy");

    [JsonIgnore]
    public string ContentHtml => Markdig.Markdown.ToHtml(Content);
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<NewsEntity>))]
public sealed partial class NewsListEntityContext : JsonSerializerContext;

