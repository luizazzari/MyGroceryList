using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Data.Shopping;
using MyGroceryList.Mobile.Services;
using MyGroceryList.Mobile.ViewModels;

namespace MyGroceryList.Tests;

public sealed class MobileViewModelTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "MyGroceryList.Tests", Guid.NewGuid().ToString());
    private ShoppingService Service => new(new SqliteShoppingRepository(Path.Combine(directory, "mobile.db")), TimeProvider.System);
    private readonly TestUi ui = new();

    [Fact]
    public async Task Shopping_screen_moves_only_affected_rows_and_recovers_undo_after_restart()
    {
        var service = Service;
        var list = await service.CreateAsync("Mercado", TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Maçã", 1.5m, "kg", "Hortifruti"), ct: TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Banana", 2m, Sector: "Hortifruti"), ct: TestContext.Current.CancellationToken);
        var vm = NewShopping(service, list.Id);
        await vm.LoadCommand.ExecuteAsync(null);
        var fruitGroup = Assert.Single(vm.Groups);
        var apple = fruitGroup[0];
        var banana = fruitGroup[1];
        Assert.Equal("1,5 kg", apple.Detail);
        var resets = 0;
        fruitGroup.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++;
        };
        await apple.ToggleCommand.ExecuteAsync(null);
        Assert.Same(fruitGroup, Assert.Single(vm.Groups));
        Assert.Same(banana, Assert.Single(fruitGroup));
        Assert.Equal(0, resets);
        Assert.Equal(0.5, vm.Progress);
        Assert.True(vm.CanUndo);
        Assert.False(vm.ShowPurchased);
        await vm.TogglePurchasedCommand.ExecuteAsync(null);
        Assert.Same(apple, vm.Groups[1][0]);

        var reopened = NewShopping(Service, list.Id);
        await reopened.LoadCommand.ExecuteAsync(null);
        Assert.True(reopened.CanUndo);
        Assert.Equal("1 de 2 itens comprados", reopened.ProgressLabel);
        await reopened.UndoCommand.ExecuteAsync(null);
        Assert.Equal(2, Assert.Single(reopened.Groups).Count);
        Assert.False(reopened.CanUndo);
    }

    [Fact]
    public async Task Quick_entry_clears_and_requests_focus_only_after_a_successful_save()
    {
        var service = Service;
        var list = await service.CreateAsync("Mercado", TestContext.Current.CancellationToken);
        var vm = NewShopping(service, list.Id);
        await vm.LoadCommand.ExecuteAsync(null);
        var focusRequests = 0;
        vm.EntryAdded += (_, _) => focusRequests++;
        vm.QuickEntry = "1,5 kg de arroz";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal("", vm.QuickEntry);
        Assert.Equal(1, focusRequests);
        vm.QuickEntry = new string('x', 161);
        await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal(161, vm.QuickEntry.Length);
        Assert.NotEmpty(vm.ErrorMessage);
        Assert.Equal(1, focusRequests);
        Assert.Single((await service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items);
    }

    [Fact]
    public async Task Duplicate_review_supports_cancel_safe_sum_and_separate_variants()
    {
        var service = Service;
        var list = await service.CreateAsync("Mercado", TestContext.Current.CancellationToken);
        var vm = NewShopping(service, list.Id);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.QuickEntry = "1 kg de maçã";
        await vm.AddCommand.ExecuteAsync(null);
        vm.QuickEntry = "2 kg de MACA";
        ui.Choice = null;
        await vm.AddCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.QuickEntry);
        Assert.Single((await service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items);
        ui.Choice = "Somar quantidades";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal(3m, Assert.Single((await service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items).Details.Quantity);
        vm.QuickEntry = "500 g de maçã";
        ui.Choice = "Manter separado";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.DoesNotContain("Somar quantidades", ui.LastOptions);
        vm.QuickEntry = "1 kg de maçã verde";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal(3, (await service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items.Count);
    }

    [Fact]
    public async Task Repeated_tap_while_duplicate_review_is_open_does_not_write_twice()
    {
        var service = Service;
        var list = await service.CreateAsync("Mercado", TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Arroz", 1m), ct: TestContext.Current.CancellationToken);
        var vm = NewShopping(service, list.Id);
        await vm.LoadCommand.ExecuteAsync(null);
        ui.PendingChoice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.QuickEntry = "2 Arroz";
        var first = vm.AddCommand.ExecuteAsync(null);
        await ui.ChoiceOpened.Task.WaitAsync(TestContext.Current.CancellationToken);
        await vm.AddCommand.ExecuteAsync(null);
        vm.QuickEntry = "Banana";
        ui.PendingChoice.SetResult("Manter separado");
        await first;
        Assert.Equal(2, (await service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items.Count);
        Assert.Equal("Banana", vm.QuickEntry);
    }

    [Fact]
    public async Task Item_editor_validates_and_persists_all_editable_fields_before_navigating_back()
    {
        var service = Service;
        var list = await service.CreateAsync("Mercado", TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Queijo"), ct: TestContext.Current.CancellationToken);
        var vm = new ItemEditorViewModel(service, ui, ui) { ListId = list.Id, ItemId = list.Items[0].Id };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Name = "Queijo minas";
        vm.Quantity = "1.5";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.ErrorMessage);
        Assert.Equal(0, ui.BackCount);
        vm.Quantity = "0,5";
        vm.Unit = "quilos";
        vm.Sector = "Laticínios";
        vm.Note = "Sem sal";
        await vm.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single((await Service.OpenAsync(list.Id, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(new("Queijo minas", 0.5m, "kg", "Laticínios", "Sem sal"), saved.Details);
        Assert.Equal(1, ui.BackCount);
    }

    [Fact]
    public async Task List_screen_creates_opens_archives_and_restores_personal_lists()
    {
        var service = Service;
        var vm = new ListsViewModel(service, ui, ui, ui);
        ui.Prompt = "Compra do mês";
        await vm.CreateCommand.ExecuteAsync(null);
        var card = Assert.Single(vm.Lists);
        Assert.Equal(card.Id, ui.LastOpenedList);
        await service.ArchiveAsync(card.Id, true, TestContext.Current.CancellationToken);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Lists);
        await vm.SwitchArchiveCommand.ExecuteAsync(null);
        Assert.Single(vm.Lists);
        ui.Confirmed = true;
        await vm.OpenCommand.ExecuteAsync(vm.Lists[0]);
        Assert.False(vm.ShowingArchived);
        Assert.Single(vm.Lists);
        Assert.Equal(card.Id, ui.LastOpenedList);
    }

    [Fact]
    public async Task Storage_failure_preserves_quick_entry_and_is_presented_without_an_unhandled_exception()
    {
        var vm = NewShopping(new(new FailingRepository(), TimeProvider.System), Guid.NewGuid());
        vm.QuickEntry = "Leite";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal("Leite", vm.QuickEntry);
        Assert.NotEmpty(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.Equal(1, ui.FailureCount);
    }

    private ShoppingViewModel NewShopping(ShoppingService service, Guid id) => new(service, ui, ui, ui) { ListId = id };
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private sealed class TestUi : IUserInteraction, IAppNavigation, IUiDispatcher
    {
        public string? Prompt { get; set; }
        public string? Choice { get; set; }
        public bool Confirmed { get; set; }
        public Guid LastOpenedList { get; private set; }
        public int BackCount { get; private set; }
        public int FailureCount { get; private set; }
        public string[] LastOptions { get; private set; } = [];
        public TaskCompletionSource<string?>? PendingChoice { get; set; }
        public TaskCompletionSource ChoiceOpened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> PromptAsync(string title, string message, string? initialValue = null) => Task.FromResult(Prompt);
        public Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmar") => Task.FromResult(Confirmed);
        public Task<string?> ChooseAsync(string title, params string[] options)
        {
            LastOptions = options;
            ChoiceOpened.TrySetResult();
            return PendingChoice?.Task ?? Task.FromResult(Choice);
        }
        public void ReportFailure(Exception exception) => FailureCount++;
        public Task OpenListAsync(Guid id) { LastOpenedList = id; return Task.CompletedTask; }
        public Task EditItemAsync(Guid listId, Guid itemId) => Task.CompletedTask;
        public Task BackAsync() { BackCount++; return Task.CompletedTask; }
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    }

    private sealed class FailingRepository : IShoppingRepository
    {
        public Task<IReadOnlyList<ListSummary>> GetListsAsync(bool archived, CancellationToken cancellationToken = default) => throw new IOException();
        public Task<GroceryList> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new IOException();
        public Task CreateAsync(GroceryList list, CancellationToken cancellationToken = default) => throw new IOException();
        public Task<GroceryList> UpdateAsync(Guid id, Func<GroceryList, ListMutation> update, CancellationToken cancellationToken = default) => throw new IOException();
        public Task<string?> GetPreferredSectorAsync(string productKey, CancellationToken cancellationToken = default) => throw new IOException();
    }
}
