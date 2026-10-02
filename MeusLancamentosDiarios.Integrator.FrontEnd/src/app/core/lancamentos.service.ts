import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { AuthService } from './auth.service';
import { mensagensDeErro } from './erros';
import { LancamentoCriado, NovoLancamento, SaldoTela } from './lancamentos.models';

/**
 * Unica porta de acesso ao BFF.
 *
 * O acompanhamento do saldo usa polling condicional: o BFF devolve
 * intervaloPollingMs > 0 apenas enquanto ha lancamento em transito, e zero
 * assim que tudo consolidou. Em repouso, portanto, nao ha trafego nenhum.
 *
 * Trocar isso por SignalR mais tarde encosta so neste arquivo: os componentes
 * leem os signals e nao sabem de onde o dado vem.
 */
@Injectable({ providedIn: 'root' })
export class LancamentosService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly _saldo = signal<SaldoTela | null>(null);
  private readonly _carregando = signal(false);
  private readonly _offline = signal(false);

  /** Conta que o admin escolheu olhar. Null segue a conta do proprio usuario. */
  private readonly _contaEscolhida = signal<string | null>(null);

  private agendamento?: ReturnType<typeof setTimeout>;

  /** Ultimo saldo conhecido. Sobrevive a uma falha de rede, para a PWA seguir util. */
  readonly saldo = this._saldo.asReadonly();

  readonly carregando = this._carregando.asReadonly();

  /** Verdadeiro quando a ultima tentativa falhou e a tela esta mostrando dado em cache. */
  readonly offline = this._offline.asReadonly();

  /**
   * Conta em foco. O cliente so tem uma, a do token; o admin comeca na dele e pode
   * olhar qualquer outra.
   */
  readonly conta = computed(() => this._contaEscolhida() ?? this.auth.usuario()?.contaId ?? '');

  constructor() {
    // Trocou o usuario (login, logout, outra pessoa no mesmo aparelho): nada do
    // anterior pode continuar na tela.
    effect(() => {
      // Lido so para o effect depender do usuario.
      void this.auth.usuario()?.id;

      untracked(() => {
        this.pararAcompanhamento();
        this._contaEscolhida.set(null);
        this._saldo.set(null);
        this._offline.set(false);
      });
    });
  }

  /**
   * Troca a conta em foco e recomeca o acompanhamento. So o admin troca. O saldo
   * anterior e descartado na hora: manter na tela o numero de outra conta seria
   * enganoso.
   */
  async trocarConta(contaId: string): Promise<void> {
    if (!this.auth.isAdmin()) return;

    const normalizada = contaId.trim().toUpperCase();
    if (!normalizada || normalizada === this.conta()) return;

    this._contaEscolhida.set(normalizada);
    this._saldo.set(null);

    await this.acompanhar();
  }

  async carregarSaldo(): Promise<void> {
    const conta = this.conta();
    if (!conta) return;

    this._carregando.set(true);

    try {
      const saldo = await firstValueFrom(
        this.http.get<SaldoTela>(`${API_BASE_URL}/saldo/${encodeURIComponent(conta)}`),
      );

      this._saldo.set(saldo);
      this._offline.set(false);
    } catch {
      // Mantem o ultimo saldo na tela: o carimbo de "ultima atualizacao" ja
      // diz ao usuario de quando ele e.
      this._offline.set(true);
    } finally {
      this._carregando.set(false);
    }
  }

  /**
   * Carrega o saldo e, enquanto houver pendencia, reconsulta no intervalo que o
   * BFF sugerir. Para sozinho quando nao ha mais nada em transito.
   */
  async acompanhar(): Promise<void> {
    this.pararAcompanhamento();
    await this.carregarSaldo();

    const intervalo = this._saldo()?.intervaloPollingMs ?? 0;
    if (intervalo <= 0 || !this.auth.autenticado()) return;

    this.agendamento = setTimeout(() => void this.acompanhar(), intervalo);
  }

  pararAcompanhamento(): void {
    if (this.agendamento === undefined) return;

    clearTimeout(this.agendamento);
    this.agendamento = undefined;
  }

  /** Contas que ja tem lancamento. O BFF so responde ao admin. */
  async listarContas(): Promise<string[]> {
    try {
      return await firstValueFrom(this.http.get<string[]>(`${API_BASE_URL}/contas`));
    } catch {
      // Sem sugestoes o seletor continua aceitando a conta digitada.
      return [];
    }
  }

  async incluir(lancamento: NovoLancamento): Promise<LancamentoCriado> {
    return await firstValueFrom(
      this.http.post<LancamentoCriado>(`${API_BASE_URL}/lancamentos`, lancamento),
    );
  }

  /** Extrai as mensagens do FluentValidation para exibicao no formulario. */
  static mensagensDeErro(erro: unknown): string[] {
    return mensagensDeErro(erro, 'Não foi possível incluir o lançamento.');
  }
}
