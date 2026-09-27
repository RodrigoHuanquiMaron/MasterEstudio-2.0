// Conecta la página del usuario al canal de PieHost y la actualiza sin recargar
(function () {
    const config = document.getElementById('tiempo-real');
    if (!config) return;

    const url = config.dataset.url;
    const estado = document.getElementById('estado-tiempo-real');
    const avisos = document.getElementById('avisos-tiempo-real');
    let intentos = 0;
    let yaConectoAntes = false;

    function mostrarEstado(texto, clase) {
        if (!estado) return;
        estado.textContent = texto;
        estado.className = 'badge ' + clase;
    }

    function avisar(texto) {
        if (!avisos) return;
        const div = document.createElement('div');
        div.className = 'alert alert-info shadow-sm mb-2';
        div.textContent = texto;
        avisos.appendChild(div);
        setTimeout(() => div.remove(), 6000);
    }

    // Pide la página actual al servidor y reemplaza solo el contenido principal
    async function actualizarContenido() {
        try {
            const respuesta = await fetch(window.location.href, { cache: 'no-store' });
            if (!respuesta.ok) return;
            const html = await respuesta.text();
            const nuevo = new DOMParser().parseFromString(html, 'text/html').querySelector('main');
            const actual = document.querySelector('main');
            if (nuevo && actual) actual.innerHTML = nuevo.innerHTML;
        } catch (e) {
            console.warn('No se pudo actualizar la página', e);
        }
    }

    // PieHost puede entregar el mensaje como texto JSON; se interpreta con cuidado
    function leerEvento(datos) {
        let mensaje = datos;
        for (let i = 0; i < 2 && typeof mensaje === 'string'; i++) {
            try { mensaje = JSON.parse(mensaje); } catch { return null; }
        }
        return mensaje && mensaje.event === 'CursoActualizado' ? mensaje.data : null;
    }

    function conectar() {
        mostrarEstado('Conectando…', 'text-bg-secondary');
        const socket = new WebSocket(url);

        socket.onopen = () => {
            mostrarEstado('En vivo', 'text-bg-success');
            // Al reconectar se consulta el estado vigente, por si hubo cambios mientras no había conexión
            if (yaConectoAntes) actualizarContenido();
            yaConectoAntes = true;
            intentos = 0;
        };

        socket.onmessage = (e) => {
            const datos = leerEvento(e.data);
            if (!datos) return;
            avisar(datos.accion === 'Eliminado'
                ? `Se eliminó el curso "${datos.titulo}".`
                : `Nuevo curso disponible: "${datos.titulo}".`);
            actualizarContenido();
        };

        socket.onclose = () => {
            mostrarEstado('Reconectando…', 'text-bg-warning');
            intentos++;
            setTimeout(conectar, Math.min(30000, 1000 * 2 ** intentos));
        };

        socket.onerror = () => socket.close();
    }

    conectar();
})();