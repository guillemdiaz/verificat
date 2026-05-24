using Microsoft.Data.SqlClient;
using System.Data;
using Verificat.Models;

namespace Verificat;

internal class InvoiceRepository
{
    private readonly string _connectionString;
    
    public InvoiceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string GetLastHash(SqlConnection connection, SqlTransaction transaction)
    {
        const string query = @"
                SELECT TOP 1 Empremta
                FROM RegistresFacturacio
                ORDER BY ID DESC
            ";
        using var command = new SqlCommand(query, connection, transaction);
        object? result = command.ExecuteScalar();
        return result?.ToString() ?? string.Empty;
    }

    public void Insert(SqlConnection connection, SqlTransaction transaction, Invoice invoice)
    {
        const string query = @"
                INSERT INTO RegistresFacturacio 
                    (SerieFactura, NumeroFactura, DataExpedicio, NIFEmissor, 
                    ImportTotal, TipusImpositiu, QuotaIVA, PrimerRegistre, 
                    EmpremtaAnterior, Empremta)
                VALUES
                    (@SerieFactura, @NumeroFactura, @DataExpedicio, @NIFEmissor,
                    @ImportTotal, @TipusImpositiu, @QuotaIVA, @PrimerRegistre, 
                    @EmpremtaAnterior, @Empremta)";

        using var command = new SqlCommand(query, connection, transaction);

        command.Parameters.Add("@SerieFactura", SqlDbType.NVarChar, 20).Value = invoice.SerieFactura;
        command.Parameters.Add("@NumeroFactura", SqlDbType.NVarChar, 60).Value = invoice.NumeroFactura;
        command.Parameters.Add("@DataExpedicio", SqlDbType.DateTime).Value = invoice.DataExpedicio;
        command.Parameters.Add("@NIFEmissor", SqlDbType.NVarChar, 20).Value = invoice.NIFEmissor;
        command.Parameters.Add("@ImportTotal", SqlDbType.Decimal).Value = invoice.ImportTotal;
        command.Parameters.Add("@TipusImpositiu", SqlDbType.Decimal).Value = invoice.TipusImpositiu;
        command.Parameters.Add("@QuotaIVA", SqlDbType.Decimal).Value = invoice.QuotaIVA;
        command.Parameters.Add("@PrimerRegistre", SqlDbType.NVarChar, 1).Value = invoice.PrimerRegistre;
        command.Parameters.Add("@EmpremtaAnterior", SqlDbType.NVarChar, 64).Value = invoice.EmpremtaAnterior;
        command.Parameters.Add("@Empremta", SqlDbType.NVarChar, 64).Value = invoice.Empremta;

        command.ExecuteNonQuery();
    }

    public List<Invoice> GetAllOrdered()
    {
        const string query = @"
            SELECT ID, SerieFactura, NumeroFactura, DataExpedicio,
                   NIFEmissor, ImportTotal, TipusImpositiu, QuotaIVA,
                   PrimerRegistre, EmpremtaAnterior, Empremta
            FROM RegistresFacturacio
            ORDER BY ID ASC
        ";

        var invoices = new List<Invoice>();

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(query, connection);
        connection.Open();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            invoices.Add(new Invoice
            {
                Id = (int)reader["ID"],
                SerieFactura = (string)reader["SerieFactura"],
                NumeroFactura = (string)reader["NumeroFactura"],
                DataExpedicio = (DateTime)reader["DataExpedicio"],
                NIFEmissor = (string)reader["NIFEmissor"],
                ImportTotal = (decimal)reader["ImportTotal"],
                TipusImpositiu = (decimal)reader["TipusImpositiu"],
                QuotaIVA = (decimal)reader["QuotaIVA"],
                PrimerRegistre = (string)reader["PrimerRegistre"],
                EmpremtaAnterior = (string)reader["EmpremtaAnterior"],
                Empremta = (string)reader["Empremta"]
            });
        }
        return invoices;
    }
}
