using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Application.Transfer;
using MyGroceryList.Mobile.Services;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Mobile.ViewModels;

public partial class ImportItemViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsIncluded { get; set; } = true;
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))]
    public partial string Quantity { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))]
    public partial string Unit { get; set; } = "";
    [ObservableProperty] public partial string Sector { get; set; } = Sectors.Other;
    [ObservableProperty] public partial string Note { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool IsPurchased { get; set; }
    [ObservableProperty] public partial bool IsEditing { get; set; }
    public IReadOnlyList<string> AvailableSectors => Sectors.All;
    public string Summary => string.Join(" · ", new[] { string.Join(' ', new[] { Quantity, Unit }
        .Where(value => !string.IsNullOrWhiteSpace(value))), IsPurchased ? "Comprado" : "Pendente" }
        .Where(value => !string.IsNullOrWhiteSpace(value)));

    public ImportItemViewModel(ImportedItem item)
    {
        Name = item.Details.Name;
        Quantity = item.Details.Quantity?.ToString(CultureInfo.GetCultureInfo("pt-BR")) ?? "";
        Unit = item.Details.Unit ?? "";
        Sector = item.Details.Sector;
        Note = item.Details.Note ?? "";
        IsPurchased = item.IsPurchased;
    }

    public ImportedItem ToImportedItem() => new(new ItemDraft(Name, QuickEntryParser.ParseQuantity(Quantity),
        Unit, Sector, Note).Validated(), IsPurchased);

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;
}

public partial class ImportListViewModel(ListTransferService transfer, IAppNavigation navigation,
    IUserInteraction interaction) : ViewModelBase(interaction)
{
    private Guid? importedListId;
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit)), NotifyPropertyChangedFor(nameof(ImportButtonLabel))]
    public partial bool HasImported { get; set; }
    public ObservableCollection<ImportItemViewModel> Items { get; } = [];
    public bool CanEdit => !HasImported && !IsBusy;
    public string ImportButtonLabel => HasImported ? "Abrir lista" : "Importar lista";

    public void Initialize(ListImportPreview preview)
    {
        Name = preview.Name;
        Items.Clear();
        foreach (var item in preview.Items) Items.Add(new(item));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanEdit));
    }

    [RelayCommand]
    private Task ImportAsync(CancellationToken ct) => RunAsync(async () =>
    {
        if (importedListId is null)
        {
            var selected = Items.Where(item => item.IsIncluded).Select(item => item.ToImportedItem()).ToArray();
            var list = await transfer.ImportAsync(Name, selected, ct);
            importedListId = list.Id;
            HasImported = true;
        }
        await navigation.OpenImportedListAsync(importedListId.Value);
    });
}
