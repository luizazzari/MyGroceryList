using MyGroceryList.Application.Transfer;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Mobile.Services;

public sealed class MauiListFiles : IListFiles
{
    public async Task<ListFile?> PickAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Importar lista CSV ou JSON",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["*/*"],
                [DevicePlatform.iOS] = ["public.comma-separated-values-text", "public.json", "public.plain-text"],
                [DevicePlatform.MacCatalyst] = ["public.comma-separated-values-text", "public.json", "public.plain-text"],
                [DevicePlatform.WinUI] = [".csv", ".json"]
            })
        });
        if (result is null) return null;
        var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".json")
            throw new ShoppingValidationException("Selecione um arquivo .csv ou .json.");

        await using var source = await result.OpenReadAsync();
        using var content = new MemoryStream();
        await source.CopyToAsync(content, cancellationToken);
        return new(result.FileName, content.ToArray(), result.ContentType ?? "application/octet-stream");
    }

    public async Task ShareAsync(ListFile file, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, file.FileName);
        await File.WriteAllBytesAsync(path, file.Content, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Exportar lista",
            File = new ShareFile(path, file.ContentType)
        });
    }
}
