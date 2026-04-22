using SqlSugar;

namespace UrlShortener.Core.Entities;

// An "entity" is the C# class that maps 1:1 to a DB row.
// SqlSugar uses these attributes to figure out the SQL schema.
// Equivalent in your frontend world: a TypeScript type that mirrors a DB record.
[SugarTable("ShortLinks")]
public sealed class ShortLink
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string Slug { get; set; } = default!;

    [SugarColumn(Length = 2048, IsNullable = false)]
    public string TargetUrl { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public int ClickCount { get; set; }
}
