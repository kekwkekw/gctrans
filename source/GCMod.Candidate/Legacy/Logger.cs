namespace GCMod;

/// <summary>
/// 统一日志封装，避免各处直接引用 Plugin.Log。
/// </summary>
public static class Logger
{
    static System.Action<string> info, warn, error;
    public static void Bind(System.Action<string> infoSink, System.Action<string> warningSink, System.Action<string> errorSink)
    { info = infoSink; warn = warningSink; error = errorSink; }
    public static void Info(string msg) { try { info?.Invoke(msg); } catch { } }

    public static void Warn(string msg) { try { warn?.Invoke(msg); } catch { } }

    public static void Error(string msg) { try { error?.Invoke(msg); } catch { } }
}
