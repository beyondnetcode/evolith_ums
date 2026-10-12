/**
 * Resuelve la URL de un recurso estático servido desde `public/`, respetando el prefijo
 * público con el que se construyó el bundle.
 *
 * POR QUÉ HACE FALTA. Vite reescribe las rutas de los recursos que pasan por su grafo de
 * imports y las del `index.html`, pero NO los literales de cadena escritos a mano en el
 * JSX: `<img src="/brand/logo.png" />` se emite tal cual. Con la app servida en la raíz eso
 * funciona por casualidad. Bajo un subpath —el directorio virtual `/ums_web/` de IIS— la
 * petición se va a la raíz del sitio y devuelve 404. Medido el 2026-08-15: los dos logos de
 * EVOLITH eran las únicas peticiones fallidas del despliegue.
 *
 * `import.meta.env.BASE_URL` es el `base` de Vite y siempre termina en `/`: vale `/` en el
 * despliegue de contenedor y `/ums_web/` en el de IIS.
 *
 * @param ruta Ruta del recurso relativa a `public/`, con o sin barra inicial.
 */
export const assetUrl = (ruta: string): string =>
  `${import.meta.env.BASE_URL}${ruta.replace(/^\/+/, '')}`;
