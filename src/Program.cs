using System;
using Hakoniwa.Core;

namespace Hakoniwa;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashGuard.Install();
        try
        {
            if (AppUpdate.TryApply(args))
                return;
            GameHost.Run(args);
        }
        catch (Exception ex)
        {
            CrashGuard.Handle(ex);
        }
    }
}
