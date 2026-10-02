using FluentAssertions;
using FluentValidation;
using MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Cqrs;

public sealed class DispatcherTests
{
    // Comando, handler e validator de mentira, só para exercitar o despacho.

    public sealed record ComandoDeTeste(string Valor) : ICommand<string>;

    public sealed record ConsultaDeTeste(int Numero) : IQuery<int>;

    public sealed class HandlerDoComando : ICommandHandler<ComandoDeTeste, string>
    {
        public bool Executou { get; private set; }

        public Task<string> HandleAsync(ComandoDeTeste comando, CancellationToken cancellationToken)
        {
            Executou = true;
            return Task.FromResult($"tratado: {comando.Valor}");
        }
    }

    public sealed class HandlerDaConsulta : IQueryHandler<ConsultaDeTeste, int>
    {
        public Task<int> HandleAsync(ConsultaDeTeste consulta, CancellationToken cancellationToken) =>
            Task.FromResult(consulta.Numero * 2);
    }

    public sealed class HandlerQueExplode : ICommandHandler<ComandoDeTeste, string>
    {
        public Task<string> HandleAsync(ComandoDeTeste comando, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("falha de dominio");
    }

    public sealed class ValidatorDoComando : AbstractValidator<ComandoDeTeste>
    {
        public ValidatorDoComando() =>
            RuleFor(x => x.Valor).NotEmpty().WithMessage("Valor obrigatorio.");
    }

    private static Dispatcher Montar(Action<IServiceCollection> registrar)
    {
        var servicos = new ServiceCollection();
        registrar(servicos);

        return new Dispatcher(servicos.BuildServiceProvider());
    }

    public sealed class QuandoDespachaUmComandoValido
    {
        [Fact]
        public async Task Entao_chama_o_handler_registrado()
        {
            // Case
            var handler = new HandlerDoComando();

            var dispatcher = Montar(s =>
            {
                s.AddSingleton<ICommandHandler<ComandoDeTeste, string>>(handler);
                s.AddSingleton<IValidator<ComandoDeTeste>, ValidatorDoComando>();
            });

            // When
            var resultado = await dispatcher.EnviarAsync(new ComandoDeTeste("ok"));

            // Then
            resultado.Should().Be("tratado: ok");
            handler.Executou.Should().BeTrue();
        }
    }

    public sealed class QuandoOComandoNaoPassaNaValidacao
    {
        [Fact]
        public async Task Entao_lanca_antes_de_chegar_ao_handler()
        {
            // Case
            var handler = new HandlerDoComando();

            var dispatcher = Montar(s =>
            {
                s.AddSingleton<ICommandHandler<ComandoDeTeste, string>>(handler);
                s.AddSingleton<IValidator<ComandoDeTeste>, ValidatorDoComando>();
            });

            // When
            var acao = () => dispatcher.EnviarAsync(new ComandoDeTeste(""));

            // Then — a validação roda no dispatcher, não em cada handler; isso
            // impede que uma feature nova esqueça de validar.
            await acao.Should().ThrowAsync<ValidationException>();
            handler.Executou.Should().BeFalse();
        }
    }

    public sealed class QuandoNaoExisteValidatorParaOComando
    {
        [Fact]
        public async Task Entao_despacha_normalmente()
        {
            // Case — nem toda mensagem precisa de validator.
            var dispatcher = Montar(s =>
                s.AddSingleton<ICommandHandler<ComandoDeTeste, string>, HandlerDoComando>());

            // When
            var resultado = await dispatcher.EnviarAsync(new ComandoDeTeste("sem validator"));

            // Then
            resultado.Should().Be("tratado: sem validator");
        }
    }

    public sealed class QuandoNaoHaHandlerRegistrado
    {
        [Fact]
        public async Task Entao_avisa_qual_mensagem_ficou_sem_dono()
        {
            // Case
            var dispatcher = Montar(_ => { });

            // When
            var acao = () => dispatcher.EnviarAsync(new ComandoDeTeste("x"));

            // Then
            var erro = await acao.Should().ThrowAsync<InvalidOperationException>();
            erro.WithMessage("*ComandoDeTeste*");
        }
    }

    public sealed class QuandoOHandlerLancaExcecao
    {
        [Fact]
        public async Task Entao_a_excecao_original_sobe_sem_embrulho()
        {
            // Case
            var dispatcher = Montar(s =>
                s.AddSingleton<ICommandHandler<ComandoDeTeste, string>, HandlerQueExplode>());

            // When
            var acao = () => dispatcher.EnviarAsync(new ComandoDeTeste("x"));

            // Then — o despacho é por reflexão, então sem o desembrulho chegaria
            // uma TargetInvocationException e o middleware de erro não saberia
            // classificar o status da resposta.
            var erro = await acao.Should().ThrowAsync<InvalidOperationException>();
            erro.WithMessage("falha de dominio");
        }
    }

    public sealed class QuandoDespachaUmaConsulta
    {
        [Fact]
        public async Task Entao_usa_o_handler_de_consulta()
        {
            // Case
            var dispatcher = Montar(s =>
                s.AddSingleton<IQueryHandler<ConsultaDeTeste, int>, HandlerDaConsulta>());

            // When
            var resultado = await dispatcher.ConsultarAsync(new ConsultaDeTeste(21));

            // Then
            resultado.Should().Be(42);
        }

        [Fact]
        public async Task Entao_nao_confunde_com_o_handler_de_comando()
        {
            // Case — comando e consulta com o mesmo tipo de retorno não podem
            // se cruzar: o dispatcher resolve pela interface aberta correta.
            var dispatcher = Montar(s =>
            {
                s.AddSingleton<IQueryHandler<ConsultaDeTeste, int>, HandlerDaConsulta>();
                s.AddSingleton<ICommandHandler<ComandoDeTeste, string>, HandlerDoComando>();
            });

            // When
            var consulta = await dispatcher.ConsultarAsync(new ConsultaDeTeste(5));
            var comando = await dispatcher.EnviarAsync(new ComandoDeTeste("a"));

            // Then
            consulta.Should().Be(10);
            comando.Should().Be("tratado: a");
        }
    }
}
