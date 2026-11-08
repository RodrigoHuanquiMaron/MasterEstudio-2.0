// Tutor IA: cuestionarios y explicaciones a partir del documento del alumno (Groq, desde el servidor)
(function () {
    const panel = document.getElementById('tutor-panel');
    const botonAbrir = document.getElementById('tutor-abrir');
    if (!panel || !botonAbrir) return;

    const $ = id => document.getElementById(id);
    const urls = panel.dataset;
    const token = panel.querySelector('input[name="__RequestVerificationToken"]').value;

    const estado = {
        documentoId: null,
        nombre: '',
        dificultad: 'Media',
        ronda: 0,
        preguntas: [],        // { enunciado, opciones, correcta, explicacion, tema, elegida, calificada }
        historial: [],        // conversación del tab Profundizar
        totalCorrectas: 0,
        totalCalificadas: 0
    };

    // ---------- Utilidades ----------

    const escapar = t => String(t ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    // Markdown mínimo y seguro: **negrita**, `código`, listas con guion y párrafos
    function markdown(texto) {
        const lineas = escapar(texto).split(/\r?\n/);
        let html = '', enLista = false;
        for (const linea of lineas) {
            const item = linea.match(/^\s*(?:[-*•]|\d+[.)])\s+(.*)$/);
            if (item) {
                if (!enLista) { html += '<ul>'; enLista = true; }
                html += `<li>${item[1]}</li>`;
                continue;
            }
            if (enLista) { html += '</ul>'; enLista = false; }
            if (linea.trim()) html += `<p>${linea}</p>`;
        }
        if (enLista) html += '</ul>';
        return html
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            .replace(/`([^`]+)`/g, '<code>$1</code>');
    }

    async function enviar(url, cuerpo) {
        const opciones = { method: 'POST', headers: { 'RequestVerificationToken': token } };
        if (cuerpo instanceof FormData) {
            opciones.body = cuerpo;
        } else {
            opciones.headers['Content-Type'] = 'application/json';
            opciones.body = JSON.stringify(cuerpo);
        }
        const respuesta = await fetch(url, opciones);
        let datos = {};
        try { datos = await respuesta.json(); } catch { /* respuesta vacía */ }
        if (!respuesta.ok) throw new Error(datos.error || 'Ocurrió un error. Inténtalo de nuevo.');
        return datos;
    }

    function aviso(mensaje, tipo = 'danger') {
        const div = document.createElement('div');
        div.className = `alert alert-${tipo} small tutor-aviso`;
        div.setAttribute('role', 'alert');
        div.textContent = mensaje;
        panel.querySelector('.tutor-cuerpo').prepend(div);
        setTimeout(() => div.remove(), 7000);
    }

    // ---------- Abrir / cerrar y pestañas ----------

    function abrir() {
        panel.classList.add('abierto');
        panel.setAttribute('aria-hidden', 'false');
        botonAbrir.setAttribute('aria-expanded', 'true');
        document.body.classList.add('tutor-activo');
        setTimeout(() => $('tutor-cerrar').focus(), 50);
    }

    function cerrar() {
        panel.classList.remove('abierto');
        panel.setAttribute('aria-hidden', 'true');
        botonAbrir.setAttribute('aria-expanded', 'false');
        document.body.classList.remove('tutor-activo');
        botonAbrir.focus();
    }

    botonAbrir.addEventListener('click', () => panel.classList.contains('abierto') ? cerrar() : abrir());
    $('tutor-cerrar').addEventListener('click', cerrar);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && panel.classList.contains('abierto')) cerrar(); });

    function mostrarTab(nombre) {
        panel.querySelectorAll('.tutor-tabs button').forEach(b => {
            const activo = b.dataset.tab === nombre;
            b.classList.toggle('activo', activo);
            b.setAttribute('aria-selected', activo);
        });
        $('tab-quiz').hidden = nombre !== 'quiz';
        $('tab-chat').hidden = nombre !== 'chat';
        if (nombre === 'chat') setTimeout(() => $('chat-texto').focus(), 50);
    }
    panel.querySelectorAll('.tutor-tabs button').forEach(b => b.addEventListener('click', () => mostrarTab(b.dataset.tab)));

    // ---------- Pasos del cuestionario ----------

    function paso(nombre) {
        $('paso-subir').hidden = nombre !== 'subir';
        $('paso-cargando').hidden = nombre !== 'cargando';
        $('paso-quiz').hidden = nombre !== 'quiz';
    }

    let mensajesCarga = null;
    function cargando(textos) {
        paso('cargando');
        let i = 0;
        $('texto-cargando').textContent = textos[0];
        clearInterval(mensajesCarga);
        mensajesCarga = setInterval(() => { i = (i + 1) % textos.length; $('texto-cargando').textContent = textos[i]; }, 2500);
    }
    const finCarga = () => clearInterval(mensajesCarga);

    // ---------- 1. Elegir archivo (clic o arrastrar) ----------

    const zona = $('zona-archivo');
    const inputArchivo = $('tutor-archivo');
    let archivo = null;

    function elegir(f) {
        if (!f) return;
        const ext = (f.name.split('.').pop() || '').toLowerCase();
        if (!['pdf', 'pptx', 'docx', 'txt', 'md'].includes(ext)) {
            aviso(ext === 'ppt' || ext === 'doc'
                ? 'Ese formato es antiguo: guárdalo como .pptx o .docx.'
                : 'Formato no soportado. Usa PDF, PowerPoint (.pptx), Word (.docx) o .txt.');
            return;
        }
        if (f.size > 15 * 1024 * 1024) { aviso('El archivo pesa más de 15 MB.'); return; }
        archivo = f;
        $('nombre-archivo').textContent = f.name;
        zona.classList.add('listo');
        $('tutor-generar').disabled = false;
    }

    inputArchivo.addEventListener('change', () => elegir(inputArchivo.files[0]));
    ['dragenter', 'dragover'].forEach(ev => zona.addEventListener(ev, e => { e.preventDefault(); zona.classList.add('arrastrando'); }));
    ['dragleave', 'drop'].forEach(ev => zona.addEventListener(ev, e => { e.preventDefault(); zona.classList.remove('arrastrando'); }));
    zona.addEventListener('drop', e => elegir(e.dataTransfer.files[0]));

    // ---------- 2. Generar la primera ronda ----------

    $('tutor-generar').addEventListener('click', async () => {
        if (!archivo) return;
        estado.dificultad = panel.querySelector('input[name="tutor-dif"]:checked').value;

        const datos = new FormData();
        datos.append('archivo', archivo);
        datos.append('cantidad', $('tutor-cantidad').value);
        datos.append('dificultad', estado.dificultad);
        datos.append('__RequestVerificationToken', token);

        cargando(['Leyendo tu documento…', 'Identificando los temas clave…', 'Redactando las preguntas…', 'Casi listo…']);
        try {
            const r = await enviar(urls.urlSubir, datos);
            Object.assign(estado, {
                documentoId: r.documentoId, nombre: r.nombre, ronda: 0,
                preguntas: [], totalCorrectas: 0, totalCalificadas: 0
            });
            $('doc-nombre').textContent = r.nombre;
            $('lista-preguntas').innerHTML = '';
            $('resumen-total').hidden = true;
            agregarPreguntas(r.preguntas);
            paso('quiz');
        } catch (err) {
            paso('subir');
            aviso(err.message);
        } finally {
            finCarga();
        }
    });

    // ---------- 3. Mostrar preguntas ----------

    const letras = ['A', 'B', 'C', 'D', 'E', 'F'];

    function agregarPreguntas(nuevas) {
        const lista = $('lista-preguntas');
        const inicio = estado.preguntas.length;

        if (estado.ronda > 0) {
            lista.insertAdjacentHTML('beforeend', `<div class="tutor-separador">Ronda ${estado.ronda + 1}</div>`);
        }

        nuevas.forEach((p, k) => {
            const i = inicio + k;
            estado.preguntas.push({ ...p, elegida: null, calificada: false, ronda: estado.ronda });
            const opciones = p.opciones.map((o, j) => `
                <button type="button" class="tutor-opcion" data-p="${i}" data-o="${j}" aria-pressed="false">
                    <span class="letra">${letras[j]}</span><span class="texto">${escapar(o)}</span>
                </button>`).join('');
            lista.insertAdjacentHTML('beforeend', `
                <article class="tutor-pregunta" id="pregunta-${i}">
                    <div class="tutor-pregunta-top">
                        <span class="num">${i + 1}</span>
                        ${p.tema ? `<span class="tema">${escapar(p.tema)}</span>` : ''}
                    </div>
                    <h4 class="enunciado">${escapar(p.enunciado)}</h4>
                    <div class="opciones">${opciones}</div>
                    <div class="retro" hidden></div>
                </article>`);
        });

        lista.querySelectorAll('.tutor-opcion').forEach(b => {
            if (b.dataset.listo) return;
            b.dataset.listo = '1';
            b.addEventListener('click', () => elegirOpcion(+b.dataset.p, +b.dataset.o));
        });

        $('tutor-calificar').hidden = false;
        $('tutor-mas').hidden = true;
        actualizarProgreso();
        document.getElementById(`pregunta-${inicio}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function elegirOpcion(i, j) {
        const p = estado.preguntas[i];
        if (p.calificada) return;
        p.elegida = j;
        document.querySelectorAll(`#pregunta-${i} .tutor-opcion`).forEach(b => {
            const marcada = +b.dataset.o === j;
            b.classList.toggle('elegida', marcada);
            b.setAttribute('aria-pressed', marcada);
        });
        actualizarProgreso();
    }

    function pendientes() { return estado.preguntas.filter(p => !p.calificada); }

    function actualizarProgreso() {
        const ronda = pendientes();
        const respondidas = ronda.filter(p => p.elegida !== null).length;
        $('progreso-texto').textContent = ronda.length
            ? `${respondidas} de ${ronda.length} respondidas`
            : 'Ronda calificada';
        $('progreso-barra').style.width = ronda.length ? `${respondidas * 100 / ronda.length}%` : '100%';
        $('tutor-calificar').disabled = respondidas === 0;
    }

    // ---------- 4. Calificar ----------

    $('tutor-calificar').addEventListener('click', async () => {
        const ronda = pendientes();
        const respondidas = ronda.filter(p => p.elegida !== null);
        if (respondidas.length < ronda.length &&
            !confirm(`Te faltan ${ronda.length - respondidas.length} preguntas. ¿Calificar de todas formas? Las que no respondiste cuentan como incorrectas.`)) return;

        let correctas = 0;
        ronda.forEach(p => {
            const i = estado.preguntas.indexOf(p);
            const acierto = p.elegida === p.correcta;
            if (acierto) correctas++;
            p.calificada = true;

            const tarjeta = $(`pregunta-${i}`);
            tarjeta.classList.add(acierto ? 'acierto' : 'error');
            tarjeta.querySelectorAll('.tutor-opcion').forEach(b => {
                const o = +b.dataset.o;
                b.disabled = true;
                if (o === p.correcta) b.classList.add('correcta');
                else if (o === p.elegida) b.classList.add('incorrecta');
            });

            const retro = tarjeta.querySelector('.retro');
            retro.hidden = false;
            retro.innerHTML = `
                <p class="veredicto">${acierto ? '<i class="bi bi-check-circle-fill"></i> ¡Correcto!'
                    : p.elegida === null ? '<i class="bi bi-dash-circle-fill"></i> Sin responder'
                    : '<i class="bi bi-x-circle-fill"></i> Incorrecto'}</p>
                <p class="mb-2">${escapar(p.explicacion)}</p>
                <button type="button" class="btn btn-link btn-sm p-0 profundizar" data-p="${i}">
                    <i class="bi bi-lightbulb"></i> ${acierto ? 'Saber más de este tema' : 'No lo entendí, explícamelo'}
                </button>`;
            retro.querySelector('.profundizar').addEventListener('click', () => profundizarEn(p));
        });

        estado.totalCorrectas += correctas;
        estado.totalCalificadas += ronda.length;
        mostrarResultado(correctas, ronda.length);

        $('tutor-calificar').hidden = true;
        $('tutor-mas').hidden = false;
        actualizarProgreso();

        // Se guarda el resultado; si falla, el alumno igual ve su puntaje
        try {
            await enviar(urls.urlResultado, {
                documento: estado.nombre,
                dificultad: estado.dificultad,
                correctas,
                total: ronda.length,
                cursoId: +(document.getElementById('cursoId')?.value || 0) || null
            });
            agregarAlHistorial(correctas, ronda.length);
        } catch { /* sin conexión: no se guarda */ }
    });

    function mostrarResultado(correctas, total) {
        const pct = Math.round(correctas * 100 / total);
        const clase = pct >= 70 ? 'bien' : pct >= 50 ? 'regular' : 'mal';
        const mensaje = pct === 100 ? '¡Perfecto! Dominas este material.'
            : pct >= 70 ? '¡Muy bien! Vas por buen camino.'
            : pct >= 50 ? 'Bien, pero repasa los temas que fallaste.'
            : 'Necesitas repasar. Usa "Profundizar" en las preguntas que fallaste.';
        const global = estado.totalCalificadas > total
            ? `<span class="small text-secondary d-block">Acumulado: ${estado.totalCorrectas} de ${estado.totalCalificadas}</span>` : '';

        const resumen = $('resumen-total');
        resumen.hidden = false;
        resumen.className = `tutor-total ${clase}`;
        resumen.innerHTML = `
            <div class="anillo" style="--pct:${pct}"><span>${pct}%</span></div>
            <div>
                <p class="fw-bold mb-1">${correctas} de ${total} correctas</p>
                <p class="small mb-0">${mensaje}</p>
                ${global}
            </div>`;
        resumen.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function agregarAlHistorial(correctas, total) {
        const lista = $('lista-resultados');
        lista.querySelector('.vacio')?.remove();
        const pct = Math.round(correctas * 100 / total);
        const clase = pct >= 70 ? 'bien' : pct >= 50 ? 'regular' : 'mal';
        const ahora = new Date();
        const hora = `${String(ahora.getDate()).padStart(2, '0')}/${String(ahora.getMonth() + 1).padStart(2, '0')} ${String(ahora.getHours()).padStart(2, '0')}:${String(ahora.getMinutes()).padStart(2, '0')}`;
        lista.insertAdjacentHTML('afterbegin', `
            <li><span class="tutor-pct ${clase}">${pct}%</span>
                <span class="flex-grow-1 text-truncate">
                    <span class="d-block text-truncate fw-semibold">${escapar(estado.nombre)}</span>
                    <span class="small text-secondary">${correctas}/${total} · ${escapar(estado.dificultad)} · ${hora}</span>
                </span></li>`);
    }

    // ---------- 5. Más preguntas ----------

    $('tutor-mas').addEventListener('click', async () => {
        const boton = $('tutor-mas');
        boton.disabled = true;
        boton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Generando…';
        try {
            const r = await enviar(urls.urlMas, {
                documentoId: estado.documentoId,
                ronda: estado.ronda + 1,
                cantidad: +$('tutor-cantidad').value,
                dificultad: estado.dificultad,
                yaHechas: estado.preguntas.map(p => p.enunciado)
            });
            estado.ronda++;
            agregarPreguntas(r.preguntas);
        } catch (err) {
            aviso(err.message);
        } finally {
            boton.disabled = false;
            boton.innerHTML = '<i class="bi bi-plus-circle"></i> Generar más preguntas';
        }
    });

    $('tutor-nuevo').addEventListener('click', () => {
        if (pendientes().some(p => p.elegida !== null) && !confirm('Tienes respuestas sin calificar. ¿Subir otro documento?')) return;
        archivo = null;
        inputArchivo.value = '';
        $('nombre-archivo').textContent = 'Arrastra tu documento o haz clic aquí';
        zona.classList.remove('listo');
        $('tutor-generar').disabled = true;
        paso('subir');
    });

    // ---------- 6. Profundizar (chat con el tutor) ----------

    function burbuja(rol, html) {
        const div = document.createElement('div');
        div.className = `burbuja ${rol}`;
        div.innerHTML = html;
        $('chat-mensajes').appendChild(div);
        div.scrollIntoView({ behavior: 'smooth', block: 'end' });
        return div;
    }

    function enlacesTema(tema) {
        const q = encodeURIComponent(tema);
        return `
            <div class="tutor-tema">
                <span class="nombre">${escapar(tema)}</span>
                <span class="enlaces">
                    <button type="button" class="preguntar" data-tema="${escapar(tema)}" title="Preguntar al tutor"><i class="bi bi-chat-dots"></i></button>
                    <a href="https://www.google.com/search?q=${q}" target="_blank" rel="noopener" title="Buscar en Google"><i class="bi bi-google"></i></a>
                    <a href="https://www.youtube.com/results?search_query=${q}" target="_blank" rel="noopener" title="Ver videos en YouTube"><i class="bi bi-youtube"></i></a>
                    <a href="https://es.wikipedia.org/w/index.php?search=${q}" target="_blank" rel="noopener" title="Leer en Wikipedia"><i class="bi bi-wikipedia"></i></a>
                </span>
            </div>`;
    }

    let enviando = false;
    async function preguntarTutor(texto) {
        texto = texto.trim();
        if (!texto || enviando) return;
        enviando = true;
        $('chat-sugerencias').hidden = true;

        burbuja('yo', `<p>${escapar(texto)}</p>`);
        const pensando = burbuja('bot', '<span class="escribiendo"><span></span><span></span><span></span></span>');

        try {
            const r = await enviar(urls.urlProfundizar, {
                documentoId: estado.documentoId,
                mensaje: texto,
                historial: estado.historial
            });
            pensando.innerHTML = markdown(r.respuesta) +
                (r.temas?.length ? `<div class="tutor-temas"><span class="tutor-etq">Para seguir aprendiendo</span>${r.temas.map(enlacesTema).join('')}</div>` : '');
            pensando.querySelectorAll('.preguntar').forEach(b =>
                b.addEventListener('click', () => preguntarTutor(`Explícame más sobre: ${b.dataset.tema}`)));

            estado.historial.push({ rol: 'user', texto }, { rol: 'assistant', texto: r.respuesta });
            if (estado.historial.length > 16) estado.historial = estado.historial.slice(-16);
        } catch (err) {
            pensando.innerHTML = `<p class="text-danger mb-0"><i class="bi bi-exclamation-triangle"></i> ${escapar(err.message)}</p>`;
        } finally {
            enviando = false;
        }
    }

    function profundizarEn(p) {
        mostrarTab('chat');
        const elegida = p.elegida === null ? 'no respondí' : `respondí "${p.opciones[p.elegida]}"`;
        preguntarTutor(`En la pregunta "${p.enunciado}" ${elegida} y la respuesta correcta es "${p.opciones[p.correcta]}". ` +
            `Explícame el tema "${p.tema || 'de la pregunta'}" con un ejemplo sencillo.`);
    }

    $('chat-form').addEventListener('submit', e => {
        e.preventDefault();
        const texto = $('chat-texto').value;
        $('chat-texto').value = '';
        ajustarAltura();
        preguntarTutor(texto);
    });

    // Enter envía, Shift + Enter hace salto de línea
    $('chat-texto').addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); $('chat-form').requestSubmit(); }
    });

    function ajustarAltura() {
        const t = $('chat-texto');
        t.style.height = 'auto';
        t.style.height = Math.min(t.scrollHeight, 120) + 'px';
    }
    $('chat-texto').addEventListener('input', ajustarAltura);

    panel.querySelectorAll('.chip-sug').forEach(b => b.addEventListener('click', () => preguntarTutor(b.textContent)));
})();
