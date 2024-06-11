namespace Quack.Analysis
{
    class MiscUtils
    {
        
        public static string FirstNLines(string text, int n)
        {
            var lines = text.Split('\n');
            return string.Join('\n', lines.Take(n));
        }
    }
}