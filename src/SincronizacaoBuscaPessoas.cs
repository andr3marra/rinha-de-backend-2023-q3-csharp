using NATS.Client.Core;
using System.Collections.Concurrent;

namespace RinhaDeBackEnd;
public class SincronizacaoBuscaPessoas
    : BackgroundService {
    private readonly ConcurrentDictionary<string, Pessoa> _pessoasMap;
    private readonly ConcurrentDictionary<Guid, Pessoa> _pessoasById;
    private readonly ConcurrentDictionary<string, byte> _apelidoPessoas;
    private readonly INatsConnection _nats;
    private readonly ILogger<SincronizacaoBuscaPessoas> _logger;
    private readonly string natsOwnChannel;

    public SincronizacaoBuscaPessoas(
        ConcurrentDictionary<string, Pessoa> pessoasMap,
        ConcurrentDictionary<Guid, Pessoa> pessoasById,
        ConcurrentDictionary<string, byte> apelidoPessoas,
        INatsConnection natsConnection,
        ILogger<SincronizacaoBuscaPessoas> logger,
        string natsOwnChannel) {
        _pessoasMap = pessoasMap;
        _nats = natsConnection;
        _logger = logger;
        this.natsOwnChannel = natsOwnChannel;
        _pessoasById = pessoasById;
        _apelidoPessoas = apelidoPessoas;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await using var sub = await _nats.SubscribeAsync<Pessoa>(natsOwnChannel, cancellationToken: stoppingToken);
        await foreach (var msg in sub.Msgs.ReadAllAsync(stoppingToken)) {
            var pessoa = msg.Data;
            var buscaStackValue = pessoa.Stack == null ? "" : string.Join("", pessoa.Stack.Select(s => s.ToString()));
            var buscaValue = $"{pessoa.Apelido}{pessoa.Nome}{buscaStackValue}" ?? "";
            _pessoasMap.TryAdd(buscaValue, pessoa);
            _pessoasById.TryAdd(pessoa.Id.Value, pessoa);
            _apelidoPessoas.TryAdd(pessoa.Apelido, default(byte));
        }
    }
}