using System.Text.Json;
using System.Text.Json.Serialization;
using MeusLancamentosDiarios.Integrator.Bff.Auth;
using MeusLancamentosDiarios.Integrator.Bff.Clients;
using MeusLancamentosDiarios.Integrator.Bff.Clients.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Endpoints;
using MeusLancamentosDiarios.Integrator.Bff.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

const string PoliticaCors = "front-angular";

// Endereco e timeout da WebApi vem so do appsettings (ou do ambiente). Sem eles
// a aplicacao nem sobe, em vez de apontar para um padrao escondido no codigo.
builder.Services.AddOptions<WebApiOptions>()
    .Bind(builder.Configuration.GetSection(WebApiOptions.Secao))
    .Validate(o => o.Valida, "WebApi:BaseUrl precisa ser uma URL absoluta e WebApi:Timeout, maior que zero.")
    .ValidateOnStart();

// Valida o JWT que a WebApi emitiu e aplica as mesmas roles.
builder.Services.AddAutenticacao(builder.Configuration);

// Opcoes de JSON compartilhadas pelo cliente HTTP e pela serializacao das respostas.
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

builder.Services.AddHttpClient<ILancamentosApiClient, LancamentosApiClient>((provider, http) =>
    {
        var opcoes = provider.GetRequiredService<IOptions<WebApiOptions>>().Value;

        http.BaseAddress = new Uri(opcoes.BaseUrl);
        http.Timeout = opcoes.Timeout;
    })
    // O Bearer do front segue junto: a WebApi decide com o mesmo token.
    .AddHttpMessageHandler<RepassarTokenHandler>();

// Endpoint -> service -> cliente da WebApi; o de-para fica nos conversores.
builder.Services.AddTelaServices();

builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opcoes.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// O BFF e a unica porta que o navegador enxerga, entao o CORS vive aqui.
builder.Services.AddCors(opcoes => opcoes.AddPolicy(PoliticaCors, politica => politica
    .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(opcoes =>
{
    opcoes.AddDocumentTransformer<SegurancaBearerNoOpenApi>();
    opcoes.AddOperationTransformer<SegurancaBearerNoOpenApi>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors(PoliticaCors);

// Contrato em /openapi/v1.json e a UI em /swagger. A imagem roda como Production,
// entao fora de Development so com Swagger:Habilitado: o compose liga, o Cloud Run nao.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(opcoes =>
    {
        opcoes.SwaggerEndpoint("/openapi/v1.json", "BFF v1");
        opcoes.EnablePersistAuthorization();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapBff();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();

app.Run();
