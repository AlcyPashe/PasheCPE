<?xml version="1.0" encoding="UTF-8"?>
<!--
  Utilidades de validación de PasheCPE para las XSL de SUNAT (TRD §9.3). Reemplazan a local:///commons/error/validate_utils.xsl
  de DataPower, que SUNAT no publica: mismas plantillas y parámetros, pero en lugar de dp:reject (que detiene el proceso)
  emiten un nodo pcpe:resultado por cada error u observación, así se informan todos de una sola pasada, en el orden en que
  se ejecutan (el primero es el que devolvería SUNAT).

  Las funciones pcpe:* las implementa ExtensionesPasheCpe (catálogos y ruta del nodo) y regexp:* ExtensionesExsltRegexp.
-->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:regexp="http://exslt.org/regular-expressions"
    xmlns:pcpe="urn:pashecpe:xsl"
    exclude-result-prefixes="regexp">

    <!-- Formatos numéricos de SUNAT: n(12,2), n(12,3) y n(12,10). -->
    <xsl:variable name="pcpeDosDecimales" select="'^[0-9]{1,12}(\.[0-9]{1,2})?$'"/>
    <xsl:variable name="pcpeTresDecimales" select="'^[0-9]{1,12}(\.[0-9]{1,3})?$'"/>
    <xsl:variable name="pcpeDiezDecimales" select="'^[0-9]{1,12}(\.[0-9]{1,10})?$'"/>

    <!-- Emite el resultado: tipo «error» u «observacion» (isError = false()). -->
    <xsl:template name="pcpeReportar">
        <xsl:param name="codigo"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="node"/>
        <xsl:param name="descripcion"/>
        <xsl:param name="line"/>
        <pcpe:resultado codigo="{$codigo}" ruta="{pcpe:ruta($node)}">
            <xsl:attribute name="tipo">
                <xsl:choose>
                    <xsl:when test="$isError">error</xsl:when>
                    <xsl:otherwise>observacion</xsl:otherwise>
                </xsl:choose>
            </xsl:attribute>
            <xsl:attribute name="detalle">
                <xsl:value-of select="$descripcion"/>
                <xsl:if test="string($line) != ''">
                    <xsl:value-of select="concat(' (línea ', $line, ')')"/>
                </xsl:if>
            </xsl:attribute>
        </pcpe:resultado>
    </xsl:template>

    <!-- Reporta si la expresión es verdadera. -->
    <xsl:template name="isTrueExpresion">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="expresion"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:param name="line"/>
        <xsl:if test="$expresion = true()">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$node"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
                <xsl:with-param name="line" select="$line"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Reporta si el nodo existe y la expresión es verdadera. -->
    <xsl:template name="isTrueExpresionIfExist">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="expresion"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:param name="line"/>
        <xsl:if test="$node and $expresion = true()">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$node"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
                <xsl:with-param name="line" select="$line"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Reporta si el nodo existe y su valor no cumple la expresión regular. -->
    <xsl:template name="regexpValidateElementIfExist">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="regexp"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:param name="line"/>
        <xsl:if test="$node and not(regexp:test($node, $regexp))">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$node"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
                <xsl:with-param name="line" select="$line"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Reporta si el nodo no existe. Con «expresion», solo cuando esta es verdadera. -->
    <xsl:template name="existElement">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="node"/>
        <xsl:param name="expresion" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="not($node) and $expresion = true()">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeNotExist"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$node"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Reporta si el nodo no existe o está vacío. -->
    <xsl:template name="existElementNoVacio">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="node"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="not($node) or normalize-space(string($node)) = ''">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeNotExist"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$node"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Si el nodo no existe reporta errorCodeNotExist; si no cumple la expresión regular, errorCodeValidate. -->
    <xsl:template name="existAndRegexpValidateElement">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="regexp"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:choose>
            <xsl:when test="not($node)">
                <xsl:call-template name="pcpeReportar">
                    <xsl:with-param name="codigo" select="$errorCodeNotExist"/>
                    <xsl:with-param name="isError" select="$isError"/>
                    <xsl:with-param name="node" select="$node"/>
                    <xsl:with-param name="descripcion" select="$descripcion"/>
                </xsl:call-template>
            </xsl:when>
            <xsl:when test="not(regexp:test($node, $regexp))">
                <xsl:call-template name="pcpeReportar">
                    <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                    <xsl:with-param name="isError" select="$isError"/>
                    <xsl:with-param name="node" select="$node"/>
                    <xsl:with-param name="descripcion" select="$descripcion"/>
                </xsl:call-template>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <!-- Valida el formato decimal y, si isGreaterCero, que el valor sea mayor que cero. -->
    <xsl:template name="pcpeValidarDecimal">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="formato"/>
        <xsl:param name="obligatorio"/>
        <xsl:param name="isGreaterCero"/>
        <xsl:param name="isError"/>
        <xsl:param name="descripcion"/>
        <xsl:choose>
            <xsl:when test="not($node)">
                <xsl:if test="$obligatorio">
                    <xsl:call-template name="pcpeReportar">
                        <xsl:with-param name="codigo" select="$errorCodeNotExist"/>
                        <xsl:with-param name="isError" select="$isError"/>
                        <xsl:with-param name="node" select="$node"/>
                        <xsl:with-param name="descripcion" select="$descripcion"/>
                    </xsl:call-template>
                </xsl:if>
            </xsl:when>
            <xsl:when test="not(regexp:test($node, $formato)) or ($isGreaterCero and not(number($node) &gt; 0))">
                <xsl:call-template name="pcpeReportar">
                    <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                    <xsl:with-param name="isError" select="$isError"/>
                    <xsl:with-param name="node" select="$node"/>
                    <xsl:with-param name="descripcion" select="$descripcion"/>
                </xsl:call-template>
            </xsl:when>
        </xsl:choose>
    </xsl:template>

    <xsl:template name="existAndValidateValueTwoDecimal">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="isGreaterCero" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="pcpeValidarDecimal">
            <xsl:with-param name="errorCodeNotExist" select="$errorCodeNotExist"/>
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="node" select="$node"/>
            <xsl:with-param name="formato" select="$pcpeDosDecimales"/>
            <xsl:with-param name="obligatorio" select="true()"/>
            <xsl:with-param name="isGreaterCero" select="$isGreaterCero"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template name="validateValueTwoDecimalIfExist">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="isGreaterCero" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="pcpeValidarDecimal">
            <xsl:with-param name="errorCodeNotExist" select="$errorCodeNotExist"/>
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="node" select="$node"/>
            <xsl:with-param name="formato" select="$pcpeDosDecimales"/>
            <xsl:with-param name="obligatorio" select="false()"/>
            <xsl:with-param name="isGreaterCero" select="$isGreaterCero"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template name="validateValueThreeDecimalIfExist">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="isGreaterCero" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="pcpeValidarDecimal">
            <xsl:with-param name="errorCodeNotExist" select="$errorCodeNotExist"/>
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="node" select="$node"/>
            <xsl:with-param name="formato" select="$pcpeTresDecimales"/>
            <xsl:with-param name="obligatorio" select="false()"/>
            <xsl:with-param name="isGreaterCero" select="$isGreaterCero"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template name="existAndValidateValueTenDecimal">
        <xsl:param name="errorCodeNotExist"/>
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="node"/>
        <xsl:param name="isGreaterCero" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="pcpeValidarDecimal">
            <xsl:with-param name="errorCodeNotExist" select="$errorCodeNotExist"/>
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="node" select="$node"/>
            <xsl:with-param name="formato" select="$pcpeDiezDecimales"/>
            <xsl:with-param name="obligatorio" select="true()"/>
            <xsl:with-param name="isGreaterCero" select="$isGreaterCero"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <!--
      Reporta si el código no está en el catálogo (vigente a la fecha de recepción). Con «expresion», solo cuando esta es
      verdadera. Un código ausente o vacío no se reporta aquí (lo controlan las reglas de existencia): SUNAT acepta sin
      observación 4332 las líneas sin código de producto.
    -->
    <xsl:template name="findElementInCatalog">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="idCatalogo"/>
        <xsl:param name="catalogo"/>
        <xsl:param name="expresion" select="true()"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="string($idCatalogo) != '' and $expresion = true() and not(pcpe:enCatalogo(string($catalogo), string($idCatalogo)))">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$idCatalogo"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!--
      Reporta si la propiedad del código en el catálogo es distinta del valor esperado (o si el código no existe). Igual
      que findElementInCatalog, un código ausente o vacío no se reporta.
    -->
    <xsl:template name="findElementInCatalogProperty">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="idCatalogo"/>
        <xsl:param name="catalogo"/>
        <xsl:param name="propiedad"/>
        <xsl:param name="valorPropiedad"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="string($idCatalogo) != '' and not(pcpe:propiedadCatalogo(string($catalogo), string($idCatalogo), string($propiedad)) = $valorPropiedad)">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$idCatalogo"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!--
      Variantes de findElementInCatalogProperty que usan las XSL GRE 2.0 (01/10/2026): la propiedad «gre-r»/«gre-t» del
      N.° 61 (documento relacionado aplicable a la guía remitente o transportista, columna «GRE Aplicable») y el ubigeo de
      un puerto o aeropuerto (N.° 63/64) frente al ubigeo de partida. Mismos parámetros y mismo comportamiento.
    -->
    <xsl:template name="findElementInCatalog61rProperty">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="idCatalogo"/>
        <xsl:param name="catalogo"/>
        <xsl:param name="propiedad"/>
        <xsl:param name="valorPropiedad"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="findElementInCatalogProperty">
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="idCatalogo" select="$idCatalogo"/>
            <xsl:with-param name="catalogo" select="$catalogo"/>
            <xsl:with-param name="propiedad" select="$propiedad"/>
            <xsl:with-param name="valorPropiedad" select="$valorPropiedad"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template name="findElementInCatalog61tProperty">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="idCatalogo"/>
        <xsl:param name="catalogo"/>
        <xsl:param name="propiedad"/>
        <xsl:param name="valorPropiedad"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="findElementInCatalogProperty">
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="idCatalogo" select="$idCatalogo"/>
            <xsl:with-param name="catalogo" select="$catalogo"/>
            <xsl:with-param name="propiedad" select="$propiedad"/>
            <xsl:with-param name="valorPropiedad" select="$valorPropiedad"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <xsl:template name="findElementInCatalogGREUbigeoProperty">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="idCatalogo"/>
        <xsl:param name="catalogo"/>
        <xsl:param name="propiedad"/>
        <xsl:param name="valorPropiedad"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:call-template name="findElementInCatalogProperty">
            <xsl:with-param name="errorCodeValidate" select="$errorCodeValidate"/>
            <xsl:with-param name="idCatalogo" select="$idCatalogo"/>
            <xsl:with-param name="catalogo" select="$catalogo"/>
            <xsl:with-param name="propiedad" select="$propiedad"/>
            <xsl:with-param name="valorPropiedad" select="$valorPropiedad"/>
            <xsl:with-param name="isError" select="$isError"/>
            <xsl:with-param name="descripcion" select="$descripcion"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Rechazo incondicional. -->
    <xsl:template name="rejectCall">
        <xsl:param name="errorCode"/>
        <xsl:param name="errorMessage"/>
        <xsl:call-template name="pcpeReportar">
            <xsl:with-param name="codigo" select="$errorCode"/>
            <xsl:with-param name="descripcion" select="$errorMessage"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Observación incondicional. -->
    <xsl:template name="addWarning">
        <xsl:param name="warningCode"/>
        <xsl:param name="warningMessage"/>
        <xsl:call-template name="pcpeReportar">
            <xsl:with-param name="codigo" select="$warningCode"/>
            <xsl:with-param name="isError" select="false()"/>
            <xsl:with-param name="descripcion" select="$warningMessage"/>
        </xsl:call-template>
    </xsl:template>

    <!-- Reporta si la fecha de inicio es anterior a la fecha final (yyyy-MM-dd). -->
    <xsl:template name="isDateBefore">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="startDateNode"/>
        <xsl:param name="endDateNode"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="$startDateNode and $endDateNode and number(translate($startDateNode, '-', '')) &lt; number(translate($endDateNode, '-', ''))">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$startDateNode"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

    <!-- Reporta si la fecha es posterior a hoy en Lima (yyyy-MM-dd). -->
    <xsl:template name="isDateAfterToday">
        <xsl:param name="errorCodeValidate"/>
        <xsl:param name="startDateNode"/>
        <xsl:param name="isError" select="true()"/>
        <xsl:param name="descripcion"/>
        <xsl:if test="$startDateNode and number(translate($startDateNode, '-', '')) &gt; number(translate(pcpe:hoy(), '-', ''))">
            <xsl:call-template name="pcpeReportar">
                <xsl:with-param name="codigo" select="$errorCodeValidate"/>
                <xsl:with-param name="isError" select="$isError"/>
                <xsl:with-param name="node" select="$startDateNode"/>
                <xsl:with-param name="descripcion" select="$descripcion"/>
            </xsl:call-template>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
