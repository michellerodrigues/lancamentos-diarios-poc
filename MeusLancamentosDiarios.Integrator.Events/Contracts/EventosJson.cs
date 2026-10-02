using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeusLancamentosDiarios.Integrator.Events.Contracts;

/// <summary>
/// Formato do corpo das mensagens. Publicador e consumidor compartilham estas
/// opcoes para que o contrato nao dependa do default de cada processo.
/// </summary>
public static class EventosJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
