import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { mensagensDeErro } from '../../core/erros';

@Component({
  selector: 'app-esqueci-senha',
  imports: [RouterLink],
  templateUrl: './esqueci-senha.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EsqueciSenhaComponent {
  private readonly auth = inject(AuthService);

  protected readonly email = signal('');
  protected readonly enviando = signal(false);
  protected readonly erros = signal<string[]>([]);

  /** Troca o formulario pela confirmacao. */
  protected readonly enviado = signal(false);

  protected readonly podeEnviar = computed(() => this.email().trim().length > 0 && !this.enviando());

  protected aoDigitarEmail(evento: Event): void {
    this.email.set((evento.target as HTMLInputElement).value);
  }

  protected async enviar(evento: Event): Promise<void> {
    evento.preventDefault();
    if (!this.podeEnviar()) return;

    this.enviando.set(true);
    this.erros.set([]);

    try {
      await this.auth.esqueciSenha(this.email().trim());
      this.enviado.set(true);
    } catch (erro) {
      this.erros.set(mensagensDeErro(erro, 'Não foi possível enviar o link.'));
    } finally {
      this.enviando.set(false);
    }
  }
}
