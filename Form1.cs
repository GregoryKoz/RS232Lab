using System;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace RS232Lab;

public partial class Form1 : Form
{
    private SerialPort? _serialPort;
    private readonly Stopwatch _pingWatch = new();

    private ComboBox comboPort = null!;
    private ComboBox comboBaud = null!;
    private ComboBox comboParity = null!;
    private ComboBox comboDataBits = null!;
    private ComboBox comboStopBits = null!;
    private ComboBox comboTerminator = null!;
    private ComboBox comboHandshake = null!;

    private Button btnRefresh = null!;
    private Button btnOpen = null!;
    private Button btnClose = null!;
    private Button btnSend = null!;
    private Button btnPing = null!;
    private Button btnClearReceive = null!;
    private Button btnClearLog = null!;

    private TextBox txtSend = null!;
    private RichTextBox txtReceive = null!;
    private RichTextBox txtLog = null!;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        LoadDefaultValues();
        RefreshPorts();
    }

    private void BuildUi()
    {
        Text = "RS232 Lab - komunikacja przez port szeregowy";
        Size = new Size(1550, 720);
        StartPosition = FormStartPosition.CenterScreen;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };

        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        Controls.Add(main);

        var configGroup = new GroupBox
        {
            Text = "Konfiguracja łącza",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var config = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };

        configGroup.Controls.Add(config);
        main.Controls.Add(configGroup);

        comboPort = AddCombo(config, "Port:", 90);
        comboBaud = AddCombo(config, "Baud:", 90);
        comboParity = AddCombo(config, "Parity:", 90);
        comboDataBits = AddCombo(config, "Data bits:", 70);
        comboStopBits = AddCombo(config, "Stop bits:", 80);
        comboTerminator = AddCombo(config, "Terminator:", 90);
        comboHandshake = AddCombo(config, "Handshake:", 100);

        btnRefresh = AddButton(config, "Odśwież", btnRefresh_Click);
        btnOpen = AddButton(config, "Otwórz", btnOpen_Click);
        btnClose = AddButton(config, "Zamknij", btnClose_Click);

        var sendGroup = new GroupBox
        {
            Text = "Nadawanie",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var sendPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };

        sendGroup.Controls.Add(sendPanel);
        main.Controls.Add(sendGroup);

        txtSend = new TextBox
        {
            Width = 650
        };

        sendPanel.Controls.Add(txtSend);

        btnSend = AddButton(sendPanel, "Wyślij", btnSend_Click);
        btnPing = AddButton(sendPanel, "PING", btnPing_Click);

        var receiveGroup = new GroupBox
        {
            Text = "Odbiór",
            Dock = DockStyle.Fill
        };

        var receivePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };

        receivePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        receivePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        txtReceive = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        btnClearReceive = new Button
        {
            Text = "Wyczyść odbiór",
            Width = 130,
            Height = 30
        };
        btnClearReceive.Click += (_, _) => txtReceive.Clear();

        receivePanel.Controls.Add(txtReceive);
        receivePanel.Controls.Add(btnClearReceive);

        receiveGroup.Controls.Add(receivePanel);
        main.Controls.Add(receiveGroup);

        var logGroup = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Fill
        };

        var logPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };

        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        btnClearLog = new Button
        {
            Text = "Wyczyść log",
            Width = 120,
            Height = 30
        };
        btnClearLog.Click += (_, _) => txtLog.Clear();

        logPanel.Controls.Add(txtLog);
        logPanel.Controls.Add(btnClearLog);

        logGroup.Controls.Add(logPanel);
        main.Controls.Add(logGroup);
    }

    private ComboBox AddCombo(Control parent, string label, int width)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Padding = new Padding(8, 6, 0, 0)
        });

        var combo = new ComboBox
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        parent.Controls.Add(combo);
        return combo;
    }

    private Button AddButton(Control parent, string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Width = 90,
            Height = 28
        };

        button.Click += click;
        parent.Controls.Add(button);
        return button;
    }

    private void LoadDefaultValues()
    {
        comboBaud.Items.AddRange(new object[]
        {
            "150", "300", "600", "1200", "2400", "4800",
            "9600", "19200", "38400", "57600", "115200"
        });
        comboBaud.SelectedItem = "9600";

        comboParity.Items.AddRange(new object[] { "None", "Even", "Odd" });
        comboParity.SelectedItem = "None";

        comboDataBits.Items.AddRange(new object[] { "7", "8" });
        comboDataBits.SelectedItem = "8";

        comboStopBits.Items.AddRange(new object[] { "One", "Two" });
        comboStopBits.SelectedItem = "One";

        comboTerminator.Items.AddRange(new object[] { "Brak", "CR", "LF", "CRLF" });
        comboTerminator.SelectedItem = "CRLF";

        comboHandshake.Items.AddRange(new object[]
        {
            "Brak",
            "RTS/CTS",
            "XON/XOFF"
        });
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

    private void btnRefresh_Click(object? sender, EventArgs e)
    {
        RefreshPorts();
    }

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

            _serialPort = new SerialPort
            {
                PortName = comboPort.SelectedItem.ToString()!,
                BaudRate = int.Parse(comboBaud.SelectedItem!.ToString()!),
                DataBits = int.Parse(comboDataBits.SelectedItem!.ToString()!),
                Parity = Enum.Parse<Parity>(comboParity.SelectedItem!.ToString()!),
                StopBits = Enum.Parse<StopBits>(comboStopBits.SelectedItem!.ToString()!),
                Handshake = GetHandshake(),
                Encoding = Encoding.ASCII,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.Open();

            Log($"Otwarto port {_serialPort.PortName}: " +
                $"{_serialPort.BaudRate}, {_serialPort.DataBits}{_serialPort.Parity.ToString()[0]}{_serialPort.StopBits}, " +
                $"Handshake={comboHandshake.SelectedItem}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd otwierania portu: {ex.Message}");
        }
    }

    private void btnClose_Click(object? sender, EventArgs e)
    {
        ClosePortIfOpen();
    }

    private void ClosePortIfOpen()
    {
        if (_serialPort == null)
            return;

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

    private void btnSend_Click(object? sender, EventArgs e)
    {
        SendText(txtSend.Text, true);
    }

    private void btnPing_Click(object? sender, EventArgs e)
    {
        if (!IsPortReady())
            return;

        _pingWatch.Restart();
        SendText("PING", true);
        Log("Wysłano PING.");
    }

    private void SendText(string text, bool addTerminator)
    {
        if (!IsPortReady())
            return;

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
        if (_serialPort == null)
            return;

        try
        {
            string data = _serialPort.ReadExisting();

            BeginInvoke(() =>
            {
                txtReceive.AppendText(data);
                Log($"RX: {EscapeForLog(data)}");

                HandlePingPong(data);
            });
        }
        catch
        {
            // Przy zamykaniu portu może pojawić się wyjątek, ignorujemy.
        }
    }

    private void HandlePingPong(string data)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            return;

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

    private bool IsPortReady()
    {
        if (_serialPort != null && _serialPort.IsOpen)
            return true;

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
            _ => ""
        };
    }

    private static string EscapeForLog(string text)
    {
        return text
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private void Log(string message)
    {
        if (txtLog == null)
            return;

        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ClosePortIfOpen();
        base.OnFormClosing(e);
    }
}