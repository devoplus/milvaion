using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Suvari.ScheduledTasks.Core.Utilities;

public static class Text
{
    /// <summary>
    /// Metinde bulunan Türkçe karakterleri düzeltir.
    /// </summary>
    /// <param name="input">Gönderilen Metin</param>
    /// <returns>Türkçe karakterleri düzeltilen metni döndürür.</returns>
    public static string ReplaceTurkishCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        char[] turkishChars = new[]
                                  {
                                          'Ğ', 'Ü', 'Ş', 'İ', 'Ö', 'Ç', 'ğ', 'ü', 'ş', 'ı', 'ö', 'ç'
                                      };
        char[] replaceValues = new[]
                                   {
                                           'G', 'U', 'S', 'I', 'O', 'C', 'g', 'u', 's', 'i', 'o', 'c'
                                       };

        for (int i = 0; i < turkishChars.Length; i++)
        {
            input = input.Replace(turkishChars[i], replaceValues[i]);
        }

        return input;
    }

    /// <summary>
    /// Metinde bulunan Türkçe karakterleri URL'de kullanılabilecek şekilde düzeltir.
    /// </summary>
    /// <param name="input">Gönderilen Metin</param>
    /// <returns>Türkçe karakterleri düzeltilen metni döndürür.</returns>
    public static string ReplaceTurkishCharactersForSlug(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        input = input.ToLower(new System.Globalization.CultureInfo("tr-TR"));
        input = input.Replace(" ", "-")
            .Replace("'", "-");

        char[] turkishChars = new[]
                                  {
                                          'ç', 'ğ', 'ı', 'ö', 'ş', 'ü', '_', '&', '!', ',', ':', '?', 'ý', 'ð', 'ü', 'þ', 'ö', 'ç',
                                        'I', 'Ð', 'Ü', 'Þ', 'Ý', 'Ç', 'Ö', '=', '.', '`', '<', '>', '@', '(', ')', '?','+','\''
                                      };
        char[] replaceValues = new[]
                                   {
                                           'c', 'g', 'i', 'o', 's', 'u', '-', '-', '-', '-', '-', '-', 'i', 'g', 'u', 's', 'o', 'c',
                                           'i', 'g', 'u', 's', 'i', 'c', 'o', '-', '-', '-', '-', '-', '-', '-', '-', '-', '-','-','-'
                                       };

        for (int i = 0; i < turkishChars.Length; i++)
        {
            input = input.Replace(turkishChars[i], replaceValues[i]);
        }

        return input;
    }

    /// <summary>
    /// Tarih ve saate göre yeni dosya adı üretir.
    /// </summary>
    /// <param name="fileName">Dosya Adı</param>
    /// <returns>Oluşturulan yeni dosya adını döndürür.</returns>
    public static string CreateUniqeSafeName(string fileName)
    {
        string fileNameWitOutExtension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        return DateTime.Now.Year.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() +
                 DateTime.Now.Second.ToString() + DateTime.Now.Millisecond.ToString() + ReplaceTurkishCharactersForSlug(fileNameWitOutExtension) + extension;

    }

    /// <summary>
    /// Metnin belirtilen uzunluğu kadarını filtreler.
    /// </summary>
    /// <param name="text">Metin</param>
    /// <param name="charCount">Karakter Uzunluğu</param>
    /// <returns>Metnin belirtilen uzunluğu kadarını döndürür.</returns>
    public static string StringLimiter(string text, int charCount)
    {
        int intCharSize = text.Length;
        if (text.Length > charCount)
            intCharSize = text.IndexOf(" ", charCount);

        if (text.Length < intCharSize + 1)
        {
            return text;
        }
        return text.Substring(0, intCharSize) + "...";
    }

    /// <summary>
    /// Metnin belirtilen uzunluğu kadarını filtreler.(HTML)
    /// </summary>
    /// <param name="htmlText">Html Metni</param>
    /// <param name="charCount">Karakter Uzunluğu</param>
    /// <returns>Metnin belirtilen uzunluğu kadarını HTML formatında döndürür.</returns>
    public static string StringLimiterForHtml(string htmlText, int charCount)
    {
        string filteredText = HTML.StripTagsRegexCompiled(htmlText);

        int intCharSize = charCount;
        if (filteredText.Length > charCount && filteredText.IndexOf(" ", charCount) > 0)
            intCharSize = filteredText.IndexOf(" ", charCount);

        if (filteredText.Length < intCharSize + 1)
        {
            return filteredText;
        }
        return filteredText.Substring(0, intCharSize) + "...";
    }

    /// <summary>
    /// Verilen string'in içindeki numaraları döndürür.
    /// </summary>
    /// <param name="input">Numaraların alınacağı string.</param>
    /// <returns>String içindeki numaralar.</returns>
    public static string GetNumbers(string input)
    {
        return new string(input.Where(c => char.IsDigit(c)).ToArray());
    }

    public static string ToLowerTurkish(this string input)
    {
        return input.ToLower(new CultureInfo("tr-TR", false));
    }

    /// <summary>
    /// Verilen telefon numarasını ülkeye göre formatlar.
    /// </summary>
    /// <param name="phoneNumber">Telefon numarası</param>
    /// <param name="country">Ülke</param>
    /// <returns>Formatlanmış telefon numarası</returns>
    public static string FormatPhoneNumber(long phoneNumber, Country country)
    {
        switch (country)
        {
            case Country.Turkey:
                return string.Format("{0:(###) ### ## ##}", phoneNumber);
            default:
                return "Unknown country.";
        }
    }

    public enum Country
    {
        Turkey = 0
    }

    public static bool IsNumeric(string s)
    {
        return int.TryParse(s, out int n);
    }
}