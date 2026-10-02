# Imagem da WebApi de dominio.
#
# O contexto de build e a RAIZ DA SOLUCAO, nao esta pasta:
#   docker build -f MeusLancamentosDiarios.Integrator.WebApi/Dockerfile -t <imagem> .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# NuGet.config primeiro: restore deterministico, so nuget.org, sem o feed privado.
COPY NuGet.config ./
COPY MeusLancamentosDiarios.Integrator.Common/MeusLancamentosDiarios.Integrator.Common.csproj \
     MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Messages/MeusLancamentosDiarios.Integrator.Messages.csproj \
     MeusLancamentosDiarios.Integrator.Messages/
COPY MeusLancamentosDiarios.Integrator.Events/MeusLancamentosDiarios.Integrator.Events.csproj \
     MeusLancamentosDiarios.Integrator.Events/
COPY MeusLancamentosDiarios.Integrator.WebApi/MeusLancamentosDiarios.Integrator.WebApi.csproj \
     MeusLancamentosDiarios.Integrator.WebApi/

# Camada de restore separada do codigo: mudar um .cs nao invalida o cache dos pacotes.
RUN dotnet restore MeusLancamentosDiarios.Integrator.WebApi/MeusLancamentosDiarios.Integrator.WebApi.csproj

# O .Events entra junto: e ele que carrega o repositorio e o publicador.
# Common (enums, helpers, roles) e Messages (requests e responses entre BFF e WebApi).
COPY MeusLancamentosDiarios.Integrator.Common/ MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Messages/ MeusLancamentosDiarios.Integrator.Messages/
COPY MeusLancamentosDiarios.Integrator.Events/ MeusLancamentosDiarios.Integrator.Events/
COPY MeusLancamentosDiarios.Integrator.WebApi/ MeusLancamentosDiarios.Integrator.WebApi/

RUN dotnet publish MeusLancamentosDiarios.Integrator.WebApi/MeusLancamentosDiarios.Integrator.WebApi.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# A imagem oficial ja escuta em 8080 (ASPNETCORE_HTTP_PORTS), que e tambem a porta
# que o Cloud Run entrega por padrao.
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario nao-root ja definido nas imagens oficiais.
USER $APP_UID

ENTRYPOINT ["dotnet", "MeusLancamentosDiarios.Integrator.WebApi.dll"]
