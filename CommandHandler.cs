using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OSMaker
{
    public static class CommandHandler
    {
        public static Dictionary<string, string> Variables = new Dictionary<string, string>();

        public static NamedPipeServerStream? Pipe = null;
        public static Thread? Thread;
        public static bool Connected { get; private set; }

        public static void Initialize()
        {
            Pipe      = null;
            Connected = false;
            Thread    = null;
        }

        public static void ExecuteFile(string fname)
        {
            if (!File.Exists(fname)) { Debug.Error("Failed to locate script at '" + fname + "'"); return; }
            List<string> lines = File.ReadAllLines(fname).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(";")) { lines.RemoveAt(i); i--; continue; }
                int comment_start = lines[i].IndexOf(';');
                if (comment_start >= 0 && comment_start < lines[i].Length) { lines[i] = lines[i].Substring(0, comment_start); }
            }

            foreach (string line in lines) { Execute(line); }
        }

        public static void Execute(string input)
        {
            if (input.Length == 0) { return; }

            List<string> args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (args.Count == 0) { return; }

            try
            {
                string cmd = args[0].ToUpper();
                if (cmd == "SETVAR") { SETVAR(args); }
                else if (cmd == "SETDIR") { SETDIR(args); }
                else if (cmd == "RMDIR") { RMDIR(args); }
                else if (cmd == "MKDIR") { MKDIR(args); }
                else if (cmd == "RECURSIVE_IO") { RECURSIVE_IO(args); }
                else if (cmd == "RECURSIVE") { RECURSIVE(args); }
                else if (cmd == "PIPE") { PIPE(args); }
                else 
                { 
                    if (Variables.ContainsKey(cmd))
                    {
                        args.RemoveAt(0);
                        string argstr = string.Join(' ', args);
                        Process proc = StartProcess(Variables[cmd], argstr);
                        proc.WaitForExit();
                        if (proc.ExitCode == 0) { Debug.OK("Command '" + cmd + "' finished successfully"); }
                    }
                    else { Debug.Error("Invalid command '" + cmd + "'"); }
                }
            }
            catch (Exception ex) { Debug.Error("Unexpected failure - " + ex.Message); return; }
        }

        private static Process StartProcess(string name, string args)
        {
            try
            {
                Process proc = Process.Start(name, args);
                return proc;
            }
            catch (Exception ex) { Debug.Error(ex.Message); return Process.GetCurrentProcess(); }
        }

        private static void SETVAR(List<string> args)
        {
            if (args.Count < 3) { Debug.Error("Invalid arguments for 'SETVAR'"); return; }
            args.RemoveAt(0);

            string var = args[0].ToUpper();
            args.RemoveAt(0);

            string value = string.Join(' ', args);
            Variables[var] = value;
            Debug.Info("Set variable - Variable:" + var + " Value:" + value);
        }

        private static void SETDIR(List<string> args)
        {
            if (args.Count < 2) { Debug.Error("Invalid arguments for 'SETDIR'"); return; }
            args.RemoveAt(0);

            string path = string.Join(' ', args);
            Environment.CurrentDirectory = path;
            Debug.Info("Set current directory to '" + path + "'");
        }

        private static void RECURSIVE(List<string> args)
        {
            if (args.Count < 4) { Debug.Error("Invalid arguments for 'RECURSIVE'"); return; }
            args.RemoveAt(0);

            string ext = args[0];
            args.RemoveAt(0);

            string path_in = args[0].Replace("\\", "/");
            if (!path_in.EndsWith("/")) { path_in += "/"; }
            args.RemoveAt(0);

            string next = string.Join(' ', args);
            next = next.Substring(next.IndexOf(':') + 1);

            string filestr = "";
            string[] files = Directory.GetFiles(path_in, "*" + ext, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)  
            {
                filestr += files[i] + " ";
            }
            next = next.Replace("#IN", filestr);
            Execute(next);
        }

        private static void RECURSIVE_IO(List<string> args)
        {
            if (args.Count < 6) { Debug.Error("Invalid arguments for 'RECURSIVE_IO'"); return; }
            args.RemoveAt(0);

            string input = args[0];
            args.RemoveAt(0);

            string output = args[0];
            args.RemoveAt(0);

            string path_in = args[0].Replace("\\", "/");
            if (!path_in.EndsWith("/")) { path_in += "/"; }
            args.RemoveAt(0);

            string path_out = args[0].Replace("\\", "/");
            if (!path_out.EndsWith("/")) { path_out += "/"; }
            args.RemoveAt(0);
            
            string[] files = Directory.GetFiles(path_in, "*" + Path.GetExtension(input), SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)  
            {
                string next = string.Join(' ', args);
                next = next.Substring(next.IndexOf(':') + 1);
                next = next.Replace("#IN",  files[i]);
                next = next.Replace("#OUT", path_out + Path.GetFileNameWithoutExtension(files[i]) + output.Substring(output.IndexOf('.')));
                Execute(next);
            }
        }

        private static void RMDIR(List<string> args)
        {
            if (args.Count < 2) { Debug.Error("Expected path for removal"); return; }
            args.RemoveAt(0);

            string path = string.Join(' ', args);
            if (!Directory.Exists(path)) {Debug.Error("Unable to locate directory '" + path + "'"); return; }
            Directory.Delete(path, true);
            Debug.Info("Deleted directory '" + path + "'");
        }

        private static void MKDIR(List<string> args)
        {
            if (args.Count < 2) { Debug.Error("Expected path for removal"); return; }
            args.RemoveAt(0);

            string path = string.Join(' ', args);
            if (Directory.Exists(path)) { Debug.Error("Directory already exists"); return; }
            Directory.CreateDirectory(path);
            Debug.Info("Created directory at '" + path + "'");
        }

        private static void PIPE(List<string> args)
        {
            if (args.Count != 2) { Debug.Error("Invalid arguments for 'PIPE'"); return; }

            string name = args[1];

            Pipe = new NamedPipeServerStream(name);
            Thread = new Thread(PipeMain);
            Thread.Start();
            Debug.Info("Started pipe");
        }

        private static void PipeMain()
        {
            bool running = true;
            while (running)
            {
                if (Pipe == null) { return; }

                Pipe.WaitForConnection();
                Connected = Pipe.IsConnected;

                if (!Connected) { Debug.Error("Failed to connect debugger pipe"); return; }
                else { Debug.OK("Debugger pipe connected"); }

                while (Pipe.IsConnected)
                {
                    while (!Pipe.CanRead);
                    Console.Write((char)Pipe.ReadByte());
                }

                running   = false;
                Connected = false;
                Pipe      = null;
                Debug.Info("Closed debugger pipe");
            }
        }
    }
}