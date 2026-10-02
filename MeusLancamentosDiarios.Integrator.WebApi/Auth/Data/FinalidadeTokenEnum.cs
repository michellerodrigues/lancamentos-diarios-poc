namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

/// <summary>Para que serve um token opaco guardado em TokensDeUsuario.</summary>
public enum FinalidadeTokenEnum : byte
{
    /// <summary>Troca por um novo par de tokens. Revogado no logout.</summary>
    Renovacao = 1,

    /// <summary>Vai no link do e-mail de "esqueci a senha". Uso unico.</summary>
    RedefinicaoSenha = 2
}
