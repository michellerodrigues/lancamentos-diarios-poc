import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../core/auth.service';
import { LancamentoCriado, TipoLancamento } from '../core/lancamentos.models';
import { LancamentosService } from '../core/lancamentos.service';
import { formatarEntradaMoeda, hojeIso, paraNumero } from '../core/moeda';

@Component({
  selector: 'app-novo-lancamento',
  imports: [RouterLink],
  templateUrl: './novo-lancamento.html',
  styleUrl: './novo-lancamento.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NovoLancamentoComponent {
  private readonly servico = inject(LancamentosService);

  /** So o admin escolhe a conta. O cliente lanca sempre na dele. */
  protected readonly isAdmin = inject(AuthService).isAdmin;

  /** Comeca na conta em foco da tela de saldo; o admin pode trocar aqui. */
  protected readonly contaId = signal(this.servico.conta());

  /**
   * Teto do calendario. Data futura nao entra: a consolidacao segue a ordem da
   * conta, e um lancamento esperando a data travaria todos os seguintes.
   */
  protected readonly hoje = hojeIso();

  protected readonly tipo = signal<TipoLancamento>('Credito');
  protected readonly valorTexto = signal('');
  protected readonly dataLancamento = signal(hojeIso());
  protected readonly observacao = signal('');

  protected readonly enviando = signal(false);
  protected readonly erros = signal<string[]>([]);

  /** Preenchido apos a inclusao: troca o formulario pela confirmacao. */
  protected readonly criado = signal<LancamentoCriado | null>(null);

  /** Saldo exibido na confirmacao — segue inalterado ate o Consumer consolidar. */
  protected readonly saldo = this.servico.saldo;

  protected readonly ehDebito = computed(() => this.tipo() === 'Debito');

  protected readonly valorNumerico = computed(() => paraNumero(this.valorTexto()));

  protected readonly podeEnviar = computed(
    () =>
      this.contaId().trim().length >= 3 &&
      this.valorNumerico() > 0 &&
      this.dataLancamento().length > 0 &&
      // Digitada a mao, a data passa por cima do max do calendario. ISO compara como texto.
      this.dataLancamento() <= this.hoje &&
      !this.enviando(),
  );

  protected aoDigitarConta(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    const normalizada = campo.value.toUpperCase();

    this.contaId.set(normalizada);
    campo.value = normalizada;
  }

  protected selecionarTipo(tipo: TipoLancamento): void {
    this.tipo.set(tipo);
  }

  protected aoDigitarValor(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    const formatado = formatarEntradaMoeda(campo.value);

    this.valorTexto.set(formatado);

    // Reescreve o campo: a mascara muda o texto a cada tecla.
    campo.value = formatado;
  }

  protected aoDigitarData(evento: Event): void {
    this.dataLancamento.set((evento.target as HTMLInputElement).value);
  }

  protected aoDigitarObservacao(evento: Event): void {
    this.observacao.set((evento.target as HTMLTextAreaElement).value);
  }

  protected async incluir(): Promise<void> {
    if (!this.podeEnviar()) return;

    this.enviando.set(true);
    this.erros.set([]);

    try {
      const criado = await this.servico.incluir({
        // Para o cliente o BFF usa a conta do token; mandar a dele seria redundante.
        contaId: this.isAdmin() ? this.contaId().trim() : null,
        tipo: this.tipo(),
        valor: this.valorNumerico(),
        dataLancamento: this.dataLancamento(),
        observacao: this.observacao().trim() || null,
      });

      this.criado.set(criado);

      // O admin pode ter lancado em outra conta que nao a que estava em foco.
      // Sendo a mesma, trocarConta sai cedo e nao recarregaria nada — dai o else.
      if (criado.contaId !== this.servico.conta()) {
        await this.servico.trocarConta(criado.contaId);
      } else {
        await this.servico.acompanhar();
      }
    } catch (erro) {
      this.erros.set(LancamentosService.mensagensDeErro(erro));
    } finally {
      this.enviando.set(false);
    }
  }
}
