# Identidade visual do front

O front usa o esquema de cores azul marinho, vermelho, magenta e azul claro.

Os tokens ficam no `:root` de [`styles.scss`](../MeusLancamentosDiarios.Integrator.FrontEnd/src/styles.scss),
com nome de função e não de cor do site: para trocar a identidade de novo, muda-se só ali.

## Paleta

| Token | Cor | No site | No app |
|---|---|---|---|
| `--azul` | `#1E5BC6` | cor da marca: blocos, botões, links | cabeçalho, botão principal, links, valor do saldo, foco dos campos |
| `--azul-escuro` | `#0E2565` | botões escuros (cookies) | hover do botão principal, texto do selo de role |
| `--azul-claro` | `#E1EFFF` | faixa do menu do topo | texto secundário sobre o azul, fundo do selo de role |
| `--magenta` | `#E6007E` | o botão "Peça já o seu" | o botão flutuante de novo lançamento: a ação principal |
| `--tinta` | `#263A79` | títulos | texto principal |
| `--cinza` | `#5A626B` | texto corrido | texto secundário |
| `--degrade` | `#1E5BC6` → `#4842B3` → `#9F1B9F` | fundo do destaque da home | o card de saldo projetado e os ícones do PWA |

Derivados, porque o site não tem o equivalente:

| Token | Cor | Por quê |
|---|---|---|
| `--fundo` | `#F2F7FF` | fundo das telas; o `#E1EFFF` em tela cheia fica saturado demais |
| `--campo` | `#F7FAFF` | fundo dos campos de formulário |
| `--borda` | `#CFDCF0` | bordas e divisores entre itens da lista |
| `--azul-desabilitado` | `#A9BFE6` | botão desabilitado |
| `--magenta-escuro` | `#C2006A` | hover do botão de novo lançamento |
| `--placeholder` | `#8A929B` | dica dentro dos campos |
| `--credito` | `#1A7FA3` | o ciano do site (`#31A7CD`) escurecido: o original dá 2,8:1 sobre branco, este passa de 4,5:1 |
| `--vermelho` | `#B3261E` | débito e erro. Fora da paleta: o magenta do site é de ação, não de alerta |

## Contraste (WCAG)

| Combinação | Razão | Uso |
|---|---|---|
| branco sobre `--azul` | 6,24:1 | cabeçalho, botão principal |
| `--azul-claro` sobre `--azul` | 5,34:1 | rótulos e usuário no cabeçalho |
| branco sobre `--magenta` | 4,50:1 | botão de novo lançamento (só ícone) |
| `--azul` sobre branco | 6,24:1 | valor do saldo, links |
| `--tinta` sobre branco | 10,69:1 | texto principal |
| `--cinza` sobre branco | 6,19:1 | texto secundário |
| `--credito` sobre branco | 4,56:1 | valores de crédito |

Tudo passa em AA para texto normal (4,5:1). O branco sobre magenta fica no limite, e por
isso o magenta só aparece em botão de ícone, nunca atrás de texto corrido.

## Tipografia

A fonte é a **Kumbh Sans**, sob a SIL Open Font License 1.1, que permite usar e
redistribuir. Ela substituiu a Azo Sans, que é paga e não pode ficar num repositório
público.

A escolha foi por medição. Cada letra e algarismo (a–z, A–Z, 0–9, ç, ã, é, õ, R$) foi
desenhado na Azo Sans e em 20 fontes livres do Google Fonts, nos pesos 400 e 700. Depois,
mediu-se quanto de cada desenho coincide com o da Azo. As cinco mais próximas:

| Fonte | Coincide com a Azo | Largura (400 / 700) | Altura do x |
|---|---|---|---|
| **Kumbh Sans** | 78,3% | 98% / 97% | 100% |
| Lato | 76,3% | 94% / 94% | 104% |
| Outfit | 75,9% | 95% / 96% | 98% |
| Figtree | 75,8% | 96% / 96% | 102% |
| Nunito Sans | 73,9% | 98% / 99% | 100% |

Como a largura e a altura do x são praticamente as da Azo, os textos ocupam o mesmo espaço,
e nenhum layout precisou mudar.

| Onde | Como carrega |
|---|---|
| Front | Pacote `@fontsource-variable/kumbh-sans`, ligado em `angular.json` > `styles`. Os arquivos vão no build, em `media/`, e o service worker os guarda: o PWA continua com a fonte offline. É uma fonte variável, então os pesos 400, 500, 600 e 700 vêm do mesmo arquivo. O token é `--fonte`, no `:root` de `styles.scss`. |
| Documento de arquitetura | Google Fonts, pesos 400, 500 e 700. Sem internet, cai na Nunito Sans ou na Segoe UI. |

## Ícones do PWA

Os ícones de `public/icons` e o `favicon.ico` substituem o logo padrão do Angular:
três barras de saldo subindo sobre o degradê, a última em magenta. O desenho fica dentro
do círculo de 40% do raio, a zona segura dos ícones `maskable`, então nenhum recorte do
Android corta as barras. O `theme-color` do `index.html` e o `theme_color` do manifest
passaram a `#1E5BC6`; o `background_color` (a tela de abertura do app instalado), a `#F2F7FF`.
