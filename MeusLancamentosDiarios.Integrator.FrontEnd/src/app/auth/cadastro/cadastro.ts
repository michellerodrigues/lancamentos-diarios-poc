import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { mensagensDeErro } from '../../core/erros';
import { BotaoGoogleComponent } from '../botao-google/botao-google';

/** Cadastro normal ou com Google. Os dois criam a conta do usuario e ja entram. */
@Component({
  selector: 'app-cadastro',
  imports: [RouterLink, BotaoGoogleComponent],
  templateUrl: './cadastro.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CadastroComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly nome = signal('');
  protected readonly email = signal('');
  protected readonly senha = signal('');
  protected readonly confirmacao = signal('');

  protected readonly enviando = signal(false);
  protected readonly erros = signal<string[]>([]);

  /** So acusa depois que a confirmacao comecou a ser digitada. */
  protected readonly senhasDiferentes = computed(
    () => this.confirmacao().length > 0 && this.confirmacao() !== this.senha(),
  );

  // A regra de forca da senha fica no servidor; aqui so o que evita ida a toa.
  protected readonly podeEnviar = computed(
    () =>
      this.nome().trim().length > 0 &&
      this.email().trim().length > 0 &&
      this.senha().length > 0 &&
      this.confirmacao() === this.senha() &&
      !this.enviando(),
  );

  protected aoDigitar(campo: 'nome' | 'email' | 'senha' | 'confirmacao', evento: Event): void {
    this[campo].set((evento.target as HTMLInputElement).value);
  }

  protected async cadastrar(evento: Event): Promise<void> {
    evento.preventDefault();
    if (!this.podeEnviar()) return;

    await this.abrirSessao(() =>
      this.auth.cadastrar(this.nome().trim(), this.email().trim(), this.senha()),
    );
  }

  protected async cadastrarComGoogle(credencial: string): Promise<void> {
    await this.abrirSessao(() => this.auth.entrarComGoogle(credencial));
  }

  private async abrirSessao(cadastro: () => Promise<void>): Promise<void> {
    this.enviando.set(true);
    this.erros.set([]);

    try {
      await cadastro();
      await this.router.navigateByUrl('/');
    } catch (erro) {
      this.erros.set(mensagensDeErro(erro, 'Não foi possível criar a conta.'));
    } finally {
      this.enviando.set(false);
    }
  }
}
