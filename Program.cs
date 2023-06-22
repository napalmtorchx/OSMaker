using System;
using System.IO;

namespace OSMaker
{
    public static class Program
    {
        private static void Main(string[] args)
        {
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
