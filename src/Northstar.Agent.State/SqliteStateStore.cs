using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Northstar.Agent.State;

/// <summary>
/// Survives process restarts. Single-node only: SQLite is a file, so scale-out needs a shared store
/// (see docs/state-choices.md).
/// </summary>
public sealed class SqliteStateStore : IStateStore
{
    private readonly string _connectionString;
    private volatile bool _schemaReady;

    public SqliteStateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        // Pooling off so the file handle is released after each call (matters for tests and Windows).
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public async Task<ConversationState> GetAsync(string conversationId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT state_json FROM conversation_state WHERE id = $id";
        command.Parameters.AddWithValue("$id", conversationId);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        return json is null
            ? ConversationState.Empty
            : JsonSerializer.Deserialize<ConversationState>(json) ?? ConversationState.Empty;
    }

    public async Task SaveAsync(string conversationId, ConversationState state, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO conversation_state (id, state_json, updated_at) VALUES ($id, $json, $now)
            ON CONFLICT(id) DO UPDATE SET state_json = excluded.state_json, updated_at = excluded.updated_at
            """;
        command.Parameters.AddWithValue("$id", conversationId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(state));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS conversation_state (
              id TEXT PRIMARY KEY,
              state_json TEXT NOT NULL,
              updated_at TEXT NOT NULL)
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _schemaReady = true;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
