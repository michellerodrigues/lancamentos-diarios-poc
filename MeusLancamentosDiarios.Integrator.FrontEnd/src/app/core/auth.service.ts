import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { ConfiguracaoAuth, Sessao } from './auth.models';

const CHAVE_SESSAO = 'lancamentos.sessao';

/**
 * Sessao do usuario: login, cadastro, Google, renovacao e logout.
 *
 * A sessao fica no localStorage para sobreviver ao F5 e a PWA reaberta. O preco e
 * ficar ao alcance de um XSS; o acesso curto (15 min) e a renovacao de uso unico,
 * revogada no logout, limitam o estrago.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _sessao = signal<Sessao | null>(lerSessaoSalva());

  private renovacaoEmCurso: Promise<string | null> | null = null;
  private configuracaoEmCache?: Promise<ConfiguracaoAuth>;

  readonly usuario = computed(() => this._sessao()?.usuario ?? null);

  readonly autenticado = computed(() => this._sessao() !== null);

  readonly isAdmin = computed(() => this.usuario()?.role === 'Admin');

  /** Lido pelo interceptor a cada requisicao. */
  tokenAcesso(): string | null {
    return this._sessao()?.tokenAcesso ?? null;
  }

  async entrar(email: string, senha: string): Promise<void> {
    this.guardar(
      await firstValueFrom(this.http.post<Sessao>(`${API_BASE_URL}/auth/login`, { email, senha })),
    );
  }

  async cadastrar(nome: string, email: string, senha: string): Promise<void> {
    this.guardar(
      await firstValueFrom(
        this.http.post<Sessao>(`${API_BASE_URL}/auth/registrar`, { nome, email, senha }),
      ),
    );
  }

  async entrarComGoogle(credencial: string): Promise<void> {
    this.guardar(
      await firstValueFrom(this.http.post<Sessao>(`${API_BASE_URL}/auth/google`, { credencial })),
    );
  }

  async esqueciSenha(email: string): Promise<void> {
    await firstValueFrom(this.http.post(`${API_BASE_URL}/auth/esqueci-senha`, { email }));
  }

  async redefinirSenha(token: string, novaSenha: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/auth/redefinir-senha`, { token, novaSenha }),
    );
  }

  /** Lida uma vez por carga da aplicacao: o Client ID nao muda enquanto ela roda. */
  configuracao(): Promise<ConfiguracaoAuth> {
    this.configuracaoEmCache ??= firstValueFrom(
      this.http.get<ConfiguracaoAuth>(`${API_BASE_URL}/auth/config`),
    ).catch(() => ({ googleClientId: '' }));

    return this.configuracaoEmCache;
  }

  /**
   * Troca o token de renovacao por um par novo e devolve o acesso novo, ou null
   * quando a sessao acabou.
   *
   * Uma renovacao por vez: varias telas recebendo 401 juntas compartilham a mesma.
   * O token de renovacao e de uso unico, e duas renovacoes paralelas com ele seriam
   * lidas pelo servidor como token roubado, derrubando a sessao.
   */
  renovar(): Promise<string | null> {
    this.renovacaoEmCurso ??= this.executarRenovacao().finally(() => {
      this.renovacaoEmCurso = null;
    });

    return this.renovacaoEmCurso;
  }

  async sair(): Promise<void> {
    const atual = this._sessao();
    this.descartar();

    // Sem o botao do Google escolhendo a conta sozinho na proxima visita.
    window.google?.accounts.id.disableAutoSelect();

    if (atual) {
      try {
        await firstValueFrom(
          this.http.post(`${API_BASE_URL}/auth/sair`, { tokenRenovacao: atual.tokenRenovacao }),
        );
      } catch {
        // Sem rede, o token de renovacao so vence. Localmente a sessao ja acabou.
      }
    }

    await this.router.navigateByUrl('/entrar');
  }

  /** A renovacao foi recusada: a sessao acabou do lado do servidor. */
  async expirou(): Promise<void> {
    this.descartar();
    await this.router.navigateByUrl('/entrar');
  }

  private async executarRenovacao(): Promise<string | null> {
    const atual = this._sessao();
    if (!atual) return null;

    try {
      const nova = await firstValueFrom(
        this.http.post<Sessao>(`${API_BASE_URL}/auth/renovar`, {
          tokenRenovacao: atual.tokenRenovacao,
        }),
      );

      this.guardar(nova);
      return nova.tokenAcesso;
    } catch {
      this.descartar();
      return null;
    }
  }

  private guardar(sessao: Sessao): void {
    this._sessao.set(sessao);

    try {
      localStorage.setItem(CHAVE_SESSAO, JSON.stringify(sessao));
    } catch {
      // Armazenamento bloqueado: a sessao vale ate fechar a aba.
    }
  }

  /** Apaga a sessao local sem avisar o servidor, para quando ele ja a encerrou. */
  descartar(): void {
    this._sessao.set(null);

    try {
      localStorage.removeItem(CHAVE_SESSAO);
    } catch {
      // Nada a limpar.
    }
  }
}

function lerSessaoSalva(): Sessao | null {
  try {
    const salva = localStorage.getItem(CHAVE_SESSAO);
    return salva ? (JSON.parse(salva) as Sessao) : null;
  } catch {
    return null;
  }
}
