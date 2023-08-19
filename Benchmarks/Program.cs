using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Http;
using RinhaDeBackEnd;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Benchmarks {
    public class Program {
        static void Main(string[] args) {
            //var summary = BenchmarkRunner.Run<SearchEndpoint>();

            var summary1 = BenchmarkRunner.Run<Serialization>();
        }


        public class Serialization {
            private Pessoa pessoa;
            public Serialization() {
                pessoa = new Pessoa() {
                    Apelido = "joaozinho2321",
                    Id = Guid.NewGuid(),
                    Nascimento = DateOnly.Parse("2023-07-07"),
                    Nome = "Joao dos Santos",
                    Stack = new List<string>() { "C#", "Go", "Java", "Python", "Rust" }

                };
            }
            [Benchmark]
            public string Serialize() => JsonSerializer.Serialize(pessoa, typeof(Pessoa), PersonContext.Default);

            [Benchmark]
            public string SerializeSourceGeneratorDefault() => JsonSerializer.Serialize(pessoa, typeof(Pessoa), PersonContext.Default);

            [Benchmark]
            public string SerializeSourceGeneratorDefaultPessoa() => JsonSerializer.Serialize(pessoa, PersonContext.Default.Pessoa);
        }
        public class SearchEndpoint {
            private IResult respostaErroResult2;
            private ConcurrentDictionary<string, Pessoa> buscaMap;

            public SearchEndpoint() {
                respostaErroResult2 = Results.Text(ResponseBusca.RespostaErroString, contentType: "application/json; charset=utf-8", statusCode: 400);
                using (StreamReader r = new StreamReader("dataBenchmark.json")) {
                    string json = r.ReadToEnd();
                    List<Root> items = JsonSerializer.Deserialize<List<Root>>(json);
                    buscaMap = new ConcurrentDictionary<string, Pessoa>();
                    foreach (var item in items) {
                        var pessoa = new Pessoa() {
                            Id = Guid.NewGuid(),
                            Apelido = item.apelido,
                            Nascimento = DateOnly.TryParse(item.nascimento, out var nascimento) ? nascimento : null,
                            Nome = item.nome,
                            Stack = item.stack
                        };

                        buscaMap.TryAdd(pessoa.Apelido, pessoa);
                        buscaMap.TryAdd(pessoa.Nome, pessoa);
                    }
                }
            }

            [Benchmark]
            public IResult SearchByNameNicknameOrStackBenchmarkV1() => SearchByNameNicknameOrStackV1(buscaMap, "BkB");
            public IResult SearchByNameNicknameOrStackV1(ConcurrentDictionary<string, Pessoa> buscaMap, string? t) {
                if (string.IsNullOrEmpty(t)) {
                    return respostaErroResult2;
                }

                var pessoas = buscaMap.Where(p => p.Key.Contains(t))
                                        .Take(50)
                                        .Select(p => p.Value)
                                        .ToList();
                return Results.Json(new ResponseBusca { Resultados = pessoas }, ResponseBuscaContext.Default.ResponseBusca);
            }

            [Benchmark]
            public IResult SearchByNameNicknameOrStackBenchmarkV2() => SearchByNameNicknameOrStackV2(buscaMap, "BkB");
            public IResult SearchByNameNicknameOrStackV2(ConcurrentDictionary<string, Pessoa> buscaMap, string? t) {
                if (string.IsNullOrEmpty(t)) {
                    return respostaErroResult2;
                }

                var pessoas = buscaMap.Where(p => p.Key.Contains(t))
                                        .Take(50)
                                        .Select(p => p.Value)
                                        .AsEnumerable();
                return Results.Json(new ResponseBusca { Resultados = pessoas }, ResponseBuscaContext.Default.ResponseBusca);
            }
            [Benchmark]
            public IResult SearchByNameNicknameOrStackBenchmarkV3() => SearchByNameNicknameOrStackV3(buscaMap, "BkB");
            public IResult SearchByNameNicknameOrStackV3(ConcurrentDictionary<string, Pessoa> buscaMap, string? t) {
                if (string.IsNullOrEmpty(t)) {
                    return respostaErroResult2;
                }

                var pessoas = buscaMap.Where(p => p.Key.Contains(t))
                                        .Take(50)
                                        .Select(p => p.Value)
                                        .AsParallel();
                return Results.Json(new ResponseBusca { Resultados = pessoas }, ResponseBuscaContext.Default.ResponseBusca);
            }
        }

        public class Root {
            public string apelido { get; set; }
            public string nome { get; set; }
            public string nascimento { get; set; }
            public List<string> stack { get; set; }
        }
    }
}