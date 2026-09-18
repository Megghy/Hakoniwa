using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hakoniwa.Core;

/// <summary>
/// XNA 的 Game.Run 走 Application.Run，Update/Draw 异常不会回到 Program.Main。
/// </summary>
public static class CrashGuard
{
    private static int _dialog;

    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Handle(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Handle(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Handle(e.Exception);
            e.SetObserved();
        };
    }

    public static void Handle(Exception ex)
    {
        if (ex is null)
            throw new ArgumentNullException(nameof(ex));

        string path = PathFor();
        try
        {
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n");
        }
        catch
        {
        }

        try
        {
            Notices.Post($"出错已记录: {Path.GetFileName(path)}");
        }
        catch
        {
        }

        if (Interlocked.Exchange(ref _dialog, 1) != 0)
            return;
        try
        {
            MessageBox.Show($"{ex}\n\n已写入 {path}\n之后的错误只记日志。", "Hakoniwa");
        }
        catch
        {
        }
    }

    private static string PathFor()
    {
        string dir = Terraria.Program.SavePath;
        if (string.IsNullOrEmpty(dir))
            dir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, "hakoniwa-error.log");
    }
}
