using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record CreatePaymentRequestDto(int ParticipantId, decimal Amount, string Currency = "DKK");

public record PaymentCallbackDto(string TransactionId, string Status, string? Data);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController(InfosysDbContext db, IMockPaymentService paymentService) : ControllerBase
{
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequestDto request)
    {
        var participant = await db.Participants.FindAsync(request.ParticipantId);
        if (participant == null)
            return NotFound("Participant not found");

        var result = await paymentService.ProcessPaymentAsync(
            new PaymentTransactionRequest(request.ParticipantId, request.Amount, request.Currency)
        );

        var payment = new Payment
        {
            ParticipantId = request.ParticipantId,
            Amount = request.Amount,
            Currency = request.Currency,
            TransactionId = result.TransactionId,
            Status = result.Success ? PaymentStatus.Completed : PaymentStatus.Failed,
            PaymentProvider = "fritid.dk",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = result.Success ? DateTime.UtcNow : null,
            CallbackData = result.Message,
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return Ok(
            new
            {
                transactionId = result.TransactionId,
                status = payment.Status.ToString(),
                message = result.Message,
            }
        );
    }

    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto callback)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.TransactionId == callback.TransactionId);
        if (payment == null)
            return NotFound("Payment transaction not found");

        payment.CallbackData = callback.Data;
        if (callback.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = DateTime.UtcNow;
        }
        else if (callback.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = PaymentStatus.Failed;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Callback processed", status = payment.Status.ToString() });
    }

    [HttpGet("{transactionId}/status")]
    [Authorize]
    public async Task<IActionResult> GetStatus(string transactionId)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        if (payment == null)
            return NotFound();

        return Ok(
            new
            {
                transactionId = payment.TransactionId,
                status = payment.Status.ToString(),
                amount = payment.Amount,
            }
        );
    }
}
