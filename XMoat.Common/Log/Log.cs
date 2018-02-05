using System;
using NLog;
#if UNITY_EDITOR || UNITY_IOS || UNITY_IPHONE || UNITY_ANDROID
using globalLog = UnityEngine.Debug;
#endif

public static class Log
{
#if NETCOREAPP2_0
    private static readonly NLogger globalLog = new NLogger();
#endif

    public static void Warning(string message, params object[] objs)
    {
        globalLog.LogWarningFormat(message, objs);
    }

    public static void Info(string message, params object[] objs)
    {
        globalLog.LogFormat(message, objs);
    }

    public static void Debug(string message, params object[] objs)
    {
        globalLog.LogAssertionFormat(message, objs);
    }

    public static void Error(string message, params object[] objs)
    {
        globalLog.LogErrorFormat(message, objs);
    }
}

public class NLogger
{
    private readonly Logger logger = LogManager.GetLogger("Logger");

    public void LogWarningFormat(string message, params object[] objs)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message, objs);
        Console.ResetColor();
        this.logger.Warn(this.Decorate(message, objs));
    }

    public void LogFormat(string message, params object[] objs)
    {
        Console.WriteLine(message, objs);
        this.logger.Info(this.Decorate(message, objs));
    }

    public void LogAssertionFormat(string message, params object[] objs)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message, objs);
        Console.ResetColor();
        this.logger.Debug(this.Decorate(message, objs));
    }

    public void LogErrorFormat(string message, params object[] objs)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message, objs);
        Console.ResetColor();
        this.logger.Error(this.Decorate(message, objs));
    }

    private string Decorate(string message, params object[] objs)
    {
        return string.Format(message, objs);
    }
}