namespace Fastasys.ApiService.Services;

public record EmailMessage(string RecipientEmail, string Subject, string Body, DateTime SentAt);

public record SmsMessage(string PhoneNumber, string Text, DateTime SentAt);

public interface IMockNotificationService
{
    Task SendEmailAsync(string recipientEmail, string subject, string body);
    Task SendSmsAsync(string phoneNumber, string messageText);
    IReadOnlyList<EmailMessage> GetSentEmails();
    IReadOnlyList<SmsMessage> GetSentSms();
}

public class MockNotificationService(ILogger<MockNotificationService> logger) : IMockNotificationService
{
    private readonly List<EmailMessage> _sentEmails = new();
    private readonly List<SmsMessage> _sentSms = new();

    public Task SendEmailAsync(string recipientEmail, string subject, string body)
    {
        var msg = new EmailMessage(recipientEmail, subject, body, DateTime.UtcNow);
        lock (_sentEmails)
        {
            _sentEmails.Add(msg);
        }
        logger.LogInformation("MOCK EMAIL sent to {Recipient}: {Subject}", recipientEmail, subject);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string messageText)
    {
        var msg = new SmsMessage(phoneNumber, messageText, DateTime.UtcNow);
        lock (_sentSms)
        {
            _sentSms.Add(msg);
        }
        logger.LogInformation("MOCK SMS sent to {Phone}: {Text}", phoneNumber, messageText);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> GetSentEmails()
    {
        lock (_sentEmails)
        {
            return _sentEmails.ToList();
        }
    }

    public IReadOnlyList<SmsMessage> GetSentSms()
    {
        lock (_sentSms)
        {
            return _sentSms.ToList();
        }
    }
}
