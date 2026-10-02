using System.Text.Json.Serialization;
using MeusLancamentosDiarios.Integrator.Events;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs;
using MeusLancamentosDiarios.Integrator.WebApi.Endpoints;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Saldo;

var builder = WebApplication.CreateBuilder(args);

// Acesso a tabela LancamentosDiarios, compartilhado com o Events e o Consumer.
builder.Services.AddLancamentosData(builder.Configuration);

// Publicacao imediata apos o commit: tira a espera do caminho feliz. O relay do
// projeto Events continua existindo como varredura, para o que falhar aqui.
builder.Services
    .AddPubSub(builder.Configuration)
    .AddRelay(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

// A WebApi emite o JWT e valida o mesmo token que o BFF repassa.
builder.Services.AddAutenticacao(builder.Configuration);

// Quantos pendentes o saldo detalha por padrao. Vem do appsettings; sem ele a API nem sobe.
builder.Services.AddOptions<SaldoOptions>()
    .Bind(builder.Configuration.GetSection(SaldoOptions.Secao))
    .Validate(o => o.Valida, "Saldo:LimitePendentesPadrao deve ser maior que zero.")
    .ValidateOnStart();

// Registra handlers e validators por varredura do assembly.
builder.Services.AddCqrs(typeof(Program).Assembly);

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ProblemaExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(opcoes =>
{
    opcoes.AddDocumentTransformer<SegurancaBearerNoOpenApi>();
    opcoes.AddOperationTransformer<SegurancaBearerNoOpenApi>();
});

builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    // Enums como texto: "Credito" no lugar de 1, para o contrato ficar legivel.
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Sem migrations do EF: o esquema e garantido na subida, de forma idempotente.
await app.Services.GetRequiredService<DatabaseBootstrapper>().GarantirEsquemaAsync();
await app.Services.GetRequiredService<AuthBootstrapper>().GarantirAsync();

app.UseExceptionHandler();

// Contrato em /openapi/v1.json e a UI em /swagger. A imagem roda como Production,
// entao fora de Development so com Swagger:Habilitado: o compose liga, o Cloud Run nao.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(opcoes =>
    {
        opcoes.SwaggerEndpoint("/openapi/v1.json", "WebApi v1");

        // O token colado no "Authorize" sobrevive ao F5.
        opcoes.EnablePersistAuthorization();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapLancamentos();
app.MapSaldo();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();

app.Run();
