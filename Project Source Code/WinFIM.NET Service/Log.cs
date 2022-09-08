using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFIM.NET_Service
{
    internal class Log
    {
        internal static void Info(string message)
        {
            Console.WriteLine(message);
        }

        internal static void Debug(string message)
        {
            Console.WriteLine($"Debug: {message}");
        }
        internal static void Warn(string message)
        {
            Console.WriteLine($"Warning: {message}");
        }
    }
}
