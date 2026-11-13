using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.ViewModels;

public sealed record ListCard(Guid Id, string Name, string ProgressLabel, string ActionLabel);

public partial class ListsViewModel(ShoppingService shopping, IAppNavigation navigation,
    IUserInteraction interaction, IUiDispatcher dispatcher) : ViewModelBase(interaction)
{
    public ObservableCollection<ListCard> Lists { get; } = [];
    [ObservableProperty] public partial bool ShowingArchived { get; set; }
    public string SectionTitle => ShowingArchived ? "Listas arquivadas" : "Minhas listas";
    public string SwitchLabel => ShowingArchived ? "Ver listas ativas" : "Ver arquivadas";
    public string EmptyTitle => ShowingArchived ? "Nenhuma lista arquivada" : "Sua próxima compra começa aqui";
    public string EmptyDescription => ShowingArchived ? "As listas que você arquivar aparecerão aqui."
        : "Crie uma lista e vá lembrando dos produtos ao longo da semana.";

    [RelayCommand]
    private Task LoadAsync() => RunAsync(RefreshAsync);

    private async Task RefreshAsync()
    {
        var lists = await shopping.GetListsAsync(ShowingArchived);
        await dispatcher.InvokeAsync(() => CollectionChanges.Reconcile(Lists, lists.Select(l =>
            new ListCard(l.Id, l.Name, $"{l.Purchased} de {l.Total} comprados",
                l.IsArchived ? "Restaurar lista" : "Abrir lista")).ToArray()));
    }

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        var name = await Interaction.PromptAsync("Nova lista", "Como você quer chamar sua lista?", "Mercado da semana");
        if (name is null) return;
        var list = await shopping.CreateAsync(name);
        // Refresh now: navigation can return to this page while the command is completing.
        if (ShowingArchived) SetArchiveView(false);
        await RefreshAsync();
        await navigation.OpenListAsync(list.Id);
    });

    [RelayCommand]
    private Task OpenAsync(ListCard? card) => RunAsync(async () =>
    {
        if (card is null) return;
        if (ShowingArchived)
        {
            if (!await Interaction.ConfirmAsync("Restaurar lista?", "A lista voltará às suas compras ativas.", "Restaurar")) return;
            await shopping.ArchiveAsync(card.Id, false);
            SetArchiveView(false);
            await RefreshAsync();
        }
        await navigation.OpenListAsync(card.Id);
    });

    [RelayCommand]
    private Task SwitchArchiveAsync() => RunAsync(async () =>
    {
        SetArchiveView(!ShowingArchived);
        await RefreshAsync();
    });

    private void SetArchiveView(bool value)
    {
        ShowingArchived = value;
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SwitchLabel));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
    }
}
