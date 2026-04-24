/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Pages/**/*.cshtml',
    './Features/**/*.cshtml',
    './Features/**/*.cs',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50:  '#fdf6ee',
          100: '#f9e7cf',
          200: '#f3cd9e',
          300: '#eaac65',
          400: '#e08d3b',
          500: '#c86f1e',
          600: '#a05414',
          700: '#7e4213',
          800: '#5d3110',
          900: '#3f220c'
        }
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        display: ['"DM Serif Display"', 'Georgia', 'serif']
      },
      animation: {
        'float-bg': 'floatBG 15s ease-in-out infinite',
        'pulse-slow': 'pulse 4s cubic-bezier(0.4, 0, 0.6, 1) infinite'
      },
      keyframes: {
        floatBG: {
          '0%, 100%': { backgroundPosition: 'center center' },
          '25%':      { backgroundPosition: '30% 70%' },
          '50%':      { backgroundPosition: '70% 30%' },
          '75%':      { backgroundPosition: '40% 60%' }
        }
      },
      backdropBlur: {
        xs: '2px'
      }
    }
  },
  plugins: []
};
