import primeui from 'tailwindcss-primeui';

/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{vue,js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#eff6ff',
          100: '#dbeafe',
          500: '#3b82f6',
          600: '#2563eb',
          700: '#1d4ed8',
        },
        ink: {
          900: '#0f172a',
          700: '#334155',
          600: '#475569',
          500: '#64748b',
          400: '#94a3b8',
        },
        line: '#e2e8f0',
        surface: '#f8fafc',
        danger: '#ef4444',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        card: '0 10px 25px -3px rgba(0,0,0,0.08), 0 4px 6px 0 rgba(0,0,0,0.04)',
        button: '0 1px 2px 0 rgba(37,99,235,0.3)',
        ring: '0 0 0 3px rgba(37,99,235,0.1)',
      },
    },
  },
  plugins: [primeui],
};
