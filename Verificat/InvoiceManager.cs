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

    public void CreateInvoice(Invoice invoice)
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

            Console.WriteLine($"Factura {invoice.SerieFactura}-{invoice.NumeroFactura} creada.");
            Console.WriteLine($"Empremta: {invoice.Empremta}");
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            Console.WriteLine($"S'ha ignorat la factura {invoice.SerieFactura}-{invoice.NumeroFactura} " +
                $"perquè ja existeix.");
            transaction.Rollback();
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"S'ha produït un error en crear la factura: {ex.Message}.");
            transaction.Rollback();
            throw;
        }
    }
   
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
