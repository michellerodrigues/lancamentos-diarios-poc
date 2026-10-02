# Imagem do relay para rodar como Cloud Run Job.
#
# O contexto de build e a RAIZ DA SOLUCAO, nao esta pasta:
#   docker build -f MeusLancamentosDiarios.Integrator.Events/Dockerfile -t <imagem> .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# NuGet.config primeiro: restore deterministico, so nuget.org, sem o feed privado.
COPY NuGet.config ./
COPY MeusLancamentosDiarios.Integrator.Common/MeusLancamentosDiarios.Integrator.Common.csproj \
     MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Events/MeusLancamentosDiarios.Integrator.Events.csproj \
     MeusLancamentosDiarios.Integrator.Events/

# Camada de restore separada do codigo: mudar um .cs nao invalida o cache dos pacotes.
RUN dotnet restore MeusLancamentosDiarios.Integrator.Events/MeusLancamentosDiarios.Integrator.Events.csproj

# Common: enums e helpers compartilhados.
COPY MeusLancamentosDiarios.Integrator.Common/ MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Events/ MeusLancamentosDiarios.Integrator.Events/

RUN dotnet publish MeusLancamentosDiarios.Integrator.Events/MeusLancamentosDiarios.Integrator.Events.csproj \
    -c Release -o /app --no-restore

# runtime, nao aspnet: o relay nao serve HTTP.
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# Um ciclo e encerra — e o que o Cloud Run Job espera.
ENV Relay__LoopContinuo=false

# Topico e subscription vem do Terraform, nao da subida da aplicacao.
ENV PubSub__CriarRecursos=false

ENV DOTNET_ENVIRONMENT=Production

# Usuario nao-root ja definido nas imagens oficiais.
USER $APP_UID

ENTRYPOINT ["dotnet", "MeusLancamentosDiarios.Integrator.Events.dll"]
