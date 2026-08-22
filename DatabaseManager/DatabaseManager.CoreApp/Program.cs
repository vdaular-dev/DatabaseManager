using DatabaseInterpreter.Core;
using DatabaseManager.Core;
using DatabaseManager.Forms;
using DatabaseManager.Profile.Manager;

namespace DatabaseManager.CoreApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DbInterpreter.Setting = SettingManager.GetInterpreterSetting();

            ProfileBaseManager.Init();   

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            Application.Run(new frmMain());
        }
    }
}