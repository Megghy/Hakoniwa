using System;
using System.IO;
using System.Windows.Forms;
using Hakoniwa.Core;

namespace Hakoniwa;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (AppUpdate.TryApply(args))
                return;
            GameHost.Run(args);
        }
        catch (Exception ex)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hakoniwa-crash.txt");
            File.WriteAllText(path, ex.ToString());
            MessageBox.Show(ex.ToString(), "Hakoniwa");
            throw;
        }
    }
}
