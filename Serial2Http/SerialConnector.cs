using System.Collections.Concurrent;
using System.IO.Ports;

class SerialConnector
{
    record PortEntry(SerialPort Port, SemaphoreSlim Gate);

    readonly ConcurrentDictionary<string, PortEntry> _ports = new(StringComparer.OrdinalIgnoreCase);

    public Task<SerialResponse> SendAsync(string portName, int baudRate, string data, int timeoutMs) =>
    Task.Run(() =>
    {
        var available = SerialPort.GetPortNames();
        if (!available.Contains(portName, StringComparer.OrdinalIgnoreCase))
            return Fail($"Porta '{portName}' non trovata. Porte disponibili: {string.Join(", ", available)}");

        var entry = _ports.GetOrAdd(portName, name =>
            new PortEntry(new SerialPort(name, baudRate), new SemaphoreSlim(1, 1)));

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

            if (sp.BaudRate != baudRate) sp.BaudRate = baudRate;
            sp.ReadTimeout  = timeoutMs;
            sp.WriteTimeout = timeoutMs;

            sp.DiscardInBuffer();
            sp.DiscardOutBuffer();

            try { sp.Write(data); }
            catch (TimeoutException)             { return Fail($"Timeout durante la scrittura su '{portName}' (>{timeoutMs} ms)."); }
            catch (InvalidOperationException ex) { return Fail($"Porta non aperta durante la scrittura: {ex.Message}"); }
            catch (IOException ex)               { return Fail($"Errore I/O durante la scrittura: {ex.Message}"); }

            string response;
            try   { response = sp.ReadLine(); }
            catch (TimeoutException)  { return Fail($"Timeout in attesa della risposta da '{portName}' (>{timeoutMs} ms)."); }
            catch (IOException ex)    { return Fail($"Errore I/O durante la lettura: {ex.Message}"); }

            return new SerialResponse(true, response.TrimEnd('\r'), null);
        }
        finally
        {
            entry.Gate.Release();
        }
    });

    static SerialResponse Fail(string error) => new(false, null, error);
}
