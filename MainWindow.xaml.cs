using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MailClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpClient client;

        private Stream stream;
        private StreamWriter streamWriter;

        private BackgroundWorker worker;
        private string delim = "-----------------------------";

        public MainWindow()
        {
            InitializeComponent();
            GetConfig();

            this.Title = "MailClient - " + this.ServerAddress.Text + ":" + this.ServerPort.Text;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Cleanup();
        }

        private void Cleanup()
        {
            if (this.streamWriter != null)
            {
                this.streamWriter.Dispose();
                this.streamWriter = null;
            }

            if (this.stream != null)
            {
                this.stream.Dispose();
                this.stream = null;
            }

            if (this.client != null)
            {
                if (this.client.Connected)
                {
                    this.client.Close();
                }

                this.client = null;
            }
        }

        private void OnMailStringSubmit_Click(object sender, RoutedEventArgs e)
        {
            // something is being processed
            if (this.worker != null)
            {
                return;
            }

            ClearTrace();

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.WorkerSupportsCancellation = false;
            this.worker.DoWork += new DoWorkEventHandler(DoMailSubmit);
            this.worker.ProgressChanged += new ProgressChangedEventHandler(OnProgressChanged);
            this.worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnWorkerCompleted);

            MailSubmitData data = new MailSubmitData();
            data.StartCommand = this.Start_1.SelectedIndex == 0 ? "EHLO" : "HELO";
            data.ServerAddess = this.ServerAddress.Text;
            data.ServerPort = this.ServerPort.Text;
            data.FromAddress = this.FromAddress_1.Text;
            data.FromOption = this.FromOption_1.Text;
            data.ToAddress = this.ToAddress_1.Text;
            data.ToOption = this.ToOption_1.Text;
            data.MailData = this.MailData.Text;
            data.DataOption = this.DataOption_1.Text;
            data.DotPad = this.DotPad_1.IsChecked.HasValue ? this.DotPad_1.IsChecked.Value : true;

            this.worker.RunWorkerAsync(data);
        }

        private void OnMailFileSubmit_Click(object sender, RoutedEventArgs e)
        {
            // something is being processed
            if (this.worker != null)
            {
                return;
            }

            ClearTrace();

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.WorkerSupportsCancellation = false;
            this.worker.DoWork += new DoWorkEventHandler(DoMailSubmit);
            this.worker.ProgressChanged += new ProgressChangedEventHandler(OnProgressChanged);
            this.worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnWorkerCompleted);

            MailSubmitData data = new MailSubmitData();
            data.StartCommand = this.Start_2.SelectedIndex == 0 ? "EHLO" : "HELO";
            data.ServerAddess = this.ServerAddress.Text;
            data.ServerPort = this.ServerPort.Text;
            data.FromAddress = this.FromAddress_2.Text;
            data.FromOption = this.FromOption_2.Text;
            data.ToAddress = this.ToAddress_2.Text;
            data.ToOption = this.ToOption_2.Text;
            data.MailDataFile = this.MailDataFile.Text;
            data.DataOption = this.DataOption_2.Text;
            data.DotPad = this.DotPad_2.IsChecked.HasValue ? this.DotPad_2.IsChecked.Value : true;

            this.worker.RunWorkerAsync(data);
        }

        private void DoMailSubmit(object sender, DoWorkEventArgs workArgs)
        {
            MailSubmitData data = workArgs.Argument as MailSubmitData;
            string dataToSend;
            bool success = true;

            if (success)
            {
                dataToSend = data.StartCommand;
                success = SendToServer(data.ServerAddess, data.ServerPort, dataToSend);
                AddTraceAsync(this.delim);
            }

            if (success)
            {
                dataToSend = "MAIL FROM:" + data.FromAddress;
                if (!string.IsNullOrEmpty(data.FromOption))
                {
                    dataToSend += " " + data.FromOption;
                }
                success = SendToServer(data.ServerAddess, data.ServerPort, dataToSend);
                AddTraceAsync(this.delim);
            }

            if (success)
            {
                dataToSend = "RCPT TO:" + data.ToAddress;
                if (!string.IsNullOrEmpty(data.ToOption))
                {
                    dataToSend += " " + data.ToOption;
                }
                success = SendToServer(data.ServerAddess, data.ServerPort, dataToSend);
                AddTraceAsync(this.delim);
            }

            if (success)
            {
                dataToSend = "DATA";
                if (!string.IsNullOrEmpty(data.DataOption))
                {
                    dataToSend += " " + data.DataOption;
                }
                success = SendToServer(data.ServerAddess, data.ServerPort, dataToSend);
                AddTraceAsync(this.delim);
            }

            if (success)
            {
                AddTraceAsync(string.Format("Dot padding - {0}", data.DotPad));
                AddTraceAsync(this.delim);

                if (!string.IsNullOrEmpty(data.MailDataFile))
                {
                    byte[] mailBytes = File.ReadAllBytes(data.MailDataFile);
                    if (data.DotPad)
                    {
                        byte[] tmpBytes = DotPad(mailBytes);
                        mailBytes = tmpBytes;
                    }
                    success = SendToServer(data.ServerAddess, data.ServerPort, mailBytes, false);
                }
                else
                {
                    string[] mailParts = data.MailData.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    foreach (string part in mailParts)
                    {
                        string tmpPart = part;
                        if (data.DotPad)
                        {
                            tmpPart = DotPad(part);
                        }
                        success = SendToServer(data.ServerAddess, data.ServerPort, tmpPart, false);
                        if (!success)
                        {
                            break;
                        }
                    }
                }
                AddTraceAsync(this.delim);
            }

            if (success)
            {
                dataToSend = ".";
                success = SendToServer(data.ServerAddess, data.ServerPort, dataToSend);
                AddTraceAsync(this.delim);
            }

            Cleanup();
        }

        private void OnMailRawSubmit_Click(object sender, RoutedEventArgs e)
        {
            // something is being processed
            if (this.worker != null)
            {
                return;
            }

            ClearTrace();

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.WorkerSupportsCancellation = false;
            this.worker.DoWork += new DoWorkEventHandler(DoMailRawSubmit);
            this.worker.ProgressChanged += new ProgressChangedEventHandler(OnProgressChanged);
            this.worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnWorkerCompleted);

            MailSubmitData data = new MailSubmitData();
            data.ServerAddess = this.ServerAddress.Text;
            data.ServerPort = this.ServerPort.Text;
            data.RawData = this.ContentData.Text;

            this.worker.RunWorkerAsync(data);
        }

        private void DoMailRawSubmit(object sender, DoWorkEventArgs workArgs)
        {
            MailSubmitData data = workArgs.Argument as MailSubmitData;
            SendToServer(data.ServerAddess, data.ServerPort, data.RawData);
            AddTraceAsync(this.delim);
            Cleanup();
        }

        private void OnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            bool? result = dlg.ShowDialog();
            if (result.HasValue && result.Value == true)
            {
                this.MailDataFile.Text = dlg.FileName;
            }
        }

        private void OnProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string trace = e.UserState as string;
            AddTrace(trace);
        }

        private void OnWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.worker = null;
        }

        private void GetConfig()
        {
            try
            {
                StreamReader reader = new StreamReader("config.txt");

                while (true)
                {
                    if (reader.EndOfStream)
                        break;

                    string str = reader.ReadLine();
                    if (str.StartsWith("Server:"))
                    {
                        str = str.Substring(7);
                        int delim = str.IndexOf(' ');
                        if (delim >= 0)
                        {
                            this.ServerAddress.Text = str.Substring(0, delim);
                            this.ServerPort.Text = str.Substring(delim + 1);
                        }
                        else
                        {
                            this.ServerAddress.Text = str;
                        }
                    }
                    else if (str.StartsWith("From:"))
                    {
                        this.FromAddress_1.Text = str.Substring(5);
                        this.FromAddress_2.Text = str.Substring(5);
                    }
                    else if (str.StartsWith("To:"))
                    {
                        this.ToAddress_1.Text = str.Substring(3);
                        this.ToAddress_2.Text = str.Substring(3);
                    }
                    else
                    {
                        string str2 = reader.ReadToEnd();
                        this.MailData.Text = str + "\r\n" + str2;
                    }
                }

                reader.Close();
            }
            catch (Exception)
            {
            }
        }

        private void ClearTrace()
        {
            this.NetworkTrace.Text = string.Empty;
            this.NetworkTraceScroll.ScrollToLeftEnd();
            this.NetworkTraceScroll.ScrollToTop();
        }

        private void AddTrace(string trace)
        {
            this.NetworkTrace.Text += trace + Environment.NewLine;
            this.NetworkTraceScroll.ScrollToLeftEnd();
            this.NetworkTraceScroll.ScrollToBottom();
        }

        private void AddTraceAsync(string trace)
        {
            this.worker.ReportProgress(10, trace);
        }

        private bool SendToServer(string serverAddress, string serverPort, string data, bool waitForResponse = true)
        {
            if (this.client == null)
            {
                try
                {

                    this.client = new System.Net.Sockets.TcpClient(serverAddress, int.Parse(serverPort));

                    this.stream = this.client.GetStream();
                    this.streamWriter = new StreamWriter(this.stream) { AutoFlush = true };
                    if (waitForResponse && !ReadFromServer())
                    {
                        throw new Exception("Failed to read from server.");
                    }
                }
                catch (Exception ex)
                {
                    string error = ex.Message;

                    Cleanup();
                    return false;
                }
            }

            try
            {
                AddTraceAsync(data.Substring(0, Math.Min(data.Length, 1000)));
                this.streamWriter.WriteLine(data);
                if (waitForResponse && !ReadFromServer())
                {
                    throw new Exception("Failed to read from server.");
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                return false;
            }

            return true;
        }

        private bool SendToServer(string serverAddress, string serverPort, byte[] data, bool waitForResponse = true)
        {
            if (this.client == null)
            {
                try
                {

                    this.client = new System.Net.Sockets.TcpClient(serverAddress, int.Parse(serverPort));

                    this.stream = this.client.GetStream();
                    this.streamWriter = new StreamWriter(this.stream) { AutoFlush = true };
                    if (waitForResponse && !ReadFromServer())
                    {
                        throw new Exception("Failed to read from server.");
                    }
                }
                catch (Exception ex)
                {
                    string error = ex.Message;

                    Cleanup();
                    return false;
                }
            }

            try
            {
                string data_string = Encoding.UTF8.GetString(data);
                AddTraceAsync(data_string.Substring(0, Math.Min(data_string.Length, 1000)));
                this.stream.Write(data, 0, data.Length);
                if (waitForResponse && !ReadFromServer())
                {
                    throw new Exception("Failed to read from server.");
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                return false;
            }

            return true;
        }


        private bool ReadFromServer()
        {
            bool success = true;
            var reader = new StreamReader(this.stream);
            reader.BaseStream.ReadTimeout = 3000000;

            try
            {
                bool readMoreLines = true;
                while (readMoreLines)
                {
                    readMoreLines = false;

                    string data = reader.ReadLine();
                    if (string.IsNullOrEmpty(data))
                    {
                        throw new Exception("No data from server.");
                    }
                    else
                    {
                        AddTraceAsync(data);

                        if (data.Length < 3)
                        {
                            throw new Exception("Invalid data from server.");
                        }
                        else
                        {
                            if ((data.Length > 3) && (data[3] == '-'))
                            {
                                readMoreLines = true;
                            }

                            char statusCode = data[0];
                            switch (statusCode)
                            {
                                case '2':
                                case '3':
                                    success = true;
                                    break;
                                case '4':
                                case '5':
                                    success = false;
                                    break;
                                default:
                                    success = false;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                success = false;
            }

            return success;
        }

        private byte[] DotPad(byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                return input;
            }

            List<byte> output = new List<byte>();
            if (input[0] == '.')
            {
                output.Add((byte)'.');
            }

            for (int i = 0; i < input.Length; i++)
            {
                if ((input[i] == '\n') &&
                    (i + 1 < input.Length) &&
                    (input[i + 1] == '.'))
                {
                    output.Add(input[i]);
                    output.Add((byte)'.');
                }
                else
                {
                    output.Add(input[i]);
                }
            }

            return output.ToArray();
        }

        private string DotPad(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            if (input[0] == '.')
            {
                input = "." + input;
            }

            int index = 0;
            do
            {
                index = input.IndexOf('\n', index);
                if (index != -1)
                {
                    if ((index + 1 < input.Length) &&
                        (input[index + 1] == '.'))
                    {
                        input = input.Substring(0, index + 1) + "." + input.Substring(index + 1);
                    }

                    index++;

                    if (index >= input.Length)
                    {
                        index = -1;
                    }
                }
            }
            while (index != -1);

            return input;
        }

        private class MailSubmitData
        {
            public string ServerAddess { get; set; }
            public string ServerPort { get; set; }

            public string StartCommand { get; set; }
            public string FromAddress { get; set; }
            public string FromOption { get; set; }
            public string ToAddress { get; set; }
            public string ToOption { get; set; }
            public string MailData { get; set; }
            public string MailDataFile { get; set; }
            public string DataOption { get; set; }
            public bool DotPad { get; set; }
           
            public string RawData { get; set; }
        }
    }
}
