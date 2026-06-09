using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RS232Lab;

public partial class Form1 : Form
{
    private SerialPort? _serialPort;
    private readonly Stopwatch _pingWatch = new();
    private readonly StringBuilder _receiveBuffer = new();
    private readonly ConcurrentQueue<string> _receivedFrames = new();

    private ComboBox comboPort = null!, comboBaud = null!, comboParity = null!, comboDataBits = null!, comboStopBits = null!, comboTerminator = null!, comboHandshake = null!;
    private TextBox txtCustomTerminator = null!, txtSend = null!, txtHexSend = null!, txtTransactionTimeout = null!;
    private RichTextBox txtReceive = null!, txtLog = null!;
    private Button btnRefresh = null!, btnOpen = null!, btnClose = null!, btnSend = null!, btnPing = null!, btnClearReceive = null!, btnClearLog = null!;
    private Button btnSendHex = null!, btnTransaction = null!, btnAutobaud = null!, btnCheckLines = null!;
    private CheckBox chkDtr = null!, chkRts = null!;
    private Label lblDsr = null!, lblCts = null!;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        LoadDefaultValues();
        RefreshPorts();
    }

    private void BuildUi()
    {
        Text = "RS232 Lab - komunikacja przez port znakowy";
        Size = new Size(1550, 800);
        StartPosition = FormStartPosition.CenterScreen;

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        Controls.Add(main);

        var config = AddGroup(main, "Konfiguracja łącza");
        comboPort = AddCombo(config, "Port:", 90);
        comboBaud = AddCombo(config, "Baud:", 90);
        comboParity = AddCombo(config, "Parity:", 90);
        comboDataBits = AddCombo(config, "Data bits:", 70);
        comboStopBits = AddCombo(config, "Stop bits:", 80);
        comboTerminator = AddCombo(config, "Terminator:", 90);

        config.Controls.Add(new Label { Text = "Własny:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        txtCustomTerminator = new TextBox { Width = 70, Text = "\\r\\n" };
        config.Controls.Add(txtCustomTerminator);

        comboHandshake = AddCombo(config, "Handshake:", 100);

        btnRefresh = AddButton(config, "Odśwież", btnRefresh_Click);
        btnOpen = AddButton(config, "Otwórz", btnOpen_Click);
        btnClose = AddButton(config, "Zamknij", btnClose_Click);

        chkDtr = new CheckBox { Text = "DTR", AutoSize = true, Padding = new Padding(8, 5, 0, 0) };
        chkRts = new CheckBox { Text = "RTS", AutoSize = true, Padding = new Padding(8, 5, 0, 0) };
        chkDtr.CheckedChanged += (_, _) => { if (_serialPort?.IsOpen == true) _serialPort.DtrEnable = chkDtr.Checked; };
        chkRts.CheckedChanged += (_, _) =>
        {
            if (_serialPort?.IsOpen == true && _serialPort.Handshake != Handshake.RequestToSend)
                _serialPort.RtsEnable = chkRts.Checked;
        };

        lblDsr = new Label { Text = "DSR: ?", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        lblCts = new Label { Text = "CTS: ?", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        btnCheckLines = AddButton(config, "Linie", btnCheckLines_Click);

        config.Controls.Add(chkDtr);
        config.Controls.Add(chkRts);
        config.Controls.Add(lblDsr);
        config.Controls.Add(lblCts);

        var send = AddGroup(main, "Nadawanie tekstowe / PING / transakcja");
        txtSend = new TextBox { Width = 650 };
        send.Controls.Add(txtSend);
        btnSend = AddButton(send, "Wyślij", btnSend_Click);
        btnPing = AddButton(send, "PING", btnPing_Click);

        send.Controls.Add(new Label { Text = "Timeout [ms]:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        txtTransactionTimeout = new TextBox { Width = 70, Text = "1000" };
        send.Controls.Add(txtTransactionTimeout);
        btnTransaction = AddButton(send, "Transakcja", btnTransaction_Click);
        btnAutobaud = AddButton(send, "Autobaud", btnAutobaud_Click);

        var binary = AddGroup(main, "Tryb binarny HEX");
        txtHexSend = new TextBox { Width = 650, Text = "48 65 6C 6C 6F" };
        binary.Controls.Add(txtHexSend);
        btnSendHex = AddButton(binary, "Wyślij HEX", btnSendHex_Click);

        var receiveGroup = new GroupBox { Text = "Odbiór", Dock = DockStyle.Fill };
        var receivePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        receivePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        receivePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        txtReceive = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true };
        btnClearReceive = new Button { Text = "Wyczyść odbiór", Width = 130, Height = 30 };
        btnClearReceive.Click += (_, _) => { txtReceive.Clear(); _receiveBuffer.Clear(); };
        receivePanel.Controls.Add(txtReceive);
        receivePanel.Controls.Add(btnClearReceive);
        receiveGroup.Controls.Add(receivePanel);
        main.Controls.Add(receiveGroup);

        var logGroup = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        txtLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true };
        btnClearLog = new Button { Text = "Wyczyść log", Width = 120, Height = 30 };
        btnClearLog.Click += (_, _) => txtLog.Clear();
        logPanel.Controls.Add(txtLog);
        logPanel.Controls.Add(btnClearLog);
        logGroup.Controls.Add(logPanel);
        main.Controls.Add(logGroup);
    }

    private FlowLayoutPanel AddGroup(Control parent, string title)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Top, AutoSize = true };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        group.Controls.Add(panel);
        parent.Controls.Add(group);
        return panel;
    }

    private ComboBox AddCombo(Control parent, string label, int width)
    {
        parent.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        var combo = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
        parent.Controls.Add(combo);
        return combo;
    }

    private Button AddButton(Control parent, string text, EventHandler click)
    {
        var button = new Button { Text = text, Width = 95, Height = 28 };
        button.Click += click;
        parent.Controls.Add(button);
        return button;
    }

    private void LoadDefaultValues()
    {
        comboBaud.Items.AddRange(new object[] { "150", "300", "600", "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        comboBaud.SelectedItem = "9600";

        comboParity.Items.AddRange(new object[] { "None", "Even", "Odd" });
        comboParity.SelectedItem = "None";

        comboDataBits.Items.AddRange(new object[] { "7", "8" });
        comboDataBits.SelectedItem = "8";

        comboStopBits.Items.AddRange(new object[] { "One", "Two" });
        comboStopBits.SelectedItem = "One";

        comboTerminator.Items.AddRange(new object[] { "Brak", "CR", "LF", "CRLF", "Własny" });
        comboTerminator.SelectedItem = "CRLF";

        comboHandshake.Items.AddRange(new object[] { "Brak", "RTS/CTS", "DTR/DSR", "XON/XOFF" });
        comboHandshake.SelectedItem = "Brak";
    }

    private void RefreshPorts()
    {
        comboPort.Items.Clear();
        foreach (var port in SerialPort.GetPortNames())
            comboPort.Items.Add(port);

        if (comboPort.Items.Count > 0)
            comboPort.SelectedIndex = 0;

        Log("Odświeżono listę portów.");
    }

    private void btnRefresh_Click(object? sender, EventArgs e) => RefreshPorts();

    private void btnOpen_Click(object? sender, EventArgs e)
    {
        if (comboPort.SelectedItem == null)
        {
            MessageBox.Show("Nie wybrano portu COM.");
            return;
        }

        try
        {
            ClosePortIfOpen();
            _receiveBuffer.Clear();
            while (_receivedFrames.TryDequeue(out _)) { }

            var handshake = GetHandshake();

            _serialPort = new SerialPort
            {
                PortName = comboPort.SelectedItem.ToString()!,
                BaudRate = int.Parse(comboBaud.SelectedItem!.ToString()!),
                DataBits = int.Parse(comboDataBits.SelectedItem!.ToString()!),
                Parity = Enum.Parse<Parity>(comboParity.SelectedItem!.ToString()!),
                StopBits = Enum.Parse<StopBits>(comboStopBits.SelectedItem!.ToString()!),
                Handshake = handshake,
                Encoding = Encoding.ASCII,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = comboHandshake.SelectedItem?.ToString() == "DTR/DSR" || chkDtr.Checked
            };

            if (handshake != Handshake.RequestToSend)
                _serialPort.RtsEnable = chkRts.Checked;

            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.Open();

            chkDtr.Checked = _serialPort.DtrEnable;
            if (_serialPort.Handshake != Handshake.RequestToSend)
                chkRts.Checked = _serialPort.RtsEnable;

            UpdateLineStatus();

            Log($"Otwarto port {_serialPort.PortName}: {_serialPort.BaudRate}, {_serialPort.DataBits}{_serialPort.Parity.ToString()[0]}{_serialPort.StopBits}, Handshake={comboHandshake.SelectedItem}, Terminator={comboTerminator.SelectedItem}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd otwierania portu: {ex.Message}");
        }
    }

    private void btnClose_Click(object? sender, EventArgs e) => ClosePortIfOpen();

    private void ClosePortIfOpen()
    {
        if (_serialPort == null) return;

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.Close();
                Log("Zamknięto port.");
            }

            _serialPort.Dispose();
            _serialPort = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd zamykania portu: {ex.Message}");
        }
    }

    private void btnSend_Click(object? sender, EventArgs e) => SendText(txtSend.Text, true);

    private void btnSendHex_Click(object? sender, EventArgs e)
    {
        if (!IsPortReady()) return;

        try
        {
            byte[] bytes = ParseHex(txtHexSend.Text);
            byte[] terminator = Encoding.ASCII.GetBytes(GetTerminator());

            _serialPort!.Write(bytes, 0, bytes.Length);
            if (terminator.Length > 0)
                _serialPort.Write(terminator, 0, terminator.Length);

            Log($"TX HEX: {BitConverter.ToString(bytes).Replace("-", " ")}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd HEX: {ex.Message}");
        }
    }

    private async void btnPing_Click(object? sender, EventArgs e)
    {
        if (!IsPortReady()) return;

        _pingWatch.Restart();
        SendText("PING", true);
        Log("Wysłano PING.");

        await Task.Delay(3000);

        if (_pingWatch.IsRunning)
        {
            _pingWatch.Stop();
            Log("PING timeout - brak PONG.");
        }
    }

    private async void btnTransaction_Click(object? sender, EventArgs e)
    {
        if (!IsPortReady()) return;

        if (!int.TryParse(txtTransactionTimeout.Text, out int timeoutMs))
            timeoutMs = 1000;

        while (_receivedFrames.TryDequeue(out _)) { }

        SendText(txtSend.Text, true);
        Log($"Transakcja: oczekiwanie na odpowiedź maks. {timeoutMs} ms.");

        string? response = await WaitForFrameAsync(timeoutMs);

        if (response == null)
            Log("Transakcja: timeout.");
        else
            Log($"Transakcja: odpowiedź = {EscapeForLog(response)}");
    }

    private async void btnAutobaud_Click(object? sender, EventArgs e)
    {
        if (comboPort.SelectedItem == null)
        {
            MessageBox.Show("Wybierz port.");
            return;
        }

        int[] speeds = { 150, 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

        Log("Autobauding: start. Druga strona musi mieć uruchomiony program i odpowiadać PONG na PING.");

        foreach (int speed in speeds)
        {
            comboBaud.SelectedItem = speed.ToString();

            try
            {
                ClosePortIfOpen();
                btnOpen_Click(null, EventArgs.Empty);

                await Task.Delay(200);

                while (_receivedFrames.TryDequeue(out _)) { }

                _pingWatch.Restart();
                SendText("PING", true);

                string? response = await WaitForFrameAsync(500);

                if (response != null && response.Contains("PONG"))
                {
                    _pingWatch.Stop();
                    Log($"Autobauding: wykryto {speed} bit/s.");
                    return;
                }
            }
            catch
            {
                // Próbujemy następną prędkość.
            }
        }

        Log("Autobauding: nie wykryto parametrów.");
    }

    private void btnCheckLines_Click(object? sender, EventArgs e)
    {
        if (!IsPortReady()) return;
        UpdateLineStatus();
        Log($"Linie: DTR={_serialPort!.DtrEnable}, RTS={SafeRts()}, DSR={_serialPort.DsrHolding}, CTS={_serialPort.CtsHolding}");
    }

    private void SendText(string text, bool addTerminator)
    {
        if (!IsPortReady()) return;

        try
        {
            string data = addTerminator ? text + GetTerminator() : text;
            _serialPort!.Write(data);
            Log($"TX: {EscapeForLog(data)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd wysyłania: {ex.Message}");
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null) return;

        try
        {
            string data = _serialPort.ReadExisting();

            BeginInvoke(new Action(() =>
            {
                txtReceive.AppendText(data);
                ProcessReceivedData(data);
                UpdateLineStatus();
            }));
        }
        catch
        {
            // Ignorujemy wyjątki przy zamykaniu portu.
        }
    }

    private void ProcessReceivedData(string data)
    {
        _receiveBuffer.Append(data);
        string terminator = GetTerminator();

        if (string.IsNullOrEmpty(terminator))
        {
            Log($"RX: {EscapeForLog(data)}");
            _receivedFrames.Enqueue(data);
            HandlePingPong(data);
            return;
        }

        while (true)
        {
            string buffer = _receiveBuffer.ToString();
            int index = buffer.IndexOf(terminator, StringComparison.Ordinal);

            if (index < 0) break;

            string message = buffer[..index];

            _receiveBuffer.Clear();
            _receiveBuffer.Append(buffer[(index + terminator.Length)..]);

            _receivedFrames.Enqueue(message);
            Log($"RX FRAME: {EscapeForLog(message)}");
            HandlePingPong(message);
        }
    }

    private void HandlePingPong(string data)
    {
        if (_serialPort == null || !_serialPort.IsOpen) return;

        if (data.Contains("PING"))
        {
            SendText("PONG", true);
            Log("Odebrano PING, odesłano PONG.");
        }

        if (data.Contains("PONG") && _pingWatch.IsRunning)
        {
            _pingWatch.Stop();
            Log($"Odebrano PONG. Round trip delay = {_pingWatch.ElapsedMilliseconds} ms.");
        }
    }

    private async Task<string?> WaitForFrameAsync(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (_receivedFrames.TryDequeue(out string? frame))
                return frame;

            await Task.Delay(20);
        }

        return null;
    }

    private void UpdateLineStatus()
    {
        if (_serialPort == null || !_serialPort.IsOpen) return;

        try
        {
            lblDsr.Text = $"DSR: {_serialPort.DsrHolding}";
            lblCts.Text = $"CTS: {_serialPort.CtsHolding}";
        }
        catch
        {
            lblDsr.Text = "DSR: ?";
            lblCts.Text = "CTS: ?";
        }
    }

    private bool SafeRts()
    {
        try { return _serialPort?.RtsEnable ?? false; }
        catch { return false; }
    }

    private bool IsPortReady()
    {
        if (_serialPort != null && _serialPort.IsOpen) return true;

        MessageBox.Show("Port nie jest otwarty.");
        return false;
    }

    private Handshake GetHandshake()
    {
        return comboHandshake.SelectedItem?.ToString() switch
        {
            "RTS/CTS" => Handshake.RequestToSend,
            "XON/XOFF" => Handshake.XOnXOff,
            _ => Handshake.None
        };
    }

    private string GetTerminator()
    {
        return comboTerminator.SelectedItem?.ToString() switch
        {
            "CR" => "\r",
            "LF" => "\n",
            "CRLF" => "\r\n",
            "Własny" => DecodeCustomTerminator(txtCustomTerminator.Text),
            _ => ""
        };
    }

    private static string DecodeCustomTerminator(string text)
    {
        return text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
    }

    private static byte[] ParseHex(string text)
    {
        string[] parts = text.Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            bytes[i] = Convert.ToByte(parts[i], 16);

        return bytes;
    }

    private static string EscapeForLog(string text)
    {
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private void Log(string message)
    {
        if (txtLog == null) return;
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ClosePortIfOpen();
        base.OnFormClosing(e);
    }
}