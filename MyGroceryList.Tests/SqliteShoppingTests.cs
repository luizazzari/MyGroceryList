using Microsoft.Data.Sqlite;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Data.Shopping;

namespace MyGroceryList.Tests;

public sealed class SqliteShoppingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "MyGroceryList.Tests", Guid.NewGuid().ToString());
    private string DatabasePath => Path.Combine(directory, "shopping.db");
    private ShoppingService NewService() => new(new SqliteShoppingRepository(DatabasePath), new FixedTime());

    [Fact]
    public async Task Reopening_the_database_recovers_lists_items_decimals_notes_marks_and_undo()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado da semana", ct: TestContext.Current.CancellationToken);
        await service.CreateAsync("Churrasco", ct: TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Queijo", 0.5m, "kg", "Laticínios", "Sem lactose"), ct: TestContext.Current.CancellationToken);
        var itemId = list.Items[0].Id;
        await service.SetPurchasedAsync(list.Id, itemId, true, ct: TestContext.Current.CancellationToken);

        var reopenedService = NewService();
        var reopened = await reopenedService.OpenAsync(list.Id, ct: TestContext.Current.CancellationToken);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 15, 0, 0, TimeSpan.Zero), reopened.CreatedAt);
        var item = Assert.Single(reopened.Items);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(0.5m, item.Details.Quantity);
        Assert.Equal("Sem lactose", item.Details.Note);
        Assert.Equal("Laticínios", item.Details.Sector);
        Assert.True(item.IsPurchased);
        Assert.NotNull(reopened.Undo);
        Assert.Equal(2, (await reopenedService.GetListsAsync(ct: TestContext.Current.CancellationToken)).Count);
        var undone = await reopenedService.UndoAsync(list.Id, ct: TestContext.Current.CancellationToken);
        Assert.False(undone.Items[0].IsPurchased);
        Assert.Null((await NewService().OpenAsync(list.Id, ct: TestContext.Current.CancellationToken)).Undo);
    }

    [Fact]
    public async Task Editing_remembers_sector_with_case_and_accent_insensitive_identity()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado", ct: TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Maçã", 1.5m, "kg"), ct: TestContext.Current.CancellationToken);
        await service.EditAsync(list.Id, list.Items[0].Id, new("Maçã", 1.5m, "kg", "Hortifruti"), ct: TestContext.Current.CancellationToken);
        var draft = await NewService().PrepareEntryAsync("2 kg de MACA", ct: TestContext.Current.CancellationToken);
        Assert.Equal("Hortifruti", draft.Sector);
        Assert.Equal("MACA", draft.Name);
        Assert.Equal("Outros", (await service.PrepareEntryAsync("Maçã verde", ct: TestContext.Current.CancellationToken)).Sector);
    }

    [Fact]
    public async Task Rename_archive_restore_and_delete_survive_new_repository_instances()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado", ct: TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Leite"), ct: TestContext.Current.CancellationToken);
        await service.RenameAsync(list.Id, "Compra do mês", ct: TestContext.Current.CancellationToken);
        await service.ArchiveAsync(list.Id, true, ct: TestContext.Current.CancellationToken);
        Assert.Empty(await NewService().GetListsAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal("Compra do mês", Assert.Single(await NewService().GetListsAsync(true, ct: TestContext.Current.CancellationToken)).Name);
        await NewService().ArchiveAsync(list.Id, false, ct: TestContext.Current.CancellationToken);
        await NewService().RemoveAsync(list.Id, list.Items[0].Id, ct: TestContext.Current.CancellationToken);
        Assert.Empty((await NewService().OpenAsync(list.Id, ct: TestContext.Current.CancellationToken)).Items);
    }

    [Fact]
    public async Task Concurrent_writes_do_not_lose_items()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado", ct: TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(1, 25).Select(i => service.AddAsync(list.Id, new($"Produto {i}"), ct: TestContext.Current.CancellationToken)));
        Assert.Equal(25, (await NewService().OpenAsync(list.Id, ct: TestContext.Current.CancellationToken)).Items.Count);
    }

    [Fact]
    public async Task Failing_partway_through_a_write_rolls_back_the_list_and_item_changes()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado", ct: TestContext.Current.CancellationToken);
        list = await service.AddAsync(list.Id, new("Leite"), ct: TestContext.Current.CancellationToken);
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER SimulateDiskFailure BEFORE UPDATE ON ShoppingItems
                BEGIN SELECT RAISE(ABORT, 'Simulated failure'); END;
                """;
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() => service.SetPurchasedAsync(list.Id, list.Items[0].Id, true, ct: TestContext.Current.CancellationToken));
        var recovered = await NewService().OpenAsync(list.Id, ct: TestContext.Current.CancellationToken);
        Assert.False(recovered.Items[0].IsPurchased);
        Assert.Null(recovered.Undo);
    }

    [Fact]
    public async Task Migration_from_version_one_preserves_existing_data_and_adds_undo_and_preferences()
    {
        Directory.CreateDirectory(directory);
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE ShoppingLists(Id TEXT PRIMARY KEY NOT NULL,Name TEXT NOT NULL,CreatedAt TEXT NOT NULL,IsArchived INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE ShoppingItems(Id TEXT PRIMARY KEY NOT NULL,ListId TEXT NOT NULL REFERENCES ShoppingLists(Id),Name TEXT NOT NULL,
                    Quantity TEXT NULL,Unit TEXT NULL,Sector TEXT NOT NULL,Note TEXT NULL,IsPurchased INTEGER NOT NULL DEFAULT 0,Position INTEGER NOT NULL);
                CREATE INDEX IX_Lists_Archived_Created ON ShoppingLists(IsArchived,CreatedAt DESC);
                CREATE INDEX IX_Items_List_Sector_Position ON ShoppingItems(ListId,Sector,Position);
                INSERT INTO ShoppingLists VALUES ($list,'Lista anterior','2026-11-01T10:00:00+00:00',0);
                INSERT INTO ShoppingItems VALUES ($item,$list,'Arroz','1.5','kg','Mercearia',NULL,1,0);
                PRAGMA user_version=1;
                """;
            command.Parameters.AddWithValue("$list", listId.ToString());
            command.Parameters.AddWithValue("$item", itemId.ToString());
            command.ExecuteNonQuery();
        }
        var service = NewService();
        var migrated = await service.OpenAsync(listId, ct: TestContext.Current.CancellationToken);
        Assert.Equal("Lista anterior", migrated.Name);
        Assert.Equal(1.5m, Assert.Single(migrated.Items).Details.Quantity);
        Assert.True(migrated.Items[0].IsPurchased);
        Assert.Null(migrated.Undo);
        await service.SetPurchasedAsync(listId, itemId, false, ct: TestContext.Current.CancellationToken);
        await service.EditAsync(listId, itemId, migrated.Items[0].Details with { Sector = "Outros" }, ct: TestContext.Current.CancellationToken);
        Assert.True((await service.UndoAsync(listId, ct: TestContext.Current.CancellationToken)).Items[0].IsPurchased);
        Assert.Equal("Outros", (await NewService().PrepareEntryAsync("Arroz", ct: TestContext.Current.CancellationToken)).Sector);
        using var verify = OpenConnection();
        using var version = verify.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(DatabaseMigrations.CurrentVersion, Convert.ToInt32(version.ExecuteScalar()));
    }

    [Fact]
    public async Task Cancellation_before_a_write_preserves_the_saved_state()
    {
        var service = NewService();
        var list = await service.CreateAsync("Mercado", ct: TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AddAsync(list.Id, new("Leite"), ct: cancelled.Token));
        Assert.Empty((await NewService().OpenAsync(list.Id, ct: TestContext.Current.CancellationToken)).Items);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    }
}
