using Microsoft.Data.Sqlite;

namespace MyGroceryList.Mobile.Data.Shopping;

public static class DatabaseMigrations
{
    public const int CurrentVersion = 2;

    public static void Apply(SqliteConnection connection)
    {
        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(versionCommand.ExecuteScalar());
        if (version > CurrentVersion)
            throw new InvalidOperationException("Este banco foi criado por uma versão mais recente do MyGroceryList.");

        using var transaction = connection.BeginTransaction();
        if (version < 1)
            Execute(connection, transaction, """
                CREATE TABLE ShoppingLists (
                    Id TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL, IsArchived INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE ShoppingItems (
                    Id TEXT PRIMARY KEY NOT NULL, ListId TEXT NOT NULL REFERENCES ShoppingLists(Id) ON DELETE CASCADE,
                    Name TEXT NOT NULL, Quantity TEXT NULL, Unit TEXT NULL, Sector TEXT NOT NULL,
                    Note TEXT NULL, IsPurchased INTEGER NOT NULL DEFAULT 0, Position INTEGER NOT NULL);
                CREATE INDEX IX_Lists_Archived_Created ON ShoppingLists(IsArchived, CreatedAt DESC);
                CREATE INDEX IX_Items_List_Sector_Position ON ShoppingItems(ListId, Sector, Position);
                """);
        if (version < 2)
            Execute(connection, transaction, """
                ALTER TABLE ShoppingLists ADD COLUMN UndoItemId TEXT NULL;
                ALTER TABLE ShoppingLists ADD COLUMN UndoPreviousValue INTEGER NULL;
                CREATE TABLE ProductSectors (ProductKey TEXT PRIMARY KEY NOT NULL, Sector TEXT NOT NULL);
                """);
        Execute(connection, transaction, $"PRAGMA user_version = {CurrentVersion};");
        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
