import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { mensagensDeErro } from '../../core/erros';
import { BotaoGoogleComponent } from '../botao-google/botao-google';

@Component({
  selector: 'app-entrar',
  imports: [RouterLink, BotaoGoogleComponent],
  templateUrl: './entrar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntrarComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly senha = signal('');

  protected readonly enviando = signal(false);
  protected readonly erros = signal<string[]>([]);

  protected readonly podeEnviar = computed(
    () => this.email().trim().length > 0 && this.senha().length > 0 && !this.enviando(),
  );

  protected aoDigitarEmail(evento: Event): void {
    this.email.set((evento.target as HTMLInputElement).value);
  }

  protected aoDigitarSenha(evento: Event): void {
    this.senha.set((evento.target as HTMLInputElement).value);
  }

  protected async entrar(evento: Event): Promise<void> {
    evento.preventDefault();
    if (!this.podeEnviar()) return;

    await this.abrirSessao(() => this.auth.entrar(this.email().trim(), this.senha()));
  }

  protected async entrarComGoogle(credencial: string): Promise<void> {
    await this.abrirSessao(() => this.auth.entrarComGoogle(credencial));
  }

  private async abrirSessao(login: () => Promise<void>): Promise<void> {
    this.enviando.set(true);
    this.erros.set([]);

    try {
      await login();
      await this.router.navigateByUrl('/');
    } catch (erro) {
      this.erros.set(mensagensDeErro(erro, 'Não foi possível entrar.'));
    } finally {
      this.enviando.set(false);
    }
  }
}
