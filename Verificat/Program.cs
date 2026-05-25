using Microsoft.Extensions.Configuration;
using Verificat;
using Verificat.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

string connectionString = config.GetConnectionString("VerificatDB")!;
var manager = new InvoiceManager(connectionString);

while (true)
{
    Console.WriteLine();
    Console.WriteLine(@"////////////// VeriFicat \\\\\\\\\\\\\\");
    Console.WriteLine(@"> SELECCIONA UNA OPCIÓ:                ");
    Console.WriteLine(@"  1) Generar factures de prova         ");
    Console.WriteLine(@"  2) Mostrar factures                  ");
    Console.WriteLine(@"  3) Verificar integritat de la cadena ");
    Console.WriteLine(@"  4) Simular manipulació               ");
    Console.WriteLine(@"  5) Resetejar sistema (Perill)        ");
    Console.WriteLine(@"  6) Sortir                            ");
    Console.WriteLine(@"///////////////////////////////////////");
    Console.Write("\n>_ ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1":
            GenerateTestInvoices(manager);
            break;
        case "2":
            ViewInvoices(manager);
            break;
        case "3":
            RunVerification(manager);
            break;
        case "4":
            RunTampering(manager);
            break;
        case "5":
            ResetDatabase(manager);
            break;
        case "6":
            Console.WriteLine("Fins aviat.");
            return;
        default:
            Console.WriteLine("Opció no vàlida.");
            break;
    }
}

static void GenerateTestInvoices(InvoiceManager manager)
{
    var invoices = new[]
    {
        new Invoice { SerieFactura = "VF26", NumeroFactura = "001",
            DataExpedicio = new DateTime(2026, 5, 23), NIFEmissor = "41562599A",
            ImportTotal = 121.00m, TipusImpositiu = 0.21m, QuotaIVA = 21.00m },
        new Invoice { SerieFactura = "VF26", NumeroFactura = "002",
            DataExpedicio = new DateTime(2026, 5, 23), NIFEmissor = "41562599A",
            ImportTotal = 60.50m, TipusImpositiu = 0.10m, QuotaIVA = 6.05m },
        new Invoice { SerieFactura = "VF26", NumeroFactura = "003",
            DataExpedicio = new DateTime(2026, 5, 23), NIFEmissor = "41562599A",
            ImportTotal = 48.40m, TipusImpositiu = 0.04m, QuotaIVA = 1.94m }
    };

    Console.WriteLine();
    foreach (var invoice in invoices)
    {
        var result = manager.CreateInvoice(invoice);
        Console.WriteLine(result.Success
            ? $"    [OK] {result.Message}\n        > HASH: {result.Empremta}"
            : $"    [INFO] {result.Message}");
    }
}

static void RunVerification(InvoiceManager manager)
{
    Console.WriteLine();
    var result = manager.VerifyChain();

    if (result.IsValid)
    {
        Console.WriteLine($"    [OK] Cadena verificada - {result.TotalInvoices} factures íntegres.");
    }
    else
    {
        Console.WriteLine($"    [FAIL] Cadena compromesa - {result.Errors.Count} error(s) detectat(s):");
        foreach (var error in result.Errors)
            Console.WriteLine($"        > {error}");
    }
}

static void RunTampering(InvoiceManager manager)
{
    var invoices = manager.GetAllInvoices();
    if (invoices.Count == 0)
    {
        Console.WriteLine("\n    [INFO] No hi ha factures per manipular.");
        return;
    }

    Console.WriteLine("\n    Factures disponibles:");
    foreach (var inv in invoices)
        Console.WriteLine($"        ID {inv.Id}  {inv.SerieFactura}-{inv.NumeroFactura}  {inv.ImportTotal:F2}€");

    Console.Write("\n    ID de la factura a manipular: ");
    if (!int.TryParse(Console.ReadLine(), out int id) || !invoices.Any(i => i.Id == id))
    {
        Console.WriteLine("    [FAIL] ID no vàlid.");
        return;
    }

    Console.WriteLine("\n    Tipus de manipulació:");
    Console.WriteLine("        1. Modificar l'import");
    Console.WriteLine("        2. Esborrar la factura");
    Console.Write("\n    Opció: ");
    string mode = Console.ReadLine()?.Trim() ?? "";

    decimal newAmount = 0;
    if (mode == "1")
    {
        Console.Write("    Nou import total (ex: 10.50): ");
        if (!decimal.TryParse(Console.ReadLine(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out newAmount))
        {
            Console.WriteLine("    [FAIL] Format no vàlid (només números i punts).");
            return;
        }

        if (newAmount <= 0)
        {
            Console.WriteLine("    [FAIL] L'import ha de ser positiu.");
            return;
        }

        if (newAmount >= 10000000000m || newAmount <= -10000000000m)
        {
            Console.WriteLine("    [FAIL] L'import és massa gran.");
            return;
        }
    }

    Console.WriteLine();
    var (success, message) = manager.SimulateTampering(id, mode, newAmount);
    Console.WriteLine(success ? $"    [WARN] {message}" : $"    [FAIL] {message}");

    if (success)
        Console.WriteLine("    > Executa l'opció 2 per auditar la manipulació.");
}

static void ResetDatabase(InvoiceManager manager)
{
    var invoices = manager.GetAllInvoices();
    if (invoices.Count == 0)
    {
        Console.WriteLine("\n    [INFO] No hi ha factures per esborrar.");
        return;
    }

    Console.WriteLine($"\n    [WARN] Estàs a punt d'esborrar totes les factures ({invoices.Count} registres) i posar l'ID a zero.");
    Console.Write("    Estàs segur que vols continuar? (S/N): ");

    string confirmacio = Console.ReadLine()?.Trim().ToUpper() ?? "";

    if (confirmacio == "S")
    {
        Console.WriteLine();
        var (success, message) = manager.ResetSystem();
        Console.WriteLine(success ? $"    [OK] {message}" : $"    [FAIL] {message}");
    }
    else
    {
        Console.WriteLine("\n    [INFO] Operació de reseteig cancel·lada.");
    }
}

static void ViewInvoices(InvoiceManager manager)
{
    var invoices = manager.GetAllInvoices();

    Console.WriteLine();
    if (invoices.Count == 0)
    {
        Console.WriteLine("    [INFO] No hi ha factures per mostrar.");
        return;
    }

    Console.WriteLine($"    [INFO] Mostrant {invoices.Count} factura(es) registrades:\n");
    Console.WriteLine("    ID | Sèrie-Num | Data       | NIF Emissor | Import  | IVA    | Hash");
    Console.WriteLine("    --------------------------------------------------------------------------------");

    foreach (var inv in invoices)
    {
        string shortHash = string.IsNullOrEmpty(inv.Empremta) ? "Cap" : inv.Empremta[..10] + "...";

        Console.WriteLine($"    {inv.Id,-2} | {inv.SerieFactura}-{inv.NumeroFactura,-4} " +
            $"| {inv.DataExpedicio:dd/MM/yyyy} | {inv.NIFEmissor,-11} | {inv.ImportTotal,6:F2}€ " +
            $"| {inv.QuotaIVA,5:F2}€ | {shortHash}");
    }
    Console.WriteLine("    --------------------------------------------------------------------------------");
}