# Imagem do Consumer, que assina a subscription e consolida o saldo.
#
# O contexto de build e a RAIZ DA SOLUCAO, nao esta pasta:
#   docker build -f MeusLancamentosDiarios.Integrator.Consumer/Dockerfile -t <imagem> .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# NuGet.config primeiro: restore deterministico, so nuget.org, sem o feed privado.
COPY NuGet.config ./
COPY MeusLancamentosDiarios.Integrator.Common/MeusLancamentosDiarios.Integrator.Common.csproj \
     MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Events/MeusLancamentosDiarios.Integrator.Events.csproj \
     MeusLancamentosDiarios.Integrator.Events/
COPY MeusLancamentosDiarios.Integrator.Consumer/MeusLancamentosDiarios.Integrator.Consumer.csproj \
     MeusLancamentosDiarios.Integrator.Consumer/

# Camada de restore separada do codigo: mudar um .cs nao invalida o cache dos pacotes.
RUN dotnet restore MeusLancamentosDiarios.Integrator.Consumer/MeusLancamentosDiarios.Integrator.Consumer.csproj

# O .Events entra junto: e ele que carrega o repositorio e os contratos de evento.
# Common: enums e helpers compartilhados.
COPY MeusLancamentosDiarios.Integrator.Common/ MeusLancamentosDiarios.Integrator.Common/
COPY MeusLancamentosDiarios.Integrator.Events/ MeusLancamentosDiarios.Integrator.Events/
COPY MeusLancamentosDiarios.Integrator.Consumer/ MeusLancamentosDiarios.Integrator.Consumer/

RUN dotnet publish MeusLancamentosDiarios.Integrator.Consumer/MeusLancamentosDiarios.Integrator.Consumer.csproj \
    -c Release -o /app --no-restore

# runtime, nao aspnet: com pull subscription o Consumer nao serve HTTP. Se virar
# push subscription, passa a ser endpoint e troca para a imagem aspnet.
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# Topico e subscription vem do Terraform, nao da subida da aplicacao.
ENV PubSub__CriarRecursos=false

ENV DOTNET_ENVIRONMENT=Production

# Usuario nao-root ja definido nas imagens oficiais.
USER $APP_UID

ENTRYPOINT ["dotnet", "MeusLancamentosDiarios.Integrator.Consumer.dll"]
