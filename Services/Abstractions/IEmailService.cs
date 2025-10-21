namespace Prueba_SCISA_Michelle.Services.Abstractions
{
    public interface IEmailService
    {
        Task SendOneAsync(int pokemonId, CancellationToken ct = default);
        Task SendBulkAsync(IEnumerable<int> pokemonIds, CancellationToken ct = default);
    }
}
