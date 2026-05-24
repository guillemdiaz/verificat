using Microsoft.Extensions.Configuration;
using Verificat;
using Verificat.Models;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

string connectionString = config.GetConnectionString("VerificatDB")!;
var invoiceManager = new InvoiceManager(connectionString);

// Crea 3 factures per verificar que la cadena s'està construint correctament.
var invoices = new List<Invoice>
{
    new Invoice
    {
        SerieFactura = "VF26",
        NumeroFactura = "001",
        DataExpedicio = new DateTime(2026, 5, 23),
        NIFEmissor = "41562599A",
        ImportTotal = 121.00m,
        TipusImpositiu = 0.21m,
        QuotaIVA = 21.00m
    },
    new Invoice
    {
        SerieFactura = "VF26",
        NumeroFactura = "002",
        DataExpedicio = new DateTime(2026, 5, 23),
        NIFEmissor = "41562599A",
        ImportTotal = 60.50m,
        TipusImpositiu = 0.10m,
        QuotaIVA = 6.05m
    },
    new Invoice
    {
        SerieFactura = "VF26",
        NumeroFactura = "003",
        DataExpedicio = new DateTime(2026, 5, 23),
        NIFEmissor = "41562599A",
        ImportTotal = 48.40m,
        TipusImpositiu = 0.04m,
        QuotaIVA = 1.94m
    }
};

Console.WriteLine("\nVeriFicat\n");
foreach (var invoice in invoices)
{
    invoiceManager.CreateInvoice(invoice);
}

invoiceManager.VerifyChain();