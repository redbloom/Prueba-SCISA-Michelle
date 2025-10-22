using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Prueba_SCISA_Michelle.Models.Dtos;
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
        private readonly IPokemonService _pokemon;

        public EmailService(
            ILogger<EmailService> logger,
            IOptions<EmailOptions> options,
            IConfiguration config,
            IPokemonService pokemon)
        {
            _logger = logger;
            _opts = options.Value;
            _pokemon = pokemon;
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


        public async Task SendOneAsync(int pokemonId, CancellationToken ct = default)
        {
            var d = await _pokemon.GetDetailsAsync(pokemonId, ct);
            if (d is null)
            {
                _logger.LogWarning("No se encontró el Pokémon {Id}", pokemonId);
                return;
            }

            var to = string.IsNullOrWhiteSpace(_opts.FromEmail) ? "noreply@example.com" : _opts.FromEmail;
            var subject = $"Ficha de {d.Name}";

            var html = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;color:#0e1222"">
  <h2 style=""margin:0 0 8px"">Información de <span style=""text-transform:capitalize"">{d.Name}</span></h2>
  <p style=""margin:0 0 12px""><img src=""{d.ImageUrl}"" alt=""sprite"" width=""96"" height=""96"" style=""image-rendering:pixelated;border-radius:8px""/></p>
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-collapse:collapse;background:#f6f8ff;border-radius:12px;overflow:hidden"">
    <tr><td style=""padding:8px 12px;border-bottom:1px solid #e6e8f5;color:#6b7280"">ID</td><td style=""padding:8px 12px"">{d.Id}</td></tr>
    <tr><td style=""padding:8px 12px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Nombre</td><td style=""padding:8px 12px;text-transform:capitalize"">{d.Name}</td></tr>
    <tr><td style=""padding:8px 12px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Especie</td><td style=""padding:8px 12px"">{d.SpeciesName}</td></tr>
    <tr><td style=""padding:8px 12px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Altura</td><td style=""padding:8px 12px"">{d.HeightMeters:N1} m</td></tr>
    <tr><td style=""padding:8px 12px;color:#6b7280"">Peso</td><td style=""padding:8px 12px"">{d.WeightKg:N1} kg</td></tr>
  </table>
</div>";

            await SendCustomAsync(to, subject, html, ct);
        }

        public async Task SendBulkAsync(IEnumerable<int> pokemonIds, CancellationToken ct = default)
        {
            var to = string.IsNullOrWhiteSpace(_opts.FromEmail) ? "noreply@example.com" : _opts.FromEmail;
            await SendBulkToCoreAsync(to, pokemonIds, ct);
        }

        public async Task SendBulkToAsync(string toEmail, IEnumerable<int> pokemonIds, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || !new EmailAddressAttribute().IsValid(toEmail))
                throw new ArgumentException("Correo destino inválido.", nameof(toEmail));

            await SendBulkToCoreAsync(toEmail, pokemonIds, ct);
        }

        private async Task SendBulkToCoreAsync(string toEmail, IEnumerable<int> pokemonIds, CancellationToken ct)
        {
            var ids = pokemonIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
            {
                _logger.LogInformation("Bulk: no hay IDs que enviar.");
                return;
            }

            using var sem = new SemaphoreSlim(6);
            var details = new List<PokemonDetailDto>();

            await Task.WhenAll(ids.Select(async id =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    var d = await _pokemon.GetDetailsAsync(id, ct);
                    if (d is not null)
                    {
                        lock (details) details.Add(d);
                    }
                }
                finally { sem.Release(); }
            }));

            details = details.OrderBy(d => d.Id).ToList();

            var rows = string.Join("", details.Select(d => $@"
<tr>
  <td style=""padding:8px 12px;border-bottom:1px solid #eee"">{d.Id}</td>
  <td style=""padding:8px 12px;border-bottom:1px solid #eee;text-transform:capitalize"">{d.Name}</td>
  <td style=""padding:8px 12px;border-bottom:1px solid #eee"">{d.SpeciesName}</td>
  <td style=""padding:8px 12px;border-bottom:1px solid #eee""><a href=""{d.ImageUrl}"">{d.ImageUrl}</a></td>
</tr>"));

            var html = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;color:#0e1222"">
  <h2 style=""margin:0 0 10px"">Lista actual de Pokémon ({details.Count})</h2>
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-collapse:collapse;width:100%;background:#ffffff;border:1px solid #e6e8f5;border-radius:12px;overflow:hidden"">
    <thead>
      <tr style=""background:#f6f8ff"">
        <th align=""left"" style=""padding:10px 12px;border-bottom:1px solid #e6e8f5"">ID</th>
        <th align=""left"" style=""padding:10px 12px;border-bottom:1px solid #e6e8f5"">Nombre</th>
        <th align=""left"" style=""padding:10px 12px;border-bottom:1px solid #e6e8f5"">Especie</th>
        <th align=""left"" style=""padding:10px 12px;border-bottom:1px solid #e6e8f5"">Imagen</th>
      </tr>
    </thead>
    <tbody>{rows}</tbody>
  </table>
</div>";

            var subject = $"Pokémon – lista actual ({details.Count})";
            await SendCustomAsync(toEmail, subject, html, ct);
        }
    }
}
