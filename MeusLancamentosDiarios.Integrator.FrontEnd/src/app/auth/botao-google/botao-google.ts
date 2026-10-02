import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  NgZone,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { AuthService } from '../../core/auth.service';
import { GoogleIdentity, carregarGoogleIdentity } from '../../core/google-identity';

/**
 * Botao oficial do Google. So aparece quando a WebApi tem um Client ID
 * configurado; sem ele, a tela fica so com e-mail e senha.
 *
 * Emite a credencial (um ID token do Google). Quem troca por sessao e a tela.
 */
@Component({
  selector: 'app-botao-google',
  template: `
    @if (habilitado()) {
      <div class="separador">ou</div>
      <div class="botao-google" #alvo></div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BotaoGoogleComponent {
  private readonly auth = inject(AuthService);
  private readonly zona = inject(NgZone);

  /** Texto do botao, no vocabulario do Google. */
  readonly texto = input<'signin_with' | 'signup_with' | 'continue_with'>('continue_with');

  readonly credencial = output<string>();

  private readonly google = signal<GoogleIdentity | null>(null);
  private readonly alvo = viewChild<ElementRef<HTMLElement>>('alvo');

  protected readonly habilitado = computed(() => this.google() !== null);

  constructor() {
    void this.iniciar();

    // O alvo so existe depois que o @if renderiza; o botao e desenhado nele.
    effect(() => {
      const google = this.google();
      const elemento = this.alvo()?.nativeElement;
      if (!google || !elemento) return;

      google.accounts.id.renderButton(elemento, {
        theme: 'outline',
        size: 'large',
        shape: 'pill',
        text: this.texto(),
        locale: 'pt-BR',
        width: Math.min(400, Math.max(200, elemento.clientWidth)),
      });
    });
  }

  private async iniciar(): Promise<void> {
    const { googleClientId } = await this.auth.configuracao();
    if (!googleClientId) return;

    try {
      const google = await carregarGoogleIdentity();

      google.accounts.id.initialize({
        client_id: googleClientId,
        // O callback vem de um iframe do Google, fora da zona do Angular.
        callback: (resposta) => this.zona.run(() => this.credencial.emit(resposta.credential)),
      });

      this.google.set(google);
    } catch {
      // Script bloqueado ou sem rede: o login por senha continua funcionando.
    }
  }
}
