using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;
        string name = "";
        int currentQuestion = 0;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminating = true;
            clientSocket.Close();
            Environment.Exit(0);
        }

        private void sendMessage(Socket socket, string message)
        {
            if (message != "" && message.Length <= 64)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
            }
        }

        private string receiveMessage(Socket socket)
        {
            lock (this)
            {
                Byte[] buffer = new Byte[1024];
                socket.Receive(buffer);
                string message = Encoding.Default.GetString(buffer);
                message = message.Substring(0, message.IndexOf("\0"));
                return message;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBoxIp.Text;

            int portNum;
            if (Int32.TryParse(textBoxPort.Text, out portNum))
            {
                try
                {
                    clientSocket.Connect(IP, portNum);
                    sendMessage(clientSocket, textBoxName.Text);
                    string reply = receiveMessage(clientSocket);
                    if(reply.Contains("This name is already in use"))
                    {
                        logs.AppendText(reply);
                        clientSocket.Close();
                    }

                    else
                    {
                        buttonConnect.Enabled = false;
                        buttonDisconnect.Enabled = true;
                        textBoxIp.Enabled = false;
                        textBoxPort.Enabled = false;
                        textBoxName.Enabled = false;
                        connected = true;

                        logs.AppendText("Connected to the server!\n");
                        logs.AppendText("Waiting for the quiz to start...\n");

                        Thread receiveThread = new Thread(Receive);
                        receiveThread.IsBackground = true;
                        receiveThread.Start();
                    }
                }
                catch
                {
                    logs.AppendText("Could not connect to the server!\n");
                }
            }
            else
            {
                logs.AppendText("Check the port\n");
            }
        }

        private void handleQuestion(string message)
        {
            lock (this)
            {
                string[] splittedMessage = message.Split(':');
                string qNumber = splittedMessage[0];
                currentQuestion = Int32.Parse(qNumber);
                string question = splittedMessage[1];
                logs.AppendText("Question " + (currentQuestion + 1).ToString() + " - " + question + "\n");
                textBoxAnswer.Enabled = true;
                buttonSend.Enabled = true;
            }
        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    string incomingMessage = receiveMessage(clientSocket);
                    if (incomingMessage.Contains(":"))
                    {
                        string[] splittedMessage = incomingMessage.Split(':');
                        string qNumber = splittedMessage[0];
                        int qnum;
                        if(Int32.TryParse(qNumber, out qnum))
                        {
                            handleQuestion(incomingMessage);
                        }
                        else
                        {
                            logs.AppendText(incomingMessage + "\n");
                        }
                        
                    }
                    else if(incomingMessage != "")
                    {
                        logs.AppendText("Server: " + incomingMessage + "\n");
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        logs.AppendText("The server has disconnected\n");
                        buttonConnect.Enabled = true;
                        buttonDisconnect.Enabled = false;
                        textBoxIp.Enabled = true;
                        textBoxPort.Enabled = true;
                        textBoxName.Enabled = true;
                    }

                    clientSocket.Close();
                    connected = false;
                }

            }
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            string message = currentQuestion.ToString() + ":" + textBoxAnswer.Text;
            sendMessage(clientSocket, message);
            textBoxAnswer.Enabled = false;
            buttonSend.Enabled = false;
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            clientSocket.Close();
            connected = false;
            buttonConnect.Enabled = true;
            buttonDisconnect.Enabled = false;
            textBoxIp.Enabled = true;
            textBoxPort.Enabled = true;
            textBoxName.Enabled = true;
            textBoxAnswer.Enabled = false;
            buttonSend.Enabled = false;
        }
    }
}
