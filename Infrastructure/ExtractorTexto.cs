using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace PlataformaCursos.Infrastructure;

// Saca el texto de los documentos que sube el alumno: PDF, PowerPoint (.pptx), Word (.docx) y texto plano
public static class ExtractorTexto
{
    public const long TamanoMaximo = 15 * 1024 * 1024;   // 15 MB
    public const int CaracteresMaximos = 150_000;

    public static readonly string[] Extensiones = { ".pdf", ".pptx", ".docx", ".txt", ".md" };

    public static async Task<string> ExtraerAsync(IFormFile archivo)
    {
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

        if (extension is ".ppt" or ".doc")
            throw new GroqException("Ese formato es antiguo. Ábrelo en PowerPoint o Word y guárdalo como .pptx o .docx.");
        if (!Extensiones.Contains(extension))
            throw new GroqException("Formato no soportado. Sube un PDF, PowerPoint (.pptx), Word (.docx) o .txt.");
        if (archivo.Length > TamanoMaximo)
            throw new GroqException("El archivo pesa más de 15 MB.");

        // Se copia a memoria porque PdfPig y ZipArchive necesitan poder moverse por el archivo
        await using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria);
        memoria.Position = 0;

        var texto = extension switch
        {
            ".pdf" => DePdf(memoria),
            ".pptx" => DeOffice(memoria, "ppt/slides/slide", separador: "\n\n--- Diapositiva ---\n"),
            ".docx" => DeOffice(memoria, "word/document", separador: "\n"),
            _ => Encoding.UTF8.GetString(memoria.ToArray())
        };

        texto = Regex.Replace(texto, @"[ \t]+", " ");
        texto = Regex.Replace(texto, @"\n{3,}", "\n\n").Trim();

        if (texto.Length < 80)
            throw new GroqException("No se encontró texto suficiente en el documento. Si es un PDF escaneado (solo imágenes), no se puede leer.");

        return texto.Length > CaracteresMaximos ? texto[..CaracteresMaximos] : texto;
    }

    private static string DePdf(Stream stream)
    {
        var sb = new StringBuilder();
        try
        {
            using var pdf = PdfDocument.Open(stream);
            foreach (var pagina in pdf.GetPages())
            {
                sb.AppendLine(pagina.Text);
                if (sb.Length > CaracteresMaximos) break;
            }
        }
        catch (Exception ex) when (ex is not GroqException)
        {
            throw new GroqException("No se pudo leer el PDF. Puede estar dañado o protegido con contraseña.");
        }
        return sb.ToString();
    }

    // Los .pptx y .docx son archivos ZIP con XML adentro; el texto está en las etiquetas <a:t> o <w:t>
    private static string DeOffice(Stream stream, string prefijo, string separador)
    {
        var sb = new StringBuilder();
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var partes = zip.Entries
                .Where(e => e.FullName.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml"))
                .OrderBy(e => NumeroDe(e.FullName));

            foreach (var parte in partes)
            {
                using var contenido = parte.Open();
                var xml = XDocument.Load(contenido);
                sb.Append(separador);

                // Cada párrafo (<a:p> o <w:p>) en su propia línea
                foreach (var parrafo in xml.Descendants().Where(x => x.Name.LocalName == "p" && x.Name.Namespace.NamespaceName.Contains("main")))
                {
                    var linea = string.Concat(parrafo.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
                    if (!string.IsNullOrWhiteSpace(linea)) sb.AppendLine(linea);
                }
                if (sb.Length > CaracteresMaximos) break;
            }
        }
        catch (Exception ex) when (ex is not GroqException)
        {
            throw new GroqException("No se pudo leer el archivo. Verifica que no esté dañado.");
        }
        return sb.ToString();
    }

    private static int NumeroDe(string ruta)
    {
        var m = Regex.Match(ruta, @"(\d+)\.xml$");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    // Toma un fragmento distinto del documento en cada ronda, para que "Generar más" cubra todo el material
    public static string Fragmento(string texto, int ronda, int largo = 12_000)
    {
        if (texto.Length <= largo) return texto;
        var inicio = (ronda * largo) % texto.Length;
        var fin = Math.Min(texto.Length, inicio + largo);
        var trozo = texto[inicio..fin];
        if (trozo.Length < largo / 2) trozo += "\n" + texto[..(largo - trozo.Length)];
        return trozo;
    }
}
