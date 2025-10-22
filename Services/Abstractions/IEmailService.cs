namespace Prueba_SCISA_Michelle.Services.Abstractions
{
    public interface IEmailService
    {
        Task SendOneAsync(int pokemonId, CancellationToken ct = default);
        Task SendBulkAsync(IEnumerable<int> pokemonIds, CancellationToken ct = default);

        Task SendCustomAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);

        Task SendCustomAsync(
            string toEmail,
            string subject,
            string htmlBody,
            IDictionary<string, (byte[] bytes, string mime, string fileName)> inlineImages,
            CancellationToken ct = default);
    }
}
