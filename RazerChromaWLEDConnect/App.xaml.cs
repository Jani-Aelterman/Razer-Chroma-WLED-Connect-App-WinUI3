// Licensed to the Chroma Control Contributors under one or more agreements.
// The Chroma Control Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace RazerChromaWLEDConnect
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            System.AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                LogException((System.Exception)args.ExceptionObject, "AppDomain.UnhandledException");

            DispatcherUnhandledException += (s, args) =>
            {
                LogException(args.Exception, "DispatcherUnhandledException");
                args.Handled = true; 
            };

            base.OnStartup(e);
        }

        private void LogException(System.Exception ex, string source)
        {
            try 
            {
                string logFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string message = $"{System.DateTime.Now}: Unhandled Exception ({source})\n{ex}\n\n";
                System.IO.File.AppendAllText(logFile, message);
            }
            catch {}
        }

        private void ShutDown(object sender, ExitEventArgs e)
        {
        }
    }
}
