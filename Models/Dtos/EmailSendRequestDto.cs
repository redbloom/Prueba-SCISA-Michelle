using System.ComponentModel.DataAnnotations;

namespace Prueba_SCISA_Michelle.Models.Dtos
{
    public sealed class EmailSendRequestDto
    {
        [Required, EmailAddress]
        public string ToEmail { get; set; } = "";

        [Required, MaxLength(200)]
        public string Subject { get; set; } = "";

        [Required]
        public string HtmlBody { get; set; } = "";
    }
}
