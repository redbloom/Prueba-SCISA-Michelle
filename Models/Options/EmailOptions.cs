namespace Prueba_SCISA_Michelle.Models.Options
{
    public sealed class EmailOptions
    {
        public string FromName { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public bool SandboxMode { get; set; } = false;
    }
}
