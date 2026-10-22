using System.Text.Json.Serialization;

namespace PasheCPE;

// Las claves nunca se serializan ([JsonIgnore]) y ToString() las enmascara: el ToString() que C# genera para un
// record imprimiría todas las propiedades, y un registro de log con el emisor expondría la clave.

/// <summary>Usuario y clave del servicio SOAP: usuario secundario SOL (sin el RUC) o usuario asignado por el OSE.</summary>
public sealed record CredencialesSoap
{
    /// <summary>Crea las credenciales SOAP.</summary>
    public CredencialesSoap(string usuario, string clave)
    {
        Usuario = usuario;
        Clave = clave;
    }

    /// <summary>Usuario secundario SOL (sin el RUC) o usuario del OSE.</summary>
    public string Usuario { get; init; }

    /// <summary>Clave del usuario. Nunca se serializa ni se escribe en logs.</summary>
    [JsonIgnore]
    public string Clave { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"CredencialesSoap {{ Usuario = {Usuario}, Clave = *** }}";
}

/// <summary>Credenciales de la API REST de SUNAT para guías de remisión (OAuth2).</summary>
public sealed record CredencialesGre
{
    /// <summary>Crea las credenciales GRE.</summary>
    public CredencialesGre(string clientId, string clientSecret, string usuarioSol, string claveSol)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
        UsuarioSol = usuarioSol;
        ClaveSol = claveSol;
    }

    /// <summary>client_id de la aplicación registrada en SUNAT.</summary>
    public string ClientId { get; init; }

    /// <summary>client_secret de la aplicación. Nunca se serializa ni se escribe en logs.</summary>
    [JsonIgnore]
    public string ClientSecret { get; init; }

    /// <summary>Usuario secundario SOL (sin el RUC).</summary>
    public string UsuarioSol { get; init; }

    /// <summary>Clave SOL. Nunca se serializa ni se escribe en logs.</summary>
    [JsonIgnore]
    public string ClaveSol { get; init; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"CredencialesGre {{ ClientId = {ClientId}, ClientSecret = ***, UsuarioSol = {UsuarioSol}, ClaveSol = *** }}";
}

/// <summary>Certificado digital del emisor: archivo .pfx en la carpeta CERT del almacén y su clave.</summary>
public sealed record CertificadoCpe
{
    /// <summary>Crea la referencia al certificado.</summary>
    public CertificadoCpe(string archivo, string clave)
    {
        Archivo = archivo;
        Clave = clave;
    }

    /// <summary>Nombre del archivo .pfx dentro de la carpeta CERT (sin ruta).</summary>
    public string Archivo { get; init; }

    /// <summary>Clave del certificado. Nunca se serializa ni se escribe en logs.</summary>
    [JsonIgnore]
    public string Clave { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"CertificadoCpe {{ Archivo = {Archivo}, Clave = *** }}";
}
