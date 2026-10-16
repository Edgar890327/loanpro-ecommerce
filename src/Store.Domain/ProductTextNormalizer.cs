using System.Globalization;
using System.Text;

namespace Store.Domain;

public static class ProductTextNormalizer
{
    public static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static string BuildSearchText(string name, string sku, string description) =>
        $"{Normalize(name)} {Normalize(sku)} {Normalize(description)}";
}
