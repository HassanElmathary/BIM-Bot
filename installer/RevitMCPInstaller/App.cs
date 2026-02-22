using System;
using System.Windows;

namespace RevitMCPInstaller
{
    public class App : System.Windows.Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.StartupUri = null;
            app.Startup += (s, e) =>
            {
                var window = new InstallerWindow();
                window.Show();
            };
            app.Run();
        }
    }
}
