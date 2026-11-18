using System.Text.Json.Serialization;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Transfer;

public enum ListFileFormat { Json, Csv }

public sealed record ListFile(string FileName, byte[] Content, string ContentType);
public sealed record ImportedItem(ItemDraft Details, bool IsPurchased);
public sealed record ListImportPreview(string Name, IReadOnlyList<ImportedItem> Items);

internal sealed record ListFilePayload(string? Format, int Version, string? Name, ListFileItem[]? Items);
internal sealed record ListFileItem(string? Name, decimal? Quantity, string? Unit, string? Sector,
    string? Note, bool IsPurchased);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ListFilePayload))]
internal partial class ListFileJsonContext : JsonSerializerContext;
