using System.Collections.Concurrent;
using System.IO.Ports;

class SerialConnector
{
    record PortEntry(SerialPort Port, SemaphoreSlim Gate);

    readonly ConcurrentDictionary<string, PortEntry> _ports = new(StringComparer.OrdinalIgnoreCase);

    public Task<SerialResponse> SendAsync(SerialRequest req) =>
    Task.Run(() =>
    {
        var isRaw = req.Encoding.Equals("RAW", StringComparison.OrdinalIgnoreCase);

        var available = SerialPort.GetPortNames();
        if (!available.Contains(req.Port, StringComparer.OrdinalIgnoreCase))
            return Fail($"Porta '{req.Port}' non trovata. Porte disponibili: {string.Join(", ", available)}");

        var entry = _ports.GetOrAdd(req.Port, name =>
            new PortEntry(new SerialPort(name, req.BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake    = Handshake.None,
                ReadTimeout  = 5000,
                WriteTimeout = 5000,
            }, new SemaphoreSlim(1, 1)));

        entry.Gate.Wait();
        try
        {
            var sp = entry.Port;

            if (!sp.IsOpen)
            {
                try { sp.Open(); }
                catch (UnauthorizedAccessException ex) { return Fail($"Porta occupata o accesso negato: {ex.Message}"); }
                catch (IOException ex)                 { return Fail($"Errore I/O in apertura porta: {ex.Message}"); }
            }

            if (sp.BaudRate != req.BaudRate) sp.BaudRate = req.BaudRate;
            sp.ReadTimeout  = req.TimeoutMs;
            sp.WriteTimeout = req.TimeoutMs;

            Console.WriteLine($"[CFG] {sp.PortName} | {sp.BaudRate} baud | {sp.DataBits} data | parity={sp.Parity} | stop={sp.StopBits} | handshake={sp.Handshake} | rx={sp.ReadTimeout} ms | tx={sp.WriteTimeout} ms");

            sp.DiscardInBuffer();
            sp.DiscardOutBuffer();

            if (isRaw)
                return SendRaw(sp, req.Port, req.Data, req.TimeoutMs);
            else
                return SendAscii(sp, req.Port, req.Data, req.TimeoutMs);
        }
        finally
        {
            entry.Gate.Release();
        }
    });

    static SerialResponse SendAscii(SerialPort sp, string portName, string data, int timeoutMs)
    {
        Console.WriteLine($"[TX]  {portName} | ASCII | {data}");
        try { sp.Write(data); }
        catch (TimeoutException)             { return Fail($"Timeout scrittura su '{portName}' (>{timeoutMs} ms)."); }
        catch (InvalidOperationException ex) { return Fail($"Porta non aperta durante la scrittura: {ex.Message}"); }
        catch (IOException ex)               { return Fail($"Errore I/O durante la scrittura: {ex.Message}"); }

        string response;
        try   { response = sp.ReadLine(); }
        catch (TimeoutException) { return Fail($"Timeout lettura da '{portName}' (>{timeoutMs} ms)."); }
        catch (IOException ex)   { return Fail($"Errore I/O durante la lettura: {ex.Message}"); }

        var trimmed = response.TrimEnd('\r');
        Console.WriteLine($"[RX]  {portName} | ASCII | {trimmed}");
        return new SerialResponse(true, trimmed, null);
    }

    static SerialResponse SendRaw(SerialPort sp, string portName, string data, int timeoutMs)
    {
        byte[] txBytes;
        try   { txBytes = ParseRawHex(data); }
        catch { return Fail($"Formato dati RAW non valido. Atteso: ':XX:XX:...' (es. ':42:02:00')."); }

        Console.WriteLine($"[TX]  {portName} | RAW | {txBytes.Length} byte | {data}");
        try { sp.Write(txBytes, 0, txBytes.Length); }
        catch (TimeoutException)             { return Fail($"Timeout scrittura su '{portName}' (>{timeoutMs} ms)."); }
        catch (InvalidOperationException ex) { return Fail($"Porta non aperta durante la scrittura: {ex.Message}"); }
        catch (IOException ex)               { return Fail($"Errore I/O durante la scrittura: {ex.Message}"); }

        // Lettura a deadline sliding: attende il primo byte, poi accumula
        // finché arrivano dati, con finestra di 200 ms sull'ultimo byte
        var buffer   = new byte[1024];
        var received = 0;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                if (sp.BytesToRead > 0)
                {
                    received += sp.Read(buffer, received, Math.Min(sp.BytesToRead, buffer.Length - received));
                    deadline  = DateTime.UtcNow.AddMilliseconds(200);
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }
        catch (IOException ex) { return Fail($"Errore I/O durante la lettura: {ex.Message}"); }

        if (received == 0)
            return Fail($"Nessuna risposta da '{portName}' entro {timeoutMs} ms.");

        var hex = FormatRawHex(buffer, received);
        Console.WriteLine($"[RX]  {portName} | RAW | {received} byte | {hex}");
        return new SerialResponse(true, hex, null);
    }

    static byte[] ParseRawHex(string s)
    {
        var tokens = s.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Select(t => Convert.ToByte(t, 16)).ToArray();
    }

    static string FormatRawHex(byte[] buf, int count) => string.Join("-", buf.Take(count).Select(b => $"{b:X2}"));

    static SerialResponse Fail(string error) => new(false, null, error);
}
