using MeusLancamentosDiarios.Integrator.Consumer;
using MeusLancamentosDiarios.Integrator.Events;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLancamentosData(builder.Configuration)
    .AddPubSub(builder.Configuration);

builder.Services.AddOptions<ConsumerOptions>()
    .Bind(builder.Configuration.GetSection(ConsumerOptions.Secao))
    .Validate(o => o.Valida, "Consumer precisa de MaxTentativas e Concorrencia maiores que zero.")
    .ValidateOnStart();

builder.Services.AddSingleton<ConsolidadorDeMensagem>();
builder.Services.AddHostedService<ConsolidacaoWorker>();

var host = builder.Build();
await host.RunAsync();
