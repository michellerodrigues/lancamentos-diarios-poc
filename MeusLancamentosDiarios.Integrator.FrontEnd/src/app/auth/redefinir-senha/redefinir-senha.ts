import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { mensagensDeErro } from '../../core/erros';

/** Destino do link do e-mail: /redefinir-senha?token=... */
@Component({
  selector: 'app-redefinir-senha',
  imports: [RouterLink],
  templateUrl: './redefinir-senha.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RedefinirSenhaComponent {
  private readonly auth = inject(AuthService);

  protected readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  protected readonly senha = signal('');
  protected readonly confirmacao = signal('');

  protected readonly enviando = signal(false);
  protected readonly erros = signal<string[]>([]);
  protected readonly concluido = signal(false);

  protected readonly senhasDiferentes = computed(
    () => this.confirmacao().length > 0 && this.confirmacao() !== this.senha(),
  );

  protected readonly podeEnviar = computed(
    () => this.senha().length > 0 && this.confirmacao() === this.senha() && !this.enviando(),
  );

  protected aoDigitar(campo: 'senha' | 'confirmacao', evento: Event): void {
    this[campo].set((evento.target as HTMLInputElement).value);
  }

  protected async redefinir(evento: Event): Promise<void> {
    evento.preventDefault();
    if (!this.podeEnviar()) return;

    this.enviando.set(true);
    this.erros.set([]);

    try {
      await this.auth.redefinirSenha(this.token, this.senha());

      // O servidor derrubou todas as sessoes, inclusive uma aberta neste aparelho.
      this.auth.descartar();
      this.concluido.set(true);
    } catch (erro) {
      this.erros.set(mensagensDeErro(erro, 'Não foi possível redefinir a senha.'));
    } finally {
      this.enviando.set(false);
    }
  }
}
