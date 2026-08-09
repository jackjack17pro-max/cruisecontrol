using System;
using System.Threading;
using System.Windows.Forms;

namespace CruiseControlApp
{
    internal static class Program
    {
        private static readonly string MutexGuid = @"Global\CruiseControlApp_Unique_Mutex_ID_983471";

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(true, MutexGuid, out bool createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
        }
    }
}