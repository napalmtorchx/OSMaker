using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OSMaker
{
    public static class Program
    {
         private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        private static void Main(string[] args)
        {
            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            uint outConsoleMode = 0;
            GetConsoleMode(iStdOut, out outConsoleMode);
            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            SetConsoleMode(iStdOut, outConsoleMode);

            Debug.Info("OSMaker Utility v1.0");
            CommandHandler.Initialize();

            if (args.Length == 0) { Debug.Error("No input file specified."); return; }
            else
            {
                string fname = string.Join(' ', args);
                CommandHandler.ExecuteFile(fname);
            }

            Console.ReadLine();
        }
    }
}
