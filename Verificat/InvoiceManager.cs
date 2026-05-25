using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Data;
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
