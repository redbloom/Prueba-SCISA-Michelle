using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prueba_SCISA_Michelle.Services.Helpers
{
    internal static class PokeApiHelper
    {
        /// <summary>
        /// Extrae el ID numérico desde una URL del tipo "https://pokeapi.co/api/v2/pokemon/35/"
        /// </summary>
        public static int ExtractId(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return 0;

            var parts = url.TrimEnd('/').Split('/');
            if (parts.Length == 0) return 0;

            return int.TryParse(parts[^1], out var id) ? id : 0;
        }

        /// <summary>
        /// Normaliza una cadena para búsquedas (elimina acentos, guiones, espacios, etc.)
        /// </summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim().ToLowerInvariant();

            var decomposed = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);

            foreach (var ch in decomposed)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            var norm = sb.ToString().Normalize(NormalizationForm.FormC);
            norm = norm.Replace("-", "").Replace(" ", "").Replace("_", "");
            return norm;
        }

        /// <summary>
        /// Normaliza una cadena para búsquedas (elimina acentos, guiones, espacios, etc.)
        /// </summary>
        public static string NormalizeName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input.Trim();

            s = s.Replace('’', '\'')
                 .Replace('‘', '\'')
                 .Replace('“', '"')
                 .Replace('”', '"');

            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            s = sb.ToString().Normalize(NormalizationForm.FormC);

            s = Regex.Replace(s, @"\s+", " ");
            s = Regex.Replace(s, @"\s*-\s*", "-");

            s = s.ToLowerInvariant();

            return s;
        }

        /// <summary>
        /// Devuelve la URL directa al sprite del Pokémon.
        /// </summary>
        public static string Sprite(int id)
            => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
    }
}
