using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Client.Hosting;
using Npgsql;
using RinhaDeBackEnd;
using System.Collections.Concurrent;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNats(1, configureOptions: options => NatsOptions.Default with { Url = Environment.GetEnvironmentVariable("NATS_URL") });

builder.Services.AddNpgsqlDataSource(
    Environment.GetEnvironmentVariable(
        "DB_CONNECTION_STRING") ??
        "ERRO de connection string!!!", dataSourceBuilderAction: a => { a.UseLoggerFactory(NullLoggerFactory.Instance); });

builder.Services.AddSingleton(_ => new ConcurrentDictionary<string, Pessoa>());
builder.Services.AddSingleton(_ => new ConcurrentDictionary<Guid, Pessoa>());
builder.Services.AddSingleton(_ => Channel.CreateUnbounded<Pessoa>(new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddSingleton(_ => new ConcurrentDictionary<string, byte>());

builder.Services.AddHostedService<InsercaoRegistrosPessoas>();

builder.Services.AddSingleton<IHostedService, SincronizacaoBuscaPessoas>();

var natsDestination =  Environment.GetEnvironmentVariable("NATS_DESTINATION");
var natsOwnChannel = Environment.GetEnvironmentVariable("NATS_OWN");

builder.Services.AddSingleton<string>(natsOwnChannel ?? "");

builder.Services.AddOutputCache();

var app = builder.Build();

app.UseOutputCache();

var DuplicatedResultStringResponse = Results.Text(ResponseCriacao.DuplicatedResultString, contentType: "application/json; charset=utf-8", statusCode: 422);
var ResponseAfeStringResponse = Results.Text(ResponseCriacao.ResponseAfeString, contentType: "application/json; charset=utf-8", statusCode: 422);

app.MapPost("/pessoas", async (HttpContext http,
                               Channel<Pessoa> channel,
                               ConcurrentDictionary<string, Pessoa> pessoaDicionary,
                               ConcurrentDictionary<Guid, Pessoa> pessoasById,
                               ConcurrentDictionary<string, byte> apelidoPessoas,
                               INatsConnection natsConnection,
                               Pessoa pessoa) => {

                                   if (!Pessoa.BasicamenteValida(pessoa)) {
                                       return ResponseAfeStringResponse;
                                   }

                                   if (apelidoPessoas.ContainsKey(pessoa.Apelido)) {
                                       return DuplicatedResultStringResponse;
                                   }

                                   pessoa.Id = Guid.NewGuid();

                                   await natsConnection.PublishAsync(natsDestination, pessoa);
                                   await channel.Writer.WriteAsync(pessoa);

                                   apelidoPessoas.TryAdd(pessoa.Apelido, default(byte));
                                   pessoasById.TryAdd(pessoa.Id.Value, pessoa);

                                   http.Response.Headers.Location = $"/pessoas/{pessoa.Id}";
                                   http.Response.StatusCode = 201;
                                   return Results.Json(new ResponseCriacao { Pessoa = pessoa }, ResponseCriacaoContext.Default.ResponseCriacao);

                               }).Produces<ResponseCriacao>();

var respostaErroResult1 = Results.Text(ResponseConsulta.RespostaErroString, contentType: "application/json; charset=utf-8", statusCode: 404);

app.MapGet("/pessoas/{id}", async (HttpContext http, ConcurrentDictionary<Guid, Pessoa> cache, Guid id) => {
    await Task.Delay(10);
    if (cache.TryGetValue(id, out var value)) {
        return Results.Json(value, PersonContext.Default.Pessoa);
    }

    return respostaErroResult1;

}).Produces<ResponseConsulta>();

var respostaErroResult2 = Results.Text(ResponseBusca.RespostaErroString, contentType: "application/json; charset=utf-8", statusCode: 400);

app.MapGet("/pessoas", (HttpContext http, ConcurrentDictionary<string, Pessoa> buscaMap, string? t) => {
    if (string.IsNullOrEmpty(t)) {
        return respostaErroResult2;
    }

    var pessoas = buscaMap.Where(p => p.Key.Contains(t))
                            .Take(50)
                            .Select(p => p.Value);

    return Results.Json(new ResponseBusca { Resultados = pessoas }, ResponseBuscaContext.Default.ResponseBusca);
}).CacheOutput(x => x.Expire(TimeSpan.FromMinutes(1))).Produces<ResponseBusca>();

app.MapGet("/contagem-pessoas", async (NpgsqlConnection conn) => {
    await using (conn) {
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(1) from pessoas";
        var count = await cmd.ExecuteScalarAsync();
        return count;
    }
});

app.Run();
