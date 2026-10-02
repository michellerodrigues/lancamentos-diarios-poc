import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../core/auth.service';
import { LancamentosService } from '../core/lancamentos.service';

@Component({
  selector: 'app-saldo-consolidado',
  imports: [RouterLink],
  templateUrl: './saldo-consolidado.html',
  styleUrl: './saldo-consolidado.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SaldoConsolidadoComponent implements OnInit {
  private readonly servico = inject(LancamentosService);
  private readonly auth = inject(AuthService);

  protected readonly saldo = this.servico.saldo;
  protected readonly carregando = this.servico.carregando;
  protected readonly offline = this.servico.offline;
  protected readonly conta = this.servico.conta;

  protected readonly usuario = this.auth.usuario;
  protected readonly isAdmin = this.auth.isAdmin;

  /** Sugestoes do seletor de conta. So o admin escolhe conta. */
  protected readonly contas = signal<string[]>([]);

  constructor() {
    // O acompanhamento so vive enquanto a tela estiver montada.
    inject(DestroyRef).onDestroy(() => this.servico.pararAcompanhamento());
  }

  ngOnInit(): void {
    void this.servico.acompanhar();

    if (this.isAdmin()) {
      void this.servico.listarContas().then((contas) => this.contas.set(contas));
    }
  }

  protected aoTrocarConta(evento: Event): void {
    void this.servico.trocarConta((evento.target as HTMLInputElement).value);
  }

  protected sair(): void {
    void this.auth.sair();
  }
}
