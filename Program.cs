using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseCors("frontend");

var dbPath = Path.Combine(app.Environment.ContentRootPath, "architect.db");
var connectionString = $"Data Source={dbPath}";

CriarBanco(connectionString);

app.MapGet("/", () => Results.Ok(new
{
    status = "API Architect funcionando"
}));

app.MapPost("/api/messages", async (ContactMessageInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Nome) ||
        string.IsNullOrWhiteSpace(input.Email) ||
        string.IsNullOrWhiteSpace(input.Mensagem))
    {
        return Results.BadRequest(new { message = "Preencha nome, e-mail e mensagem." });
    }

    using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO Messages (Nome, Email, Telefone, Interesse, Mensagem, DataCriacao)
        VALUES ($nome, $email, $telefone, $interesse, $mensagem, $dataCriacao);
    ";

    command.Parameters.AddWithValue("$nome", input.Nome.Trim());
    command.Parameters.AddWithValue("$email", input.Email.Trim());
    command.Parameters.AddWithValue("$telefone", (object?)input.Telefone?.Trim() ?? DBNull.Value);
    command.Parameters.AddWithValue("$interesse", (object?)input.Interesse?.Trim() ?? DBNull.Value);
    command.Parameters.AddWithValue("$mensagem", input.Mensagem.Trim());
    command.Parameters.AddWithValue("$dataCriacao", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

    await command.ExecuteNonQueryAsync();

    return Results.Ok(new
    {
        message = "Mensagem enviada com sucesso."
    });
});

app.MapGet("/api/messages", () =>
{
    var mensagens = new List<object>();

    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT Id, Nome, Email, Telefone, Interesse, Mensagem, DataCriacao
        FROM Messages
        ORDER BY Id DESC;
    ";

    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        mensagens.Add(new
        {
            id = reader.GetInt32(0),
            nome = reader.GetString(1),
            email = reader.GetString(2),
            telefone = reader.IsDBNull(3) ? "" : reader.GetString(3),
            interesse = reader.IsDBNull(4) ? "" : reader.GetString(4),
            mensagem = reader.GetString(5),
            dataCriacao = reader.GetString(6)
        });
    }

    return Results.Ok(mensagens);
});

app.MapDelete("/api/messages/{id}", async (int id) =>
{
    using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM Messages WHERE Id = $id";
    command.Parameters.AddWithValue("$id", id);

    var rows = await command.ExecuteNonQueryAsync();

    if (rows > 0)
    {
        return Results.Ok(new { message = "Mensagem excluída com sucesso." });
    }

    return Results.NotFound(new { message = "Mensagem não encontrada." });
});

app.Run();

static void CriarBanco(string connectionString)
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    var command = connection.CreateCommand();
    command.CommandText = @"
        CREATE TABLE IF NOT EXISTS Messages (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Nome TEXT NOT NULL,
            Email TEXT NOT NULL,
            Telefone TEXT NULL,
            Interesse TEXT NULL,
            Mensagem TEXT NOT NULL,
            DataCriacao TEXT NOT NULL
        );
    ";

    command.ExecuteNonQuery();
}

record ContactMessageInput(
    string Nome,
    string Email,
    string? Telefone,
    string? Interesse,
    string Mensagem
);