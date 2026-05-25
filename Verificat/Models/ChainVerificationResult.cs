namespace Verificat.Models;

internal record ChainVerificationResult(
    bool IsValid,
    int TotalInvoices,
    List<string> Errors
);