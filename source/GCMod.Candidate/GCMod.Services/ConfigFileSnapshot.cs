using System;
using System.IO;

namespace GCMod.Services;

public static class ConfigFileSnapshot
{
    // A failed open (for example a sharing violation) leaves the file intact.
    // Do not turn that expected save failure into a second, false restore error.
    public static bool RestoreIfChanged(string path, byte[] snapshot)
    {
        try
        {
            if (File.ReadAllBytes(path).AsSpan().SequenceEqual(snapshot)) return false;
        }
        catch (FileNotFoundException)
        {
            // Recreate a file removed during the failed save.
        }
        File.WriteAllBytes(path, snapshot);
        return true;
    }
}
