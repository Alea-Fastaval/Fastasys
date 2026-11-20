namespace Fastasys.ApiService.Services;

public record PaymentTransactionRequest(
    int ParticipantId,
    decimal Amount,
    string Currency = "DKK",
    string Description = "Fastaval Registration Payment"
);

public record PaymentTransactionResult(
    string TransactionId,
    bool Success,
    string Status,
    string Message,
    DateTime TransactionTime
);

public interface IMockPaymentService
{
    Task<PaymentTransactionResult> ProcessPaymentAsync(PaymentTransactionRequest request);
    Task<PaymentTransactionResult> RefundPaymentAsync(string transactionId, decimal amount);
    Task<PaymentTransactionResult?> GetTransactionStatusAsync(string transactionId);
}

public class MockPaymentService(ILogger<MockPaymentService> logger) : IMockPaymentService
{
    private readonly Dictionary<string, PaymentTransactionResult> _transactions = new();

    public Task<PaymentTransactionResult> ProcessPaymentAsync(PaymentTransactionRequest request)
    {
        var transactionId = "MOCK-FRITID-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        logger.LogInformation(
            "Processing mock payment via fritid.dk gateway: Participant {ParticipantId}, Amount {Amount} {Currency}",
            request.ParticipantId,
            request.Amount,
            request.Currency
        );

        var result = new PaymentTransactionResult(
            transactionId,
            Success: true,
            Status: "Completed",
            Message: "Approved by Mock Fritid.dk Gateway",
            TransactionTime: DateTime.UtcNow
        );

        _transactions[transactionId] = result;
        return Task.FromResult(result);
    }

    public Task<PaymentTransactionResult> RefundPaymentAsync(string transactionId, decimal amount)
    {
        logger.LogInformation(
            "Processing mock refund for Transaction {TransactionId}, Amount {Amount}",
            transactionId,
            amount
        );

        var result = new PaymentTransactionResult(
            "REFUND-" + transactionId,
            Success: true,
            Status: "Refunded",
            Message: $"Successfully refunded {amount} DKK",
            TransactionTime: DateTime.UtcNow
        );

        return Task.FromResult(result);
    }

    public Task<PaymentTransactionResult?> GetTransactionStatusAsync(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var result);
        return Task.FromResult(result);
    }
}
