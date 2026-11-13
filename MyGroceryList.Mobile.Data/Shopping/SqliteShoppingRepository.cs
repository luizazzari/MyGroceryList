using System.Globalization;
using Microsoft.Data.Sqlite;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Mobile.Data.Shopping;

/// <summary>Short-lived connections; all SQLite I/O runs off the UI thread.</summary>
public sealed class SqliteShoppingRepository(string databasePath) : IShoppingRepository
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;

    public Task<IReadOnlyList<ListSummary>> GetListsAsync(bool archived, CancellationToken cancellationToken = default) =>
        WithConnectionAsync<IReadOnlyList<ListSummary>>(connection =>
        {
            using var command = Command(connection, null, """
                SELECT l.Id, l.Name, l.IsArchived, COUNT(i.Id), COALESCE(SUM(i.IsPurchased),0)
                FROM ShoppingLists l LEFT JOIN ShoppingItems i ON i.ListId = l.Id
                WHERE l.IsArchived = $archived
                GROUP BY l.Id ORDER BY l.CreatedAt DESC, l.Id;
                """, ("$archived", archived));
            using var reader = command.ExecuteReader();
            var lists = new List<ListSummary>();
            while (reader.Read())
                lists.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetBoolean(2),
                    reader.GetInt32(3), reader.GetInt32(4)));
            return lists;
        }, cancellationToken);

    public Task<GroceryList> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(connection =>
        {
            using var snapshot = connection.BeginTransaction(deferred: true);
            var list = Read(connection, snapshot, id);
            snapshot.Commit();
            return list;
        }, cancellationToken);

    public Task CreateAsync(GroceryList list, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = Command(connection, transaction, """
                INSERT INTO ShoppingLists (Id, Name, CreatedAt, IsArchived) VALUES ($id,$name,$created,$archived);
                """, ("$id", list.Id.ToString()), ("$name", list.Name),
                ("$created", list.CreatedAt.ToString("O", CultureInfo.InvariantCulture)), ("$archived", list.IsArchived));
            command.ExecuteNonQuery();
            SaveItems(connection, transaction, list.Id, [], list.Items);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return true;
        }, cancellationToken);

    public Task<GroceryList> UpdateAsync(Guid id, Func<GroceryList, ListMutation> update,
        CancellationToken cancellationToken = default) => WithConnectionAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var previous = Read(connection, transaction, id);
            var mutation = update(previous);
            var list = mutation.List;
            if (list.Id != id) throw new InvalidOperationException("A identidade da lista não pode mudar.");
            using var command = Command(connection, transaction, """
                UPDATE ShoppingLists SET Name=$name, IsArchived=$archived,
                    UndoItemId=$undoId, UndoPreviousValue=$undoValue WHERE Id=$id;
                """, ("$name", list.Name), ("$archived", list.IsArchived), ("$id", id.ToString()),
                ("$undoId", list.Undo?.ItemId.ToString()), ("$undoValue", list.Undo?.PreviousValue));
            command.ExecuteNonQuery();
            SaveItems(connection, transaction, list.Id, previous.Items, list.Items);
            if (mutation.Preference is { } preference)
            {
                using var savePreference = Command(connection, transaction, """
                    INSERT INTO ProductSectors(ProductKey,Sector) VALUES ($key,$sector)
                    ON CONFLICT(ProductKey) DO UPDATE SET Sector=excluded.Sector;
                    """, ("$key", preference.ProductKey), ("$sector", preference.Sector));
                savePreference.ExecuteNonQuery();
            }
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return list;
        }, cancellationToken);

    public Task<string?> GetPreferredSectorAsync(string productKey, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(connection =>
        {
            using var command = Command(connection, null, "SELECT Sector FROM ProductSectors WHERE ProductKey=$key;",
                ("$key", productKey));
            return command.ExecuteScalar() as string;
        }, cancellationToken);

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, T> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
                Directory.CreateDirectory(directory!);
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                    { DataSource = databasePath, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString());
                connection.Open();
                if (!initialized)
                {
                    DatabaseMigrations.Apply(connection);
                    initialized = true;
                }
                ct.ThrowIfCancellationRequested();
                return action(connection);
            }, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static GroceryList Read(SqliteConnection connection, SqliteTransaction? transaction, Guid id)
    {
        string name;
        DateTimeOffset created;
        bool archived;
        LastMark? undo;
        using (var command = Command(connection, transaction,
            "SELECT Name,CreatedAt,IsArchived,UndoItemId,UndoPreviousValue FROM ShoppingLists WHERE Id=$id;",
            ("$id", id.ToString())))
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) throw new ShoppingValidationException("A lista não foi encontrada.");
            name = reader.GetString(0);
            created = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
            archived = reader.GetBoolean(2);
            undo = reader.IsDBNull(3) ? null : new(Guid.Parse(reader.GetString(3)), reader.GetBoolean(4));
        }
        var items = new List<GroceryItem>();
        using (var command = Command(connection, transaction, """
            SELECT Id,Name,Quantity,Unit,Sector,Note,IsPurchased,Position
            FROM ShoppingItems WHERE ListId=$id ORDER BY Position,Id;
            """, ("$id", id.ToString())))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                items.Add(new(Guid.Parse(reader.GetString(0)), new(reader.GetString(1),
                    reader.IsDBNull(2) ? null : decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                    NullableText(reader, 3), reader.GetString(4), NullableText(reader, 5)),
                    reader.GetBoolean(6), reader.GetInt64(7)));
        }
        return new(id, name, created, archived, items, undo);
    }

    private static void SaveItems(SqliteConnection connection, SqliteTransaction transaction, Guid listId,
        IReadOnlyList<GroceryItem> previous, IReadOnlyList<GroceryItem> current)
    {
        var previousById = previous.ToDictionary(i => i.Id);
        var currentIds = current.Select(i => i.Id).ToHashSet();
        foreach (var removed in previous.Where(i => !currentIds.Contains(i.Id)))
        {
            using var delete = Command(connection, transaction, "DELETE FROM ShoppingItems WHERE Id=$id;", ("$id", removed.Id.ToString()));
            delete.ExecuteNonQuery();
        }
        foreach (var item in current)
        {
            if (previousById.TryGetValue(item.Id, out var old) && old == item) continue;
            // ListId is supplied by the enclosing aggregate, never inferred from user input.
            using var save = Command(connection, transaction, """
                INSERT INTO ShoppingItems(Id,ListId,Name,Quantity,Unit,Sector,Note,IsPurchased,Position)
                VALUES ($id,$list,$name,$quantity,$unit,$sector,$note,$purchased,$position)
                ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,Quantity=excluded.Quantity,Unit=excluded.Unit,
                    Sector=excluded.Sector,Note=excluded.Note,IsPurchased=excluded.IsPurchased,Position=excluded.Position;
                """, ("$id", item.Id.ToString()), ("$list", listId.ToString()),
                ("$name", item.Details.Name), ("$quantity", item.Details.Quantity?.ToString(CultureInfo.InvariantCulture)),
                ("$unit", item.Details.Unit), ("$sector", item.Details.Sector), ("$note", item.Details.Note),
                ("$purchased", item.IsPurchased), ("$position", item.Position));
            save.ExecuteNonQuery();
        }
    }

    private static string? NullableText(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Key, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }
}
