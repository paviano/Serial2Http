using System.IO.Ports;
using System.Text;

var port = args.Length > 0 ? args[0] : "COM8";
var baud = args.Length > 1 ? int.Parse(args[1]) : 9600;

var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "STATUS",  "OK READY"       },
    { "VERSION", "FakeDevice 1.0" },
    { "PING",    "PONG"           },
    { "RESET",   "OK RESET"       },
};

using var sp = new SerialPort(port, baud) { ReadTimeout = -1 };

sp.Open();
Console.WriteLine($"FakeDevice in ascolto su {port} @ {baud} baud. Ctrl+C per uscire.");

var sb = new StringBuilder();

sp.DataReceived += (_, _) =>
{
    while (sp.BytesToRead > 0)
    {
        var c = (char)sp.ReadByte();
        if (c == '\n')
        {
            var msg = sb.ToString().Trim('\r', '\n', ' ');
            sb.Clear();

            Console.WriteLine($"<< {msg}");

            var reply = commands.TryGetValue(msg, out var r) ? r : $"ERR UNKNOWN: {msg}";
            sp.Write(reply + "\r\n");

            Console.WriteLine($">> {reply}");
        }
        else
        {
            sb.Append(c);
        }
    }
};

Console.CancelKeyPress += (_, e) => { e.Cancel = true; sp.Close(); };

Thread.Sleep(Timeout.Infinite);
