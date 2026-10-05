<#
.SYNOPSIS
    Deixa o banco pronto para uma demonstracao: uma semana de movimento em tres
    contas, ja consolidada.

.DESCRIPTION
    Passa pelo caminho da aplicacao, sem escrever direto no banco: login do admin na
    WebApi e POST /lancamentos/lote. O relay, o Pub/Sub e o Consumer consolidam; o
    script espera terminar e confere o saldo de cada conta contra a soma esperada.

    Os usuarios de demonstracao ja existem, porque a WebApi os cria na subida
    (Auth:SemearUsuariosDemo). As datas vao de seis dias atras ate hoje, no fuso de
    Sao Paulo, porque a WebApi recusa data futura nesse fuso. A tela mostra o saldo
    de cada conta, nao a lista de lancamentos.

    Rodar de novo e seguro. Se o movimento de demonstracao ja estiver no banco, o
    script so espera e confere. Se as contas tiverem outro movimento, ele para sem
    lancar nada.

.PARAMETER Zerar
    Apaga o banco e sobe a stack de novo antes de lancar (docker compose down -v).
    Leva tudo: as contas e os cadastros feitos pela tela, e os e-mails do Mailpit.
    Pede confirmacao, e vale so para a stack do docker compose.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\inicializar-banco-demo.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\inicializar-banco-demo.ps1 -Zerar
#>
[CmdletBinding()]
param(
    [switch] $Zerar,

    [string] $WebApi = 'http://localhost:5101',

    [string] $Email = 'admin@lancamentos.local',

    [string] $Senha = 'Demo@2026'
)

$ErrorActionPreference = 'Stop'

# Uma semana de movimento. "dias" conta para tras a partir de hoje (0 = hoje). Dentro
# de cada conta, a ordem das linhas e a ordem em que o relay publica.
$movimento = @'
conta,dias,tipo,valor,observacao
ABC1234,6,Credito,1850.00,Vendas do dia em dinheiro
ABC1234,6,Debito,620.00,Fornecedor de farinha
ABC1234,5,Credito,2130.50,Vendas no cartão
ABC1234,5,Debito,89.90,Tarifa da maquininha
ABC1234,4,Credito,1975.25,Vendas do dia
ABC1234,4,Debito,1200.00,Aluguel da loja
ABC1234,3,Credito,2310.00,Vendas no Pix
ABC1234,3,Debito,342.17,Conta de energia
ABC1234,2,Credito,1640.80,Vendas do dia
ABC1234,2,Debito,450.00,Manutenção do forno
ABC1234,1,Credito,2480.00,Encomenda de bolos para festa
ABC1234,1,Debito,1500.00,Adiantamento de salários
ABC1234,0,Credito,980.40,Vendas da manhã
XYZ9999,6,Credito,3200.00,Revisão completa
XYZ9999,6,Debito,1150.00,Compra de peças
XYZ9999,5,Credito,780.00,Troca de óleo e filtros
XYZ9999,4,Debito,2300.00,Folha de pagamento
XYZ9999,3,Credito,1450.00,Alinhamento e balanceamento
XYZ9999,3,Debito,215.60,Conta de água
XYZ9999,2,Credito,2050.00,Serviço de suspensão
XYZ9999,1,Debito,640.00,Ferramentas novas
XYZ9999,0,Debito,35.90,Tarifa bancária
ADM0001,6,Credito,10000.00,Aporte inicial
ADM0001,2,Debito,1250.00,Licenças de software
'@ | ConvertFrom-Csv

$contas = @($movimento | Select-Object -ExpandProperty conta -Unique)
$brasil = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')

# Quantos lancamentos cada conta recebe e o saldo dela quando tudo consolida.
$esperado = @{}
foreach ($conta in $contas) {
    $dela = @($movimento | Where-Object { $_.conta -eq $conta })
    $saldo = [decimal]0
    foreach ($linha in $dela) {
        if ($linha.tipo -eq 'Credito') { $saldo += [decimal]$linha.valor } else { $saldo -= [decimal]$linha.valor }
    }
    $esperado[$conta] = @{ Quantidade = [long]$dela.Count; Saldo = $saldo }
}

function Chamar {
    param([string] $Metodo, [string] $Rota, $Corpo, [string] $Token)

    $pedido = @{ Method = $Metodo; Uri = "$WebApi$Rota"; TimeoutSec = 30 }
    if ($Token) { $pedido.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Corpo) {
        # Bytes em UTF-8: com o corpo em texto, o Windows PowerShell 5.1 manda
        # ISO-8859-1, e as observacoes com acento chegariam quebradas.
        $pedido.ContentType = 'application/json; charset=utf-8'
        $pedido.Body = [Text.Encoding]::UTF8.GetBytes(($Corpo | ConvertTo-Json -Depth 5))
    }

    try {
        Invoke-RestMethod @pedido
    }
    catch {
        $resposta = $_.Exception.Response
        $status = if ($resposta) { [int]$resposta.StatusCode } else { 'sem resposta' }
        $detalhe = $_.ErrorDetails.Message
        # O Windows PowerShell 5.1 so preenche ErrorDetails quando a resposta traz
        # Content-Length, e a WebApi responde em chunked: le o corpo direto.
        if (-not $detalhe -and $resposta -is [System.Net.WebResponse]) {
            $leitor = New-Object System.IO.StreamReader($resposta.GetResponseStream(), [Text.Encoding]::UTF8)
            try { $detalhe = $leitor.ReadToEnd() } finally { $leitor.Dispose() }
        }
        throw "$($Metodo.ToUpper()) $Rota falhou ($status). $detalhe"
    }
}

function Entrar {
    (Chamar Post '/auth/login' @{ email = $Email; senha = $Senha }).tokenAcesso
}

function HojeEmSaoPaulo {
    # A WebApi recusa data futura no fuso de Sao Paulo, nao no da maquina.
    foreach ($fuso in 'E. South America Standard Time', 'America/Sao_Paulo') {
        try { return [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, $fuso).Date } catch { }
    }
    (Get-Date).Date
}

function Compose {
    # O docker escreve o progresso no stderr. Com 'Stop', o Windows PowerShell 5.1
    # trataria cada linha como erro e abortaria; quem decide e o codigo de saida.
    $ErrorActionPreference = 'Continue'
    & docker compose @args 2>&1 | ForEach-Object { "    $_" }
    if ($LASTEXITCODE -ne 0) { throw "docker compose $args falhou." }
}

function ConfirmarZerar {
    # Melhor esforco: com a WebApi no ar, diz quais contas fora da demonstracao somem
    # junto. Sao as que foram cadastradas pela tela.
    $fora = @()
    try {
        $null = Invoke-RestMethod "$WebApi/health" -TimeoutSec 5
        $todas = Chamar Get '/contas' -Token (Entrar)
        $fora = @($todas | Where-Object { $_ -and $contas -notcontains $_ })
    }
    catch { }

    $alem = ''
    if ($fora.Count -gt 0) {
        $alem = ', inclusive as contas ' + ($fora -join ', ') + ', que nao sao da demonstracao'
    }
    $aviso = "O docker compose down -v apaga o banco inteiro$alem, os cadastros feitos pela tela " +
        'e os e-mails do Mailpit. Continuar?'
    $opcoes = [Management.Automation.Host.ChoiceDescription[]]@('&Sim', '&Nao')
    try {
        # O padrao e Nao: um Enter sem querer nao apaga nada.
        $escolha = $Host.UI.PromptForChoice('Zerar o banco', $aviso, $opcoes, 1)
    }
    catch {
        throw 'Nao deu para pedir confirmacao nesta sessao. Nada foi apagado.'
    }
    $escolha -eq 0
}

function EsperarWebApi {
    $inicio = Get-Date
    $avisou = $false
    while ($true) {
        try {
            $null = Invoke-RestMethod "$WebApi/health" -TimeoutSec 5
            return
        }
        catch {
            $passou = ((Get-Date) - $inicio).TotalSeconds
            if ($passou -ge 120) {
                throw ("A WebApi nao respondeu em $WebApi/health. Suba a stack (docker compose up -d) " +
                    'ou so a WebApi (dotnet run --project MeusLancamentosDiarios.Integrator.WebApi).')
            }
            if (-not $avisou -and $passou -ge 10) {
                Write-Host '    ainda sem resposta; espero ate 2 minutos'
                $avisou = $true
            }
            Start-Sleep -Seconds 2
        }
    }
}

function TemMovimentoDaDemo([string] $Conta, [string] $Token) {
    # A sequencia comeca em 1 em cada conta, e cada lancamento ou ja consolidou (ate
    # ultimaSequenciaConsolidada) ou esta pendente. Se a quantidade e o saldo projetado
    # batem com o esperado, o movimento da conta e o da demonstracao.
    $saldo = Chamar Get "/saldo/$($Conta)?limitePendentes=1000" -Token $Token
    $pendentes = @($saldo.pendentes | Where-Object { $_ })
    (([long]$saldo.ultimaSequenciaConsolidada + $pendentes.Count) -eq $esperado[$Conta].Quantidade) -and
        ([decimal]$saldo.saldoProjetado -eq $esperado[$Conta].Saldo)
}

function MostrarNaoConsolidados($Itens, [string] $Token) {
    if (@($Itens).Count -eq 0) { return }
    Write-Host '    lancamentos que nao consolidaram:'
    foreach ($item in $Itens) {
        $detalhe = Chamar Get "/lancamentos/$($item.id)" -Token $Token
        if ($detalhe.stage -ne 'Consolidado') {
            Write-Host ('      {0} #{1}  {2}  {3}' -f $detalhe.contaId, $detalhe.sequencia, $detalhe.stage, $detalhe.erro)
        }
    }
}

try {
    if ($Zerar) {
        # So um $true exato apaga: qualquer outra resposta cancela.
        if ((ConfirmarZerar) -ne $true) {
            Write-Host 'Cancelado. Nada foi apagado.'
            exit 1
        }
        Write-Host '==> zerando o banco: docker compose down -v e up de novo'
        Push-Location (Split-Path -Parent $PSScriptRoot)
        try {
            Compose down -v
            Compose up -d --wait
        }
        finally {
            Pop-Location
        }
    }

    Write-Host "==> 1/4  esperando a WebApi em $WebApi"
    EsperarWebApi

    Write-Host "==> 2/4  login como $Email"
    $token = Entrar

    # /contas lista quem ja recebeu algum lancamento, consolidado ou nao. O lote entra
    # inteiro ou nao entra, entao as tres contas tem o movimento todo ou nenhum.
    $existentes = Chamar Get '/contas' -Token $token
    $comMovimento = @($contas | Where-Object { $existentes -contains $_ })
    $jaLancado = $false
    if ($comMovimento.Count -gt 0) {
        $daDemo = @($comMovimento | Where-Object { TemMovimentoDaDemo $_ $token })
        if ($daDemo.Count -ne $contas.Count) {
            throw ('O banco ja tem lancamentos nas contas {0}, e eles nao sao o movimento da demonstracao. ' +
                'O script nao lanca por cima. Para recomecar do zero, rode com -Zerar: ele apaga o banco ' +
                'inteiro, inclusive os cadastros feitos pela tela.') -f ($comMovimento -join ', ')
        }
        $jaLancado = $true
    }

    # Ultima sequencia a consolidar em cada conta. A sequencia comeca em 1 por conta.
    $alvo = @{}
    foreach ($conta in $contas) { $alvo[$conta] = $esperado[$conta].Quantidade }
    $itens = @()

    if ($jaLancado) {
        Write-Host '==> 3/4  o movimento de demonstracao ja esta no banco; nada a lancar'
    }
    else {
        Write-Host "==> 3/4  lancando $($movimento.Count) lancamentos em $($contas.Count) contas"
        $hoje = HojeEmSaoPaulo
        $corpo = @(foreach ($linha in $movimento) {
            [ordered]@{
                contaId        = $linha.conta
                tipo           = $linha.tipo
                valor          = [decimal]$linha.valor
                dataLancamento = $hoje.AddDays(-([int]$linha.dias)).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                observacao     = $linha.observacao
            }
        })
        $lote = Chamar Post '/lancamentos/lote' @{ itens = $corpo } $token
        $itens = @($lote.lancamentos)
    }

    Write-Host '==> 4/4  esperando a consolidacao: o relay publica um lancamento por conta a cada 2 s'
    # O id de cada sequencia deste lote, para achar quem travou a fila.
    $porSequencia = @{}
    foreach ($item in $itens) { $porSequencia["$($item.contaId)#$($item.sequencia)"] = $item.id }

    $inicio = Get-Date
    $ultimaMudanca = $inicio
    $avisou = $false
    $progressoAnterior = ''
    while ($true) {
        $faltam = 0
        $andamento = foreach ($conta in $contas) {
            $saldo = Chamar Get "/saldo/$conta" -Token $token
            $feitos = [Math]::Min([long]$saldo.ultimaSequenciaConsolidada, $alvo[$conta])
            $faltam += $alvo[$conta] - $feitos
            # Erro na publicacao segura os seguintes da conta, e e sempre o proximo da
            # fila. Erro no Consumer nao segura: os seguintes consolidam por cima, e
            # quem pega e a conferencia do saldo no fim.
            $id = $porSequencia["$conta#$($feitos + 1)"]
            if ($feitos -lt $alvo[$conta] -and $id) {
                $proximo = Chamar Get "/lancamentos/$id" -Token $token
                if ($proximo.stage -eq 'Erro') {
                    throw ('O lancamento #{0} da conta {1} parou em Erro ({2}). A conta fica travada ate ' +
                        'alguem intervir.') -f $proximo.sequencia, $conta, $proximo.erro
                }
            }
            '{0} {1}/{2}' -f $conta, $feitos, $alvo[$conta]
        }

        $progresso = '    ' + ($andamento -join '   ')
        if ($progresso -ne $progressoAnterior) {
            Write-Host $progresso
            $progressoAnterior = $progresso
            $ultimaMudanca = Get-Date
        }
        elseif (-not $avisou -and ((Get-Date) - $ultimaMudanca).TotalSeconds -ge 20) {
            Write-Host '    nada andou em 20 s: o Events e o Consumer estao no ar? (docker compose ps, ou os consoles do dotnet run)'
            $avisou = $true
        }

        if ($faltam -eq 0) { break }
        if (((Get-Date) - $inicio).TotalMinutes -ge 3) {
            MostrarNaoConsolidados $itens $token
            throw ('A consolidacao nao terminou em 3 minutos. Os lancamentos ja estao gravados e consolidam ' +
                'sozinhos quando o Events e o Consumer voltarem. Depois, rode o script de novo: ele so confere.')
        }
        Start-Sleep -Seconds 2
    }

    Write-Host ''
    Write-Host 'Saldo consolidado de cada conta:'
    $divergentes = 0
    foreach ($conta in $contas) {
        $saldo = Chamar Get "/saldo/$conta" -Token $token
        $obtido = [decimal]$saldo.saldoConsolidado
        $nota = ''
        if ($obtido -ne $esperado[$conta].Saldo) {
            $divergentes++
            $nota = '  <- esperado ' + $esperado[$conta].Saldo.ToString('C2', $brasil)
        }
        Write-Host ('    {0}  {1,2} lancamentos  {2,13}{3}' -f $conta, $esperado[$conta].Quantidade, $obtido.ToString('C2', $brasil), $nota)
    }
    if ($divergentes -gt 0) {
        MostrarNaoConsolidados $itens $token
        throw 'O saldo de alguma conta nao bate com o movimento da demonstracao.'
    }

    Write-Host ''
    Write-Host 'Pronto. Na tela, http://localhost:4200, todos com a senha Demo@2026:'
    Write-Host '    cliente@lancamentos.local    conta ABC1234'
    Write-Host '    cliente2@lancamentos.local   conta XYZ9999'
    Write-Host '    admin@lancamentos.local      conta ADM0001, e escolhe qualquer outra'
}
catch {
    Write-Host "erro: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
