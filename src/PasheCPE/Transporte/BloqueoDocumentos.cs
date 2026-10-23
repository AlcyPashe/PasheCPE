namespace PasheCPE.Transporte;

/// <summary>
/// Bloqueo por documento dentro del proceso (TRD §14): impide enviar o consultar al mismo tiempo el mismo comprobante.
/// Un semáforo por nombre de archivo, que se libera cuando nadie lo usa (la memoria no crece).
/// </summary>
internal sealed class BloqueoDocumentos
{
    private readonly Dictionary<string, Entrada> _entradas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _candado = new();

    public async Task<IDisposable> AdquirirAsync(string nombre, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Entrada entrada;
        lock (_candado)
        {
            if (!_entradas.TryGetValue(nombre, out entrada!))
            {
                entrada = new Entrada();
                _entradas[nombre] = entrada;
            }

            entrada.Referencias++;
        }

        try
        {
            await entrada.Semaforo.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            Soltar(nombre, entrada);
            throw;
        }

        return new Liberador(this, nombre, entrada);
    }

    /// <summary>Documentos con bloqueo tomado o en espera (para pruebas).</summary>
    internal int Activos
    {
        get
        {
            lock (_candado)
            {
                return _entradas.Count;
            }
        }
    }

    private void Soltar(string nombre, Entrada entrada)
    {
        lock (_candado)
        {
            if (--entrada.Referencias == 0)
            {
                _entradas.Remove(nombre);
                entrada.Semaforo.Dispose();
            }
        }
    }

    private sealed class Entrada
    {
        public SemaphoreSlim Semaforo { get; } = new(1, 1);

        public int Referencias { get; set; }
    }

    private sealed class Liberador(BloqueoDocumentos bloqueo, string nombre, Entrada entrada) : IDisposable
    {
        private int _liberado;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _liberado, 1) == 0)
            {
                entrada.Semaforo.Release();
                bloqueo.Soltar(nombre, entrada);
            }
        }
    }
}
