using MeusLancamentosDiarios.Integrator.Events.Contracts;

namespace MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;

public interface IPublicadorEventos
{
    /// <summary>Publica o evento e devolve o id atribuido pelo broker.</summary>
    Task<string> PublicarAsync(LancamentoRegistradoEvent evento, CancellationToken cancellationToken = default);
}
