/** Estagios pelos quais um lancamento passa ate entrar no saldo consolidado. */
export type StageLancamento =
  | 'Cadastrado'
  | 'Lido'
  | 'Enfileirado'
  | 'EmProcessamento'
  | 'Consolidado'
  | 'Erro';

export type TipoLancamento = 'Credito' | 'Debito';

export interface PendenteTela {
  id: string;

  /** Posicao dentro da conta. */
  sequencia: number;
  tipo: TipoLancamento;
  tipoRotulo: string;
  sinal: '+' | '-';
  valor: number;
  valorFormatado: string;
  dataLancamento: string;
  dataLancamentoFormatada: string;
  observacao?: string | null;
  stage: StageLancamento;
  stageRotulo: string;
}

export interface SaldoTela {
  contaId: string;
  saldoConsolidado: number;
  saldoConsolidadoFormatado: string;
  /** Null enquanto a conta nao consolidou nada. */
  atualizadoEm: string | null;
  ultimaAtualizacaoFormatada: string;

  /** Ate que sequencia da conta o saldo esta em dia. */
  ultimaSequenciaConsolidada: number;
  saldoProjetado: number;
  saldoProjetadoFormatado: string;
  creditosPendentes: number;
  creditosPendentesFormatado: string;
  debitosPendentes: number;
  debitosPendentesFormatado: string;
  consolidacaoEmAndamento: boolean;

  /** Zero significa que nada esta em transito e o polling deve parar. */
  intervaloPollingMs: number;

  pendentes: PendenteTela[];
}

export interface NovoLancamento {
  /** So o admin informa. Para o cliente, o BFF usa a conta do token. */
  contaId?: string | null;
  tipo: TipoLancamento;
  valor: number;
  dataLancamento: string;
  observacao?: string | null;
}

export interface LancamentoCriado {
  id: string;
  contaId: string;
  sequencia: number;
  tipo: TipoLancamento;
  tipoRotulo: string;
  valor: number;
  valorFormatado: string;
  sinal: '+' | '-';
  dataLancamento: string;
  dataLancamentoFormatada: string;
  dataRegistro: string;
  dataRegistroFormatada: string;
  observacao?: string | null;
  stage: StageLancamento;
  stageRotulo: string;
}
