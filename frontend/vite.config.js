// vite.config.js
import commonjs from '@rollup/plugin-commonjs';

export default {
    plugins: [commonjs()],
    build: {
        target: 'esnext',  // Ensure you're targeting modern browsers
        outDir: 'dist',    // Specify your output directory for the build
        output: {
            format: 'es',  // Use ES module format
        }
    }
}