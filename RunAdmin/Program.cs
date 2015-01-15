using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;

namespace RunAdmin
{
    static class Program
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsUserAnAdmin();
        
        const long MapMaxSize = UInt16.MaxValue;


        const string MapNameArgument = "-@MapName";        
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("起動するアプリケーションを引数に指定してください。");
                return;
            }

            if (!IsUserAnAdmin())
            {
                UserMain(args);
            }
            else
            {
                AdminMain(args);
            }
        }

        static void UserMain(string[] args)
        {

            var mapName = Guid.NewGuid().ToString();            
            using (var mmf = MemoryMappedFile.CreateNew(mapName, MapMaxSize))
            {
                using (var writer = mmf.CreateViewStream())
                {
                    byte[] dummy = new byte[MapMaxSize];
                    writer.Write(dummy,0,(int)writer.Length-1);

                }

                var psi = new ProcessStartInfo(GetExeName())
                {
                    Arguments = string.Format("{0} {1}"
                        ,string.Format("{0}:{1}", MapNameArgument  , mapName)
                        ,args.Select(x => x.Contains(' ') ? string.Format("\"{0}\"", x) : x).Join(" ")),
                    Verb = "runas"
                };



                var p = Process.Start(psi);
                Task.Factory.StartNew(() =>
                {
                    while (!p.HasExited)
                    {                        
                        using (var stream = mmf.CreateViewStream())
                        using (var reader = new StreamReader(stream))
                        {
                            var s = reader.ReadToEnd().Trim('\0');
                            if (!string.IsNullOrWhiteSpace(s))
                                Console.WriteLine(s);
                        }
                        Thread.Sleep(100);
                    }
                });
                p.WaitForExit();
            }
        }

        static void AdminMain(string[] args)
        {
            //ユーザー権限からの起動
            if (args[0].Contains(MapNameArgument))
            {
                var mapName = args[0].Split(':')[1];
                args = args.Skip(1).ToArray();

                var psi = new ProcessStartInfo(args[0])
                {
                    Arguments = args.Skip(1).Select(x => x.Contains(' ') ? string.Format("\"{0}\"", x) : x).Join(" "),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                using (var mmf = MemoryMappedFile.OpenExisting(mapName))
                using (var stream = mmf.CreateViewStream())
                using (var writer = new StreamWriter(stream))
                {
                    var p = new Process()
                    {
                        StartInfo = psi
                    };
                    p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data))  writer.WriteLine(e.Data); };
                    p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data))  writer.WriteLine(e.Data); };

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    while (!p.HasExited) 
                    { 
                        Thread.Sleep(100); 
                    }
                }
            }
            else
            {
                var psi = new ProcessStartInfo(args[0])
                {
                    Arguments = args.Skip(1).Select(x => x.Contains(' ') ? string.Format("\"{0}\"", x) : x).Join(" ")
                };
                Process.Start(psi);
            }
        }


        static string GetExeName()
        {
            return typeof(Program).Assembly.Location;
        }


        static string Join(this IEnumerable<string> @this, string separator)
        {
            return string.Join(separator, @this);
        }
            
    }
}
