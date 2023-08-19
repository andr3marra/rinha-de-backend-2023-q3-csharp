using System.Text.Json.Serialization;

namespace RinhaDeBackEnd;

public class Pessoa {
    public Guid? Id { get; set; }

    public string? Apelido { get; set; }

    public string? Nome { get; set; }

    public DateOnly? Nascimento { get; set; }

    public IEnumerable<string>? Stack { get; set; }

    internal static bool BasicamenteValida(Pessoa pessoa) {
        var atributosInvalidos = !pessoa.Nascimento.HasValue
                                || string.IsNullOrEmpty(pessoa.Nome)
                                || pessoa.Nome.Length > 100
                                || string.IsNullOrEmpty(pessoa.Apelido)
                                || pessoa.Apelido.Length > 32;

        if (atributosInvalidos)
            return false;

        foreach (var item in pessoa.Stack ?? Enumerable.Empty<string>())
            if (item.Length > 32 || item.Length == 0)
                return false;

        return true;
    }
}

public abstract class Response {
    public string? Erro { get; set; }
}

public class ResponseBusca
    : Response {
    public const string RespostaErroString = "{\"Resultados\":[],\"Erro\":\"\\u0027t\\u0027 n\\uFFFDo informado\"}";
    public IEnumerable<Pessoa> Resultados { get; set; } = new List<Pessoa>();
}

public class ResponseCriacao
    : Response {
    public const string ResponseAfeString = "{\"Pessoa\":null,\"Erro\":\"afe...\"}";
    public const string DuplicatedResultString = "{\"Pessoa\":null,\"Erro\":\"esse apelido j\\uFFFD existe\"}";
    public Pessoa? Pessoa { get; set; }
}

public class ResponseConsulta
    : Response {
    public const string RespostaErroString = "{\"pessoa\":null,\"erro\":\"Oops\"}";
    public object? Pessoa { get; set; }
}

[JsonSerializable(typeof(Pessoa))]
public partial class PersonContext : JsonSerializerContext { }
[JsonSerializable(typeof(ResponseBusca))]
public partial class ResponseBuscaContext : JsonSerializerContext { }
[JsonSerializable(typeof(ResponseCriacao))]
public partial class ResponseCriacaoContext : JsonSerializerContext { }
[JsonSerializable(typeof(ResponseConsulta))]
public partial class ResponseConsultaContext : JsonSerializerContext { }