using Npgsql;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RinhaDeBackEnd;
public class InsercaoRegistrosPessoas
    : BackgroundService {
    private readonly ILogger<InsercaoRegistrosPessoas> _logger;
    private readonly Channel<Pessoa> _channel;
    private readonly ConcurrentDictionary<string, Pessoa> _pessasMap;
    private readonly NpgsqlConnection _conn;

    public InsercaoRegistrosPessoas(
        ILogger<InsercaoRegistrosPessoas> logger,
        Channel<Pessoa> channel,
        ConcurrentDictionary<string, Pessoa> pessasMap,
        NpgsqlConnection conn) {
        _logger = logger;
        _channel = channel;
        _pessasMap = pessasMap;
        _conn = conn;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        bool connected = false;

        while (!connected) {
            try {
                await _conn.OpenAsync();
                connected = true;
                _logger.LogInformation("connected to postgres!!! yey");
            }
            catch (NpgsqlException) {
                _logger.LogWarning("retrying connection to postgres");
                await Task.Delay(1_000);
            }
        }

        var pessoas = new List<Pessoa>();

        while (!stoppingToken.IsCancellationRequested) {

            await  foreach (var pessoa in _channel.Reader.ReadAllAsync())
            {
                pessoas.Add(pessoa);

                if(pessoas.Count < 100)
                {
                    continue;
                }

                try
                {
                    var batch = _conn.CreateBatch();
                    var batchCommands = new List<NpgsqlBatchCommand>(pessoas.Count);

                    foreach (var p in pessoas)
                    {
                        var batchCmd = new NpgsqlBatchCommand("""
                        insert into pessoas
                        (id, apelido, nome, nascimento, stack)
                        values ($1, $2, $3, $4, $5);
                    """);
                        batchCmd.Parameters.AddWithValue(p.Id);
                        batchCmd.Parameters.AddWithValue(p.Apelido);
                        batchCmd.Parameters.AddWithValue(p.Nome);
                        batchCmd.Parameters.AddWithValue(p.Nascimento.Value);
                        batchCmd.Parameters.AddWithValue(p.Stack == null ? DBNull.Value : p.Stack.Select(s => s.ToString()).ToArray());
                        batch.BatchCommands.Add(batchCmd);

                    }

                    pessoas.Clear();
                    await batch.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "erro no worker :)");
                }
            }

        }

        await _conn.CloseAsync();
        await _conn.DisposeAsync();
    }
}
