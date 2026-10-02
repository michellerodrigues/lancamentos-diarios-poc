namespace MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;

/// <summary>
/// De-para entre o que a WebApi devolve e o que a tela consome. Puro: sem I/O, sem
/// estado, testavel sem subir nada.
/// </summary>
public interface IConversor<in TOrigem, out TDestino>
{
    TDestino Converter(TOrigem origem);
}
