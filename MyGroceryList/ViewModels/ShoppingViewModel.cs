using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Services;
using MyGroceryList.Application.Transfer;

namespace MyGroceryList.Mobile.ViewModels;

public partial class ItemRowViewModel : ObservableObject
{
    public Guid Id { get; }
    public IAsyncRelayCommand ToggleCommand { get; }
    public IAsyncRelayCommand EditCommand { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EditAccessibilityLabel))]
    public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Detail { get; set; } = "";
    [ObservableProperty] public partial string QuantityLabel { get; set; } = "";
    [ObservableProperty] public partial string Note { get; set; } = "";
    [ObservableProperty] public partial string ProductIcon { get; set; } = "";
    [ObservableProperty] public partial bool HasNote { get; set; }
    public string EditAccessibilityLabel => $"Editar {Name}";
    [ObservableProperty] public partial string AccessibilityLabel { get; set; } = "";
    [ObservableProperty] public partial bool IsPurchased { get; set; }
    public ItemRowViewModel(Guid id, Func<Task> toggle, Func<Task> edit)
    {
        Id = id;
        ToggleCommand = new AsyncRelayCommand(toggle);
        EditCommand = new AsyncRelayCommand(edit);
    }

    public void Update(GroceryItem item)
    {
        Name = item.Details.Name;
        IsPurchased = item.IsPurchased;
        var quantity = item.Details.Quantity?.ToString("0.############################", CultureInfo.GetCultureInfo("pt-BR"));
        QuantityLabel = string.Join(' ', new[] { quantity, item.Details.Unit }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Note = item.Details.Note ?? "";
        HasNote = !string.IsNullOrEmpty(Note);
        ProductIcon = ProductAppearance.IconFor(Name, item.Details.Sector);
        Detail = string.Join(" · ", new[] { QuantityLabel, item.Details.Note }.Where(s => !string.IsNullOrWhiteSpace(s)));
        AccessibilityLabel = $"{(IsPurchased ? "Desmarcar" : "Marcar como comprado")}: {Name}. {Detail}";
    }
}

public sealed class ItemGroup(string name) : ObservableCollection<ItemRowViewModel>
{
    public string Name { get; } = name;
}

public partial class ShoppingViewModel(ShoppingService shopping, IAppNavigation navigation,
    IUserInteraction interaction, IUiDispatcher dispatcher, ListTransferService transfer,
    IListFiles files) : ViewModelBase(interaction)
{
    private GroceryList? current;
    private readonly Dictionary<Guid, ItemRowViewModel> rows = [];
    private readonly Dictionary<string, ItemGroup> groups = Sectors.All.Append("Comprados")
        .ToDictionary(s => s, s => new ItemGroup(s));
    public Guid ListId { get; set; }
    public ObservableCollection<ItemGroup> Groups { get; } = [];
    [ObservableProperty] public partial string Title { get; set; } = "Minha lista";
    [ObservableProperty] public partial string QuickEntry { get; set; } = "";
    [ObservableProperty] public partial string ProgressLabel { get; set; } = "";
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial bool CanUndo { get; set; }
    [ObservableProperty] public partial bool ShowPurchased { get; set; }
    [ObservableProperty] public partial string PurchasedLabel { get; set; } = "Comprados";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial bool AllPurchased { get; set; }
    [ObservableProperty] public partial bool HasPurchased { get; set; }
    public event EventHandler? EntryAdded;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () => await ApplyAsync(await shopping.OpenAsync(ListId)));

    [RelayCommand]
    private Task ExportAsync(CancellationToken ct) => RunAsync(async () =>
    {
        var selected = await Interaction.ChooseAsync("Exportar lista", "CSV (.csv)", "MyGroceryList (.json)");
        if (selected is null) return;
        var format = selected == "CSV (.csv)" ? ListFileFormat.Csv : ListFileFormat.Json;
        var file = await transfer.ExportAsync(ListId, format, ct);
        await files.ShareAsync(file, ct);
    });

    [RelayCommand]
    private Task AddAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(QuickEntry)) return;
        var submittedText = QuickEntry;
        var draft = await shopping.PrepareEntryAsync(submittedText);
        var latest = await shopping.OpenAsync(ListId);
        var duplicate = latest.Items.FirstOrDefault(i => !i.IsPurchased &&
            ProductText.Key(i.Details.Name) == ProductText.Key(draft.Name));
        Guid? mergeInto = null;
        if (duplicate is not null)
        {
            var canMerge = duplicate.CanMerge(draft);
            var choice = await Interaction.ChooseAsync("Este produto já está na lista",
                canMerge ? ["Somar quantidades", "Manter separado"] : ["Manter separado"]);
            if (choice is null) return;
            if (choice == "Somar quantidades") mergeInto = duplicate.Id;
        }
        await ApplyAsync(await shopping.AddAsync(ListId, draft, mergeInto));
        if (QuickEntry == submittedText) QuickEntry = "";
        StatusMessage = $"{draft.Name} adicionado.";
        EntryAdded?.Invoke(this, EventArgs.Empty);
    });

    private Task ToggleAsync(Guid id) => RunAsync(async () =>
    {
        var item = current?.Items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        await ApplyAsync(await shopping.SetPurchasedAsync(ListId, id, !item.IsPurchased));
        StatusMessage = item.IsPurchased ? $"{item.Details.Name} voltou aos pendentes."
            : $"{item.Details.Name} comprado. Você pode desfazer.";
    });

    [RelayCommand]
    private Task UndoAsync() => RunAsync(async () =>
    {
        await ApplyAsync(await shopping.UndoAsync(ListId));
        StatusMessage = "Última marcação desfeita.";
    });

    [RelayCommand]
    private async Task TogglePurchasedAsync()
    {
        if (IsBusy) return;
        ShowPurchased = !ShowPurchased;
        if (current is not null) await ApplyAsync(current);
    }

    [RelayCommand]
    private Task RenameAsync() => RunAsync(async () =>
    {
        var name = await Interaction.PromptAsync("Renomear lista", "Nome da lista", Title);
        if (name is not null) await ApplyAsync(await shopping.RenameAsync(ListId, name));
    });

    [RelayCommand]
    private Task ArchiveAsync() => RunAsync(async () =>
    {
        if (!await Interaction.ConfirmAsync("Arquivar lista?", "A lista será guardada com todos os itens. Você poderá restaurá-la em Ver arquivadas.", "Arquivar")) return;
        await shopping.ArchiveAsync(ListId, true);
        await navigation.BackAsync();
    });

    private Task EditAsync(Guid id) => RunAsync(() => navigation.EditItemAsync(ListId, id));

    private Task ApplyAsync(GroceryList list) => dispatcher.InvokeAsync(() =>
    {
        current = list;
        Title = list.Name;
        var purchased = list.Items.Count(i => i.IsPurchased);
        ProgressLabel = $"{purchased} de {list.Items.Count} itens comprados";
        Progress = list.Items.Count == 0 ? 0 : (double)purchased / list.Items.Count;
        IsEmpty = list.Items.Count == 0;
        AllPurchased = !IsEmpty && purchased == list.Items.Count;
        HasPurchased = purchased > 0;
        CanUndo = list.Undo is not null;
        PurchasedLabel = $"{(ShowPurchased ? "⌃ Ocultar" : "⌄ Mostrar")} comprados ({purchased})";

        foreach (var id in rows.Keys.Where(id => list.Items.All(i => i.Id != id)).ToArray()) rows.Remove(id);
        foreach (var item in list.Items)
        {
            if (!rows.TryGetValue(item.Id, out var row))
                rows[item.Id] = row = new(item.Id, () => ToggleAsync(item.Id), () => EditAsync(item.Id));
            row.Update(item);
        }
        var visible = new List<ItemGroup>();
        foreach (var sector in Sectors.All)
        {
            var group = groups[sector];
            var desired = list.Items.Where(i => !i.IsPurchased && i.Details.Sector == sector)
                .OrderBy(i => i.Position).Select(i => rows[i.Id]).ToArray();
            CollectionChanges.Reconcile(group, desired);
            if (desired.Length > 0) visible.Add(group);
        }
        var bought = groups["Comprados"];
        CollectionChanges.Reconcile(bought, list.Items.Where(i => i.IsPurchased).OrderBy(i => i.Position)
            .Select(i => rows[i.Id]).ToArray());
        if (ShowPurchased && bought.Count > 0) visible.Add(bought);
        CollectionChanges.Reconcile(Groups, visible);
    });
}
