# Imagem do BFF, a unica porta que o navegador enxerga.
#
# O contexto de build e a RAIZ DA SOLUCAO, nao esta pasta:
#   docker build -f MeusLancamentosDiarios.Integrator.Bff/Dockerfile -t <imagem> .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# NuGet.config primeiro: restore deterministico, so nuget.org, sem o feed privado.
COPY NuGet.config ./
COPY MeusLancamentosDiarios.Integrator.Common/MeusLancamentosDiarios.Integrator.Common.csproj \
     MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Messages/MeusLancamentosDiarios.Integrator.Messages.csproj \
     MeusLancamentosDiarios.Integrator.Messages/
COPY MeusLancamentosDiarios.Integrator.Bff/MeusLancamentosDiarios.Integrator.Bff.csproj \
     MeusLancamentosDiarios.Integrator.Bff/

# Camada de restore separada do codigo: mudar um .cs nao invalida o cache dos pacotes.
RUN dotnet restore MeusLancamentosDiarios.Integrator.Bff/MeusLancamentosDiarios.Integrator.Bff.csproj

# Common (enums, helpers, roles) e Messages (requests e responses entre BFF e WebApi).
COPY MeusLancamentosDiarios.Integrator.Common/ MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Messages/ MeusLancamentosDiarios.Integrator.Messages/
COPY MeusLancamentosDiarios.Integrator.Bff/ MeusLancamentosDiarios.Integrator.Bff/

RUN dotnet publish MeusLancamentosDiarios.Integrator.Bff/MeusLancamentosDiarios.Integrator.Bff.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario nao-root ja definido nas imagens oficiais.
USER $APP_UID

ENTRYPOINT ["dotnet", "MeusLancamentosDiarios.Integrator.Bff.dll"]
