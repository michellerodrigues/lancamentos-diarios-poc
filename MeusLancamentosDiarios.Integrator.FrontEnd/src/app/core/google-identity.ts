/**
 * O minimo do Google Identity Services que a tela usa: o botao "Fazer login com o
 * Google", que devolve um ID token (credential) para a WebApi conferir.
 */
export interface GoogleIdentity {
  accounts: {
    id: {
      initialize(config: {
        client_id: string;
        callback: (resposta: { credential: string }) => void;
      }): void;
      renderButton(elemento: HTMLElement, opcoes: Record<string, unknown>): void;
      disableAutoSelect(): void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentity;
  }
}

const SCRIPT = 'https://accounts.google.com/gsi/client';

let carregamento: Promise<GoogleIdentity> | undefined;

/** Baixa o script so quando o login com Google esta ligado, e uma vez so. */
export function carregarGoogleIdentity(): Promise<GoogleIdentity> {
  carregamento ??= new Promise<GoogleIdentity>((resolver, rejeitar) => {
    if (window.google) {
      resolver(window.google);
      return;
    }

    const script = document.createElement('script');
    script.src = SCRIPT;
    script.async = true;
    script.onload = () =>
      window.google ? resolver(window.google) : rejeitar(new Error('Google Identity ausente.'));
    script.onerror = () => {
      carregamento = undefined;
      rejeitar(new Error('Nao foi possivel carregar o Google Identity.'));
    };

    document.head.appendChild(script);
  });

  return carregamento;
}
