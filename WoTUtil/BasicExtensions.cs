using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Negri.Wot
{
    public static class BasicExtensions
    {
        
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Muda o tipo de Data (não é o mesmo que ToLocal ou ToUniversal, só mexe no DateTimeKind)
        /// </summary>
        public static DateTime ChangeKind(this DateTime date, DateTimeKind kind)
        {
            return DateTime.SpecifyKind(date, kind);
        }

        
        public static DateTime RemoveKind(this DateTime date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        }

        public static DateTime ToDateTime(this long unixTime)
        {
            return UnixEpoch.AddSeconds(unixTime);
        }

        /// <summary>
        /// Retorna o dia da semana imediatamente anterior a data passada
        /// </summary>
        /// <param name="date"></param>
        /// <param name="dayOfWeek"></param>
        /// <returns></returns>
        public static DateTime PreviousDayOfWeek(this DateTime date, DayOfWeek dayOfWeek)
        {
            date = date.AddDays(-1);
            while (date.DayOfWeek != dayOfWeek)
            {
                date = date.AddDays(-1);
            }
            return date.Date;
        }

        /// <summary>
        /// Faz o Equals de strings, desconsiderando Case e Acentos (Diacriticos)
        /// </summary>
        public static bool EqualsCiAi(this string a, string b)
        {
            if ((string.IsNullOrWhiteSpace(a)) && (string.IsNullOrWhiteSpace(b)))
            {
                return true;
            }

            if ((!string.IsNullOrWhiteSpace(a)) && (string.IsNullOrWhiteSpace(b)))
            {
                return false;
            }

            if ((string.IsNullOrWhiteSpace(a)) && (!string.IsNullOrWhiteSpace(b)))
            {
                return false;
            }

            return
                string.Compare(a, b, CultureInfo.InvariantCulture,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
        }

        private static readonly List<string> RomanNumerals = new List<string> { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        private static readonly List<int> Numerals = new List<int> { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };


        /// <summary>
        /// Converte para romanos
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        /// <returns>
        /// from https://stackoverflow.com/questions/22392810/integer-to-roman-format
        /// </returns>
        public static string ToRomanNumeral(this int number)
        {
            var romanNumeral = string.Empty;
            while (number > 0)
            {
                // find biggest numeral that is less than equal to number
                var index = Numerals.FindIndex(x => x <= number);
                // subtract it's value from your number
                number -= Numerals[index];
                // tack it onto the end of your roman numeral
                romanNumeral += RomanNumerals[index];
            }
            return romanNumeral;
        }

        /// <summary>
        ///     Remove acentos e cedilhas
        /// </summary>
        public static string RemoveDiacritics(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            string stFormD = input.Normalize(NormalizationForm.FormD);
            int len = stFormD.Length;
            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[i]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[i]);
                }
            }
            return (sb.ToString().Normalize(NormalizationForm.FormC));
        }

        public static string SanitizeForFileName(this string s)
        {
            if (s == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            return s.Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('*', '_').Replace('!', '_');
        }

        public static string GetHash(this string Phrase)
        {
            SHA512Managed HashTool = new SHA512Managed();
            Byte[] PhraseAsByte = Encoding.UTF8.GetBytes(string.Concat(Phrase));
            Byte[] EncryptedBytes = HashTool.ComputeHash(PhraseAsByte);
            HashTool.Clear();
            return Convert.ToBase64String(EncryptedBytes).SanitizeForFileName();
        }

    }
}