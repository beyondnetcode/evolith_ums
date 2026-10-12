import type { Page } from '@playwright/test';

/**
 * La pantalla de acceso del Tablero. Sin aserciones a proposito: un Page Object que afirma obliga a
 * leer dos ficheros para saber que prueba un escenario.
 */
export class PaginaDeAcceso {
  constructor(private readonly page: Page) {}

  usuario = () => this.page.getByLabel('Usuario', { exact: true });
  clave = () => this.page.getByLabel('Contraseña', { exact: true });
  boton = () => this.page.getByRole('button', { name: /iniciar sesión|ingresando/i });
  titulo = () => this.page.getByRole('heading', { name: /evolith sdlc/i });
  error = () => this.page.getByRole('alert');

  async ir() {
    await this.page.goto('/');
  }

  /**
   * Rellena y envia, esperando la RESPUESTA del login y no un reloj. Esperar tiempo fijo es la
   * carrera que ya hizo parpadear al carril A (G-236): con el backend frio la respuesta tarda mas
   * de lo supuesto y la asercion siguiente mira un estado que aun no ha llegado.
   */
  async entrar(usuario: string, clave: string) {
    await this.usuario().fill(usuario);
    await this.clave().fill(clave);
    const respuesta = this.page.waitForResponse(
      (r) => r.url().includes('/api/auth/login') && r.request().method() === 'POST',
    );
    await this.boton().click();
    return respuesta;
  }
}
