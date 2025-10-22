using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Prueba_SCISA_Michelle.Models.Options;
using Prueba_SCISA_Michelle.Services.Abstractions;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly EmailOptions _opts;
        private readonly string _apiKey;

        public EmailService(ILogger<EmailService> logger, IOptions<EmailOptions> options, IConfiguration config)
        {
            _logger = logger;
            _opts = options.Value;
            _apiKey = config["SENDGRID_API_KEY"] ?? Environment.GetEnvironmentVariable("SENDGRID_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("SENDGRID_API_KEY no está configurada en el entorno.");
        }

        public async Task SendCustomAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            await SendCustomAsync(toEmail, subject, htmlBody, null!, ct);
        }

        public async Task SendCustomAsync(
            string toEmail,
            string subject,
            string htmlBody,
            IDictionary<string, (byte[] bytes, string mime, string fileName)> inlineImages,
            CancellationToken ct = default)
        {
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_opts.FromEmail, _opts.FromName);
            var to = new EmailAddress(toEmail);

            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);

            if (inlineImages is not null && inlineImages.Count > 0)
            {
                msg.Attachments ??= new List<Attachment>();
                foreach (var kv in inlineImages)
                {
                    var cid = kv.Key;
                    var (bytes, mime, fileName) = kv.Value;
                    msg.Attachments.Add(new Attachment
                    {
                        Content = Convert.ToBase64String(bytes),
                        Type = string.IsNullOrWhiteSpace(mime) ? "image/png" : mime,
                        Filename = string.IsNullOrWhiteSpace(fileName) ? $"{cid}.png" : fileName,
                        Disposition = "inline",
                        ContentId = cid
                    });
                }
            }

            if (_opts.SandboxMode)
                msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = true } };

            var resp = await client.SendEmailAsync(msg, ct);
            if ((int)resp.StatusCode >= 400)
            {
                var body = await resp.Body.ReadAsStringAsync(ct);
                _logger.LogError("Fallo al enviar correo: {Code} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException($"SendGrid {(int)resp.StatusCode}: {body}");
            }
        }

        public Task SendOneAsync(int pokemonId, CancellationToken ct = default)
        {
            _logger.LogInformation("Enviar correo para Pokémon {Id}", pokemonId);
            return Task.CompletedTask;
        }

        public Task SendBulkAsync(IEnumerable<int> pokemonIds, CancellationToken ct = default)
        {
            _logger.LogInformation("Enviar correos masivos para IDs: {Ids}", string.Join(",", pokemonIds));
            return Task.CompletedTask;
        }
    }
}
