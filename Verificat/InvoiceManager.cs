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

    public void VerifyChain()
    {
        var invoices = _repository.GetAllOrdered();

        if (invoices.Count == 0)
        {
            Console.WriteLine("No hi ha factures a la base de dades.");
            return;
        }

        // La primera factura sempre ha d'apuntar al Genesis Hash (tot zeros)
        string expectedPreviousHash = GenesisHash;
        bool chainValid = true;

        // Recorre totes les factures en ordre ascendent verificant que:
        // - Cada factura apunti a l'empremta de l'anterior
        // - L'empremta de cada factura coincideixi amb el recàlcul a partir de les seves dades
        foreach (var invoice in invoices)
        {
            // Verifica que l'encadenament és seqüencial i sense salts.
            if (invoice.EmpremtaAnterior != expectedPreviousHash)
            {
                Console.WriteLine($"Cadena trencada a ID {invoice.Id} - EmpremtaAnterior no coincideix.");
                chainValid = false;
            }

            // Torna a calcular l'empremta (hash) a partir de les dades de la factura actual
            // i l'empremta anterior.
            string payload = BuildPayload(invoice, invoice.EmpremtaAnterior);
            string recalculated = GenerateSha256Hash(payload);

            // Si l'empremta recalculada no és exactament igual a la que hi ha guardada
            // a la base de dades, vol dir que s'ha modificat algun camp de la factura.
            if (recalculated != invoice.Empremta)
            {
                Console.WriteLine($"Manipulació detectada a ID {invoice.Id} - l'empremta no coincideix.");
                chainValid = false;
            }

            // La factura següent haurà d'apuntar a l'empremta d'aquesta factura.
            expectedPreviousHash = invoice.Empremta;
        }

        if (chainValid)
            Console.WriteLine($"Cadena verificada - {invoices.Count} factures íntegres.");
    }

    public void SimulateTampering()
    {
        // Mostra les factures disponibles
        var invoices = _repository.GetAllOrdered();
        if (invoices.Count == 0)
        {
            Console.WriteLine("No hi ha factures per manipular.");
            return;
        }

        Console.WriteLine("\nFactures disponibles:");
        foreach (var inv in invoices)
            Console.WriteLine($"  ID {inv.Id} — {inv.SerieFactura}-{inv.NumeroFactura} - {inv.ImportTotal:F2}€");

        Console.Write("\nIntrodueix l'ID de la factura a manipular: ");
        if (!int.TryParse(Console.ReadLine(), out int id) ||
            !invoices.Any(i => i.Id == id))
        {
            Console.WriteLine("ID no vàlid.");
            return;
        }

        Console.WriteLine("\nTipus de manipulació:");
        Console.WriteLine("  1. Modificar l'import");
        Console.WriteLine("  2. Esborrar la factura");
        Console.Write("\nOpció: ");
        string option = Console.ReadLine() ?? "";

        switch (option)
        {
            case "1":
                Console.Write("Nou import total (ex: 10.50): ");
                if (!decimal.TryParse(Console.ReadLine(),
                                    NumberStyles.Any, 
                                    CultureInfo.InvariantCulture,
                                    out decimal newAmount)
                    )
                {
                    Console.WriteLine("Import no vàlid.");
                    return;
                }

                bool updateSuccess = _repository.AlterInvoiceAmount(id, newAmount);
                if (updateSuccess)
                    Console.WriteLine($"Factura ID {id} modificada => ImportTotal: {newAmount:F2}€");
                else
                    Console.WriteLine("Error: No s'ha pogut actualitzar la base de dades.");
                break;

            case "2":
                bool deleteSuccess = _repository.DeleteInvoice(id);
                if (deleteSuccess)
                    Console.WriteLine($"Factura ID {id} esborrada de la base de dades.");
                else
                    Console.WriteLine("Error: No s'ha pogut esborrar la factura.");
                break;

            default:
                Console.WriteLine("Opció no vàlida.");
                break;
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
