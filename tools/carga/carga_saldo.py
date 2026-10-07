"""Carga de leitura no saldo consolidado: GET /api/saldo/{conta} pelo BFF, como a tela faz.

Mede o requisito do desafio: "em dias de picos, o servico de consolidado diario recebe
50 requisicoes por segundo, com no maximo 5% de perda de requisicoes".

Chegada em taxa constante (malha aberta): cada requisicao sai no seu horario, sem
esperar a anterior responder. Assim a lentidao do servidor vira fila e perda, em vez de
reduzir a carga sem ninguem perceber. Perda = resposta fora de 2xx, timeout ou erro de
conexao.

Nao grava lancamentos: so o login abre uma sessao. Entra com o usuario de demonstracao
cliente@lancamentos.local, entao a conta consultada precisa ser a dele (ABC1234);
outra conta devolve 403 e conta como perda.

Uso, com a stack no ar (docker compose up -d) e Python 3, sem pacotes externos:
    python tools/carga/carga_saldo.py 50 60              # 50 req/s por 60 s, conta ABC1234
    python tools/carga/carga_saldo.py 100 30 ABC1234 5   # conta e timeout (s) opcionais

Variavel de ambiente opcional: BFF_URL (padrao http://localhost:5100).
"""
import json
import os
import statistics
import sys
import threading
import time
import urllib.error
import urllib.request
from collections import Counter
from concurrent.futures import ThreadPoolExecutor

BFF = os.environ.get("BFF_URL", "http://localhost:5100")


def login(email, senha):
    corpo = json.dumps({"email": email, "senha": senha}).encode()
    req = urllib.request.Request(BFF + "/api/auth/login", data=corpo,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=15) as r:
        return json.load(r)["tokenAcesso"]


def carga(rps, segundos, url, token, timeout):
    resultados = []
    trava = threading.Lock()

    def uma():
        t0 = time.perf_counter()
        try:
            req = urllib.request.Request(url, headers={"Authorization": "Bearer " + token})
            with urllib.request.urlopen(req, timeout=timeout) as r:
                r.read()
                status = r.status
        except urllib.error.HTTPError as e:
            status = e.code
        except Exception as e:  # timeout, conexao recusada, etc.
            status = type(e).__name__
        with trava:
            resultados.append((status, time.perf_counter() - t0))

    total = int(rps * segundos)
    inicio = time.perf_counter()
    atraso_max = 0.0
    with ThreadPoolExecutor(max_workers=400) as ex:
        for i in range(total):
            alvo = inicio + i / rps
            agora = time.perf_counter()
            if alvo > agora:
                time.sleep(alvo - agora)
            else:
                atraso_max = max(atraso_max, agora - alvo)
            ex.submit(uma)
    duracao = time.perf_counter() - inicio

    por_status = Counter(s for s, _ in resultados)
    ok = sorted(lat for s, lat in resultados if isinstance(s, int) and 200 <= s < 300)
    perdidas = len(resultados) - len(ok)

    def pct(p):
        if not ok:
            return None
        return round(ok[min(len(ok) - 1, int(round(p / 100 * (len(ok) - 1))))] * 1000, 1)

    return {
        "rps_alvo": rps,
        "segundos": segundos,
        "enviadas": len(resultados),
        "taxa_real_rps": round(len(resultados) / duracao, 1),
        "ok": len(ok),
        "perdidas": perdidas,
        "perda_pct": round(100 * perdidas / max(1, len(resultados)), 2),
        "por_status": {str(k): v for k, v in por_status.items()},
        "lat_ms": {
            "p50": pct(50), "p95": pct(95), "p99": pct(99),
            "max": round(ok[-1] * 1000, 1) if ok else None,
            "media": round(statistics.mean(ok) * 1000, 1) if ok else None,
        },
        # Se passar de alguns ms, o proprio gerador nao deu conta da taxa pedida.
        "atraso_max_do_gerador_ms": round(atraso_max * 1000, 1),
    }


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    rps = float(sys.argv[1])
    segundos = float(sys.argv[2])
    conta = sys.argv[3] if len(sys.argv) > 3 else "ABC1234"
    timeout = float(sys.argv[4]) if len(sys.argv) > 4 else 5.0
    token = login("cliente@lancamentos.local", "Demo@2026")
    print(json.dumps(carga(rps, segundos, f"{BFF}/api/saldo/{conta}", token, timeout),
                     ensure_ascii=False, indent=2))
