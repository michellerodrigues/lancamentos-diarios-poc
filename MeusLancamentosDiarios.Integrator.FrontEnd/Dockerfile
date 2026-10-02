# Imagem do front: build de producao do Angular servido pelo nginx.
#
# O contexto de build e ESTA PASTA, nao a raiz da solucao:
#   docker build -t <imagem> MeusLancamentosDiarios.Integrator.FrontEnd
#
# O endereco do BFF esta em src/app/core/api.config.ts (http://localhost:5100/api).
# Quem chama o BFF e o navegador, nao o container, entao localhost ali e a maquina
# de quem abre a tela.

FROM node:22-alpine AS build
WORKDIR /src

# Camada de dependencias separada do codigo: mudar um .ts nao reinstala o node_modules.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/lancamentos-frontend/browser /usr/share/nginx/html

EXPOSE 80
