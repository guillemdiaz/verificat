namespace Verificat.Models;

internal class InvoiceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Empremta { get; init; }
}