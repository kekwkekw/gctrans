namespace GCMod;
internal static class Logger
{
    public static void Info(string text) { }
    public static void Warn(string text) => System.Console.WriteLine("WARN: " + text);
}
