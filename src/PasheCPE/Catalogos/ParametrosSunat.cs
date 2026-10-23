namespace PasheCPE.Catalogos;

/// <summary>Parámetros legales de SUNAT que no son tasas ni plazos (<c>parametros.json</c>).</summary>
internal sealed class ParametrosSunat
{
    private readonly ArchivoParametros _datos = RecursosEmbebidos.Leer("parametros.json", DatosJson.Default.ArchivoParametros);

    /// <summary>Importe de una boleta (en soles) hasta el cual no se exige identificar al cliente.</summary>
    public decimal BoletaSinIdentificacionHasta => _datos.BoletaSinIdentificacionHasta;

    /// <summary>Fecha desde la que se informa el contrato de colaboración empresarial (R.S. 000048-2026).</summary>
    public DateOnly ContratoColaboracionDesde => _datos.ContratoColaboracionDesde;
}
