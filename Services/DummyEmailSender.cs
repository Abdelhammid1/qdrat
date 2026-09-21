using Microsoft.AspNetCore.Identity.UI.Services;


namespace QdratNew.Services
{

    public class DummyEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // لا ترسل شيء فعليًا – فقط تسجيل
            Console.WriteLine($"[DummyEmail] To: {email}, Subject: {subject}");
            return Task.CompletedTask;
        }
    }

}
