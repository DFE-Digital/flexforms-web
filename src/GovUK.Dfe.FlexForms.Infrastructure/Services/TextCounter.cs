namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class TextCounter
    {
        public static int GetWordCount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            char[] delimiters = [' ', '\r', '\n', '\t'];
            return text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}