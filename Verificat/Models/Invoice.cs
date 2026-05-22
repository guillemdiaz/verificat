using System;
using System.Collections.Generic;
using System.Text;

namespace Verificat.Models;

internal class Invoice
{
    public int Id { get; set; }
    public string SerieFactura { get; set; } = string.Empty;
    public string NumeroFactura { get; set; } = string.Empty;
    public DateTime DataExpedicio { get; set; }
    public string NIFEmissor { get; set; } = string.Empty;
    public decimal ImportTotal { get; set; }
    public decimal TipusImpositiu { get; set; }
    public decimal QuotaIVA { get; set; }
    public string PrimerRegistre { get; set; } = "N";
    public string EmpremtaAnterior { get; set; } = string.Empty;
    public string Empremta { get; set; } = string.Empty;
}