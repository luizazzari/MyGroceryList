using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.ViewModels;

public partial class ItemEditorViewModel(ShoppingService shopping, IAppNavigation navigation,
    IUserInteraction interaction) : ViewModelBase(interaction)
{
    public Guid ListId { get; set; }
    public Guid ItemId { get; set; }
    public IReadOnlyList<string> AvailableSectors => Sectors.All;
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Quantity { get; set; } = "";
    [ObservableProperty] public partial string Unit { get; set; } = "";
    [ObservableProperty] public partial string Sector { get; set; } = Sectors.Other;
    [ObservableProperty] public partial string Note { get; set; } = "";
    [ObservableProperty] public partial bool IsLoaded { get; set; }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        if (IsLoaded) return;
        var item = (await shopping.OpenAsync(ListId)).Items.FirstOrDefault(i => i.Id == ItemId)
            ?? throw new ShoppingValidationException("Este item não está mais na lista.");
        Name = item.Details.Name;
        Quantity = item.Details.Quantity?.ToString(CultureInfo.GetCultureInfo("pt-BR")) ?? "";
        Unit = item.Details.Unit ?? "";
        Sector = item.Details.Sector;
        Note = item.Details.Note ?? "";
        IsLoaded = true;
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (!IsLoaded) return;
        await shopping.EditAsync(ListId, ItemId, new(Name, QuickEntryParser.ParseQuantity(Quantity), Unit, Sector, Note));
        await navigation.BackAsync();
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (!IsLoaded || !await Interaction.ConfirmAsync("Excluir produto?", $"Remover {Name} desta lista?", "Excluir")) return;
        await shopping.RemoveAsync(ListId, ItemId);
        await navigation.BackAsync();
    });
}
