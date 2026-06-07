using System;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace RS232Lab;

public partial class Form1 : Form
{
    private SerialPort? _serialPort;

    private ComboBox comboPort = null!;
    private ComboBox comboBaud = null!;
    private ComboBox comboParity = null!;
    private ComboBox comboDataBits = null!;
    private ComboBox comboStopBits = null!;
    private ComboBox comboTerminator = null!;

    private Button btnRefresh = null!;
    private Button btnOpen = null!;
    private Button btnClose = null!;
    private Button btnSend = null!;

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
        Text = "RS232 Lab";
        Size = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };

        Controls.Add(main);

        var config = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        main.Controls.Add(config);

        comboPort = AddCombo(config, "Port:");
        comboBaud = AddCombo(config, "Baud:");
        comboParity = AddCombo(config, "Parity:");
        comboDataBits = AddCombo(config, "Data bits:");
        comboStopBits = AddCombo(config, "Stop bits:");
        comboTerminator = AddCombo(config, "Terminator:");

        btnRefresh = AddButton(config, "Odśwież", btnRefresh_Click);
        btnOpen = AddButton(config, "Otwórz", btnOpen_Click);
        btnClose = AddButton(config, "Zamknij", btnClose_Click);

        var sendPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        main.Controls.Add(sendPanel);

        txtSend = new TextBox
        {
            Width = 650
        };

        btnSend = AddButton(sendPanel, "Wyślij", btnSend_Click);

        sendPanel.Controls.Add(new Label
        {
            Text = "Nadawanie:",
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        });

        sendPanel.Controls.Add(txtSend);
        sendPanel.Controls.Add(btnSend);

        txtReceive = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Height = 250,
            ReadOnly = true
        };

        main.Controls.Add(new Label
        {
            Text = "Odbiór:",
            AutoSize = true
        });

        main.Controls.Add(txtReceive);

        txtLog = new RichTextBox
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            ReadOnly = true
        };

        Controls.Add(txtLog);
    }

    private ComboBox AddCombo(Control parent, string label)
    {
        parent.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Padding = new Padding(8, 6, 0, 0)
        });

        var combo = new ComboBox
        {
            Width = 100,
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
            Width = 90
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

        comboParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
        comboParity.SelectedItem = "None";

        comboDataBits.Items.AddRange(new object[] { "7", "8" });
        comboDataBits.SelectedItem = "8";

        comboStopBits.Items.AddRange(new object[] { "One", "Two" });
        comboStopBits.SelectedItem = "One";

        comboTerminator.Items.AddRange(new object[] { "Brak", "CR", "LF", "CRLF" });
        comboTerminator.SelectedItem = "CRLF";
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
            _serialPort = new SerialPort
            {
                PortName = comboPort.SelectedItem.ToString()!,
                BaudRate = int.Parse(comboBaud.SelectedItem!.ToString()!),
                DataBits = int.Parse(comboDataBits.SelectedItem!.ToString()!),
                Parity = Enum.Parse<Parity>(comboParity.SelectedItem!.ToString()!),
                StopBits = Enum.Parse<StopBits>(comboStopBits.SelectedItem!.ToString()!),
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.Open();

            Log($"Otwarto port {_serialPort.PortName}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd otwierania portu: {ex.Message}");
        }
    }

    private void btnClose_Click(object? sender, EventArgs e)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            Log("Zamknięto port.");
        }
    }

    private void btnSend_Click(object? sender, EventArgs e)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            MessageBox.Show("Port nie jest otwarty.");
            return;
        }

        string text = txtSend.Text;
        string data = text + GetTerminator();

        try
        {
            _serialPort.Write(data);
            Log($"TX: {text}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd wysyłania: {ex.Message}");
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null) return;

        string data = _serialPort.ReadExisting();

        BeginInvoke(() =>
        {
            txtReceive.AppendText(data);
            Log($"RX: {data}");
        });
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

    private void Log(string message)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}