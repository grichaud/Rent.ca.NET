// Paridad Next.js src/features/homepage/components/stats-banner.tsx (useCountUp).
// Anima los números 0 → target con easing ease-out cubic cuando el bloque entra al
// viewport. Se activa una sola vez (IntersectionObserver con disconnect).
(function () {
    "use strict";

    const DURATION_MS = 2000;
    const counters = document.querySelectorAll("[data-stat-counter]");
    if (counters.length === 0) return;

    function formatValue(value, format) {
        if (format === "thousands") {
            return Math.floor(value / 1000).toString();
        }
        return value.toString();
    }

    function animate(el) {
        const target = parseInt(el.getAttribute("data-target") || "0", 10);
        const format = el.getAttribute("data-format") || "raw";
        if (!target) return;

        const start = performance.now();
        function step(now) {
            const elapsed = now - start;
            const progress = Math.min(elapsed / DURATION_MS, 1);
            // Ease out cubic — coincide con Next.js: 1 - Math.pow(1 - progress, 3)
            const eased = 1 - Math.pow(1 - progress, 3);
            const current = Math.floor(eased * target);
            el.textContent = formatValue(current, format);
            if (progress < 1) {
                requestAnimationFrame(step);
            } else {
                el.textContent = formatValue(target, format);
            }
        }
        requestAnimationFrame(step);
    }

    // Si el navegador no soporta IntersectionObserver, animar inmediatamente.
    if (typeof IntersectionObserver === "undefined") {
        counters.forEach(animate);
        return;
    }

    // Agrupar por contenedor común (#stats-banner) para activar todas las cards juntas.
    const container = document.getElementById("stats-banner");
    if (!container) {
        counters.forEach(animate);
        return;
    }

    const observer = new IntersectionObserver(
        function (entries) {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    counters.forEach(animate);
                    observer.disconnect();
                    break;
                }
            }
        },
        { threshold: 0.3 }
    );
    observer.observe(container);
})();
