using MeusLancamentosDiarios.Integrator.Events;
using MeusLancamentosDiarios.Integrator.Events.Publishing;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLancamentosData(builder.Configuration)
    .AddPubSub(builder.Configuration)
    .AddRelay(builder.Configuration);

builder.Services.AddHostedService<RelayWorker>();

var host = builder.Build();
await host.RunAsync();
