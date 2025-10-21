using Microsoft.Extensions.Logging;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        public EmailService(ILogger<EmailService> logger) => _logger = logger;

        public Task SendOneAsync(int pokemonId, CancellationToken ct = default)
        {
            _logger.LogInformation("Simular envío de correo para Pokémon {Id}", pokemonId);
            return Task.CompletedTask;
        }

        public Task SendBulkAsync(IEnumerable<int> pokemonIds, CancellationToken ct = default)
        {
            _logger.LogInformation("Simular envío masivo para IDs: {Ids}", string.Join(",", pokemonIds));
            return Task.CompletedTask;
        }
    }
}
