import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

// Resolve absolute path so Tailwind finds its config regardless of process.cwd()
// (Docker runs npm from the workspace root /usr/src/app, not from this app's directory)
const __dirname = dirname(fileURLToPath(import.meta.url));

export default {
  plugins: {
    tailwindcss: { config: join(__dirname, 'tailwind.config.js') },
    autoprefixer: {},
  },
};
