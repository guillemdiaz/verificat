using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Verificat.Models;

namespace Verificat;

internal class InvoiceManager
{
    private readonly string _connectionString;
    private readonly InvoiceRepository _repository;

    private const string GenesisHash =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public InvoiceManager(string connectionString)
    {
        _connectionString = connectionString;
        _repository = new InvoiceRepository(connectionString);
    }

    public InvoiceResult CreateInvoice(Invoice invoice)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            string previousHash = _repository.GetLastHash(connection, transaction);
            if (string.IsNullOrEmpty(previousHash))
                previousHash = GenesisHash;

            bool isFirst = previousHash == GenesisHash;

            string payload = BuildPayload(invoice, previousHash);
            invoice.PrimerRegistre = isFirst ? "S" : "N";
            invoice.EmpremtaAnterior = previousHash;
            invoice.Empremta = GenerateSha256Hash(payload);

            _repository.Insert(connection, transaction, invoice);
            transaction.Commit();

            return new InvoiceResult
            {
                Success = true,
                Message = $"Factura {invoice.SerieFactura}-{invoice.NumeroFactura} creada.",
                Empremta = invoice.Empremta
            };
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return new InvoiceResult
            {
                Success = false,
                Message = $"La factura {invoice.SerieFactura}-{invoice.NumeroFactura} ja existeix."
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new InvoiceResult
            {
                Success = false,
                Message = $"    [INFO] {ex.Message}"
            };
        }
    }

    public ChainVerificationResult VerifyChain()
    {
        var invoices = _repository.GetAllOrdered();
        var errors = new List<string>();

        if (invoices.Count == 0)
            return new ChainVerificationResult(false, 0,
                new List<string> { "No hi ha factures a la base de dades." }
            );


        // La primera factura sempre ha d'apuntar al Genesis Hash (tot zeros)
        string expectedPreviousHash = GenesisHash;

        // Recorre totes les factures en ordre ascendent verificant que:
        // - Cada factura apunti a l'empremta de l'anterior
        // - L'empremta de cada factura coincideixi amb el recàlcul a partir de les seves dades
        foreach (var invoice in invoices)
        {
            // Verifica que l'encadenament és seqüencial i sense salts.
            if (invoice.EmpremtaAnterior != expectedPreviousHash)
                errors.Add($"ID {invoice.Id} - cadena trencada (EmpremtaAnterior no coincideix).");


            // Torna a calcular l'empremta (hash) a partir de les dades de la factura actual
            // i l'empremta anterior.
            string payload = BuildPayload(invoice, invoice.EmpremtaAnterior);
            string recalculated = GenerateSha256Hash(payload);

            // Si l'empremta recalculada no és exactament igual a la que hi ha guardada
            // a la base de dades, vol dir que s'ha modificat algun camp de la factura.
            if (recalculated != invoice.Empremta)
                errors.Add($"ID {invoice.Id} - manipulació detectada (empremta no coincideix).");

            // La factura següent haurà d'apuntar a l'empremta d'aquesta factura.
            expectedPreviousHash = invoice.Empremta;
        }

        return new ChainVerificationResult(
            IsValid: errors.Count == 0,
            TotalInvoices: invoices.Count,
            Errors: errors
        );
    }

    public (bool Success, string Message) SimulateTampering(int id, string mode, decimal newAmount = 0)
    {
        return mode switch
        {
            "1" => _repository.AlterInvoiceAmount(id, newAmount)
                ? (true, $"Factura ID {id} modificada => ImportTotal: {newAmount:F2}€")
                : (false, "No s'ha pogut modificar la factura."),

            "2" => _repository.DeleteInvoice(id)
                    ? (true, $"Factura ID {id} esborrada de la base de dades.")
                    : (false, "No s'ha pogut esborrar la factura."),

            _ => (false, "Opció no vàlida.")
        };
    }

    public (bool Success, string Message) ResetSystem()
    {
        bool success = _repository.DeleteAllInvoices();

        return success
            ? (true, "Factures esborrades i comptador d'ID a 0.")
            : (false, "No s'ha pogut resetejar la base de dades.");
    }

    public string GenerateVerifactuXml(Invoice invoice)
    {
        // Espais de noms de l'AEAT
        XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace sum = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";
        XNamespace sum1 = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
        XNamespace xd = "http://www.w3.org/2000/09/xmldsig#";   

        // Calula les dades que falten segons les normes de l'AEAT
        // La base imposable és el total menys l'IVA
        decimal baseImponible = invoice.ImportTotal - invoice.QuotaIVA;
        // L'AEAT demana el tipus impositiu en percentatge sencer
        decimal tipoImpositivoFormat = invoice.TipusImpositiu * 100;

        // Arbre XML
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(soapenv + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soapenv.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sum", sum.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sum1", sum1.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xd", xd.NamespaceName),
                new XElement(soapenv + "Header"),
                new XElement(soapenv + "Body",
                    new XElement(sum + "RegFactuSistemaFacturacion",
                        new XElement(sum + "Cabecera",
                            new XElement(sum1 + "ObligadoEmision",
                                new XElement(sum1 + "NombreRazon", "NOM EMPRESA FICTICIA S.L."),
                                new XElement(sum1 + "NIF", invoice.NIFEmissor)
                            )
                        ),
                        new XElement(sum + "RegistroFactura",
                            new XElement(sum1 + "RegistroAlta",
                                new XElement(sum1 + "IDVersion", "1.0"),
                                new XElement(sum1 + "IDFactura",
                                    new XElement(sum1 + "IDEmisorFactura", invoice.NIFEmissor),
                                    new XElement(sum1 + "NumSerieFactura", invoice.SerieFactura + invoice.NumeroFactura),
                                    new XElement(sum1 + "FechaExpedicionFactura", invoice.DataExpedicio.ToString("dd-MM-yyyy"))
                                ),
                                new XElement(sum1 + "NombreRazonEmisor", "NOM EMPRESA FICTICIA S.L."),
                                new XElement(sum1 + "TipoFactura", "F1"),
                                new XElement(sum1 + "DescripcionOperacion", "Venda de productes/serveis"),
                                new XElement(sum1 + "Desglose",
                                    new XElement(sum1 + "DetalleDesglose",
                                        new XElement(sum1 + "ClaveRegimen", "01"),
                                        new XElement(sum1 + "CalificacionOperacion", "S1"),
                                        new XElement(sum1 + "TipoImpositivo", tipoImpositivoFormat.ToString("F0", CultureInfo.InvariantCulture)),
                                        new XElement(sum1 + "BaseImponibleOimporteNoSujeto", baseImponible.ToString("F2", CultureInfo.InvariantCulture)),
                                        new XElement(sum1 + "CuotaRepercutida", invoice.QuotaIVA.ToString("F2", CultureInfo.InvariantCulture))
                                    )
                                ),
                                new XElement(sum1 + "CuotaTotal", invoice.QuotaIVA.ToString("F2", CultureInfo.InvariantCulture)),
                                new XElement(sum1 + "ImporteTotal", invoice.ImportTotal.ToString("F2", CultureInfo.InvariantCulture)),
                                new XElement(sum1 + "Encadenamiento",
                                    new XElement(sum1 + "RegistroAnterior",
                                        new XElement(sum1 + "IDEmisorFactura", invoice.NIFEmissor),
                                        new XElement(sum1 + "Huella", invoice.EmpremtaAnterior)
                                    )
                                ),
                                new XElement(sum1 + "SistemaInformatico",
                                    new XElement(sum1 + "NombreRazon", "Desenvolupador del SIF S.L."),
                                    new XElement(sum1 + "NIF", "41562299W"),
                                    new XElement(sum1 + "NombreSistemaInformatico", "VeriFicat"),
                                    new XElement(sum1 + "IdSistemaInformatico", "01"),
                                    new XElement(sum1 + "Version", "1.0.0"),
                                    new XElement(sum1 + "NumeroInstalacion", "1"),
                                    new XElement(sum1 + "TipoUsoPosibleSoloVerifactu", "S"),
                                    new XElement(sum1 + "TipoUsoPosibleMultiOT", "N"),
                                    new XElement(sum1 + "IndicadorMultiplesOT", "N")
                                ),
                                new XElement(sum1 + "FechaHoraHusoGenRegistro", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss+01:00")),
                                new XElement(sum1 + "TipoHuella", "01"),
                                new XElement(sum1 + "Huella", invoice.Empremta)
                            )
                        )
                    )
                )
            )
        );

        // Retorna la declaració i l'XML
        return doc.Declaration?.ToString() + Environment.NewLine + doc.ToString();
    }

    public List<Invoice> GetAllInvoices() => _repository.GetAllOrdered();

    private static string GenerateSha256Hash(string payload)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPayload(Invoice invoice, string previousHash)
    {
        // NOTA: Implementació simplificada.
        // L'especificació de l'AEAT inclou també
        // TipoFactura, CuotaTotal i FechaHoraHusoGenRegistro
        return "IDEmisorFactura=" + invoice.NIFEmissor 
            + "&NumSerieFactura=" + invoice.SerieFactura + invoice.NumeroFactura
            + "&FechaExpedicionFactura=" + invoice.DataExpedicio.ToString("dd-MM-yyyy") 
            + "&ImporteTotal=" + invoice.ImportTotal.ToString("F2", CultureInfo.InvariantCulture) 
            + "&Huella=" + previousHash;
    }
}
