// Búsqueda mientras escribes: pide la misma página al servidor y reemplaza solo los resultados.
// Las claves de Algolia y YouTube nunca llegan al navegador.
(function () {
    const form = document.getElementById('form-busqueda');
    const input = document.getElementById('q');
    const resultados = document.getElementById('resultados');
    if (!form || !input || !resultados) return;

    const minimo = +(form.dataset.minimo || 1);
    let espera = null;
    let ultima = input.value;
    let peticion = 0;

    async function buscar() {
        const numero = ++peticion;
        const params = new URLSearchParams(new FormData(form));
        const url = form.action + '?' + params.toString();
        resultados.classList.add('buscando');
        try {
            const respuesta = await fetch(url, { cache: 'no-store' });
            if (!respuesta.ok || numero !== peticion) return;
            const html = await respuesta.text();
            const nuevo = new DOMParser().parseFromString(html, 'text/html').getElementById('resultados');
            if (nuevo) resultados.innerHTML = nuevo.innerHTML;
            history.replaceState(null, '', url);
        } catch (e) {
            console.warn('No se pudo buscar', e);
        } finally {
            if (numero === peticion) resultados.classList.remove('buscando');
        }
    }

    input.addEventListener('input', () => {
        const texto = input.value.trim();
        if (texto === ultima.trim()) return;
        ultima = input.value;
        clearTimeout(espera);
        if (texto.length > 0 && texto.length < minimo) return;
        espera = setTimeout(buscar, +(form.dataset.espera || 300));
    });
    form.querySelectorAll('select').forEach(s => s.addEventListener('change', buscar));
})();
