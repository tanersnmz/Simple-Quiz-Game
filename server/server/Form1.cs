using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{
    public struct player
    {
        public string name;
        public double points;
        public Socket socket;

        public player(string name, Socket socket)
        {
            this.name = name;
            this.points = 0.0;
            this.socket = socket;
        }

        public void addPoint(double point)
        {
            this.points += point;
        }
    }

    public struct question
    {
        public string questionString;
        public int answer;

        public question(string questionString, int answer)
        {
            this.questionString = questionString;
            this.answer = answer;
        }
    }

    public partial class Form1 : Form
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<player> users = new List<player>();
        List<question> questions = new List<question>();

        int numOfQuestions = 0;
        bool terminating = false;
        bool listening = false;
        bool isEnoughUser = false;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }
        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void buttonListen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if (Int32.TryParse(textBoxPort.Text, out serverPort))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                serverSocket.Bind(endPoint);
                serverSocket.Listen(999);

                listening = true;
                textBoxPort.Enabled = false;
                buttonListen.Enabled = false;

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                richTextBoxLogs.AppendText("Started listening on port: " + serverPort + "\n");
                Int32.TryParse(textBoxNumberOfQuestions.Text, out numOfQuestions);
            }
            else
            {
                richTextBoxLogs.AppendText("Please check port number \n");
            }
        }

        private bool isNameExists(string newName)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (newName == users[i].name)
                {
                    return true;
                }
            }
            return false;
        }

        private void readQuestionsTxt()
        {
            richTextBoxLogs.AppendText("in read questions\n");
            string[] lines = System.IO.File.ReadAllLines(@"C:\Users\Ahmet Furkan\Documents\GitHub\CS408_Project\server\server\questions.txt");
            for(int i=0; i<lines.Length; i=i+2)
            {
                question newQuestion = new question(lines[i], Int32.Parse(lines[i + 1]));
                questions.Add(newQuestion);
            }
            richTextBoxLogs.AppendText("end read questions\n");
        }

        private void Accept()
        {
            bool temp = true;
            while (listening)
            {
                try
                {
                    while(!isEnoughUser)
                    {
                        
                        Socket newClient = serverSocket.Accept();
                        Byte[] buffer = new Byte[128];
                        newClient.Receive(buffer);
                        string nameOfClient = Encoding.Default.GetString(buffer);
                        nameOfClient = nameOfClient.Substring(0, nameOfClient.IndexOf("\0"));
                        //check unique name
                        if (!isNameExists(nameOfClient))
                        {
                            users.Add(new player(nameOfClient, newClient));

                            richTextBoxLogs.AppendText(nameOfClient + " is connected.\n");
                            if (users.Count == 2)
                            {
                                isEnoughUser = true;
                            }
                        }
                        else
                        {
                            richTextBoxLogs.AppendText(nameOfClient + " tries to connect but this name already connected.\n");
                            buffer = Encoding.Default.GetBytes("Connection Failed! This name is already in use. Try with another name.");
                            newClient.Send(buffer);
                        }
                    }

                    if (temp)
                    {
                        temp = false;
                        readQuestionsTxt();
                        Quiz();
                    }
                        

                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        richTextBoxLogs.AppendText("The socket stopped working.\n");
                    }

                }
            }
        }

        private void sendMessage(Socket socket, string message)
        {
            if (message != "" && message.Length <= 128)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                try
                {
                    socket.Send(buffer);
                }
                catch
                {
                    richTextBoxLogs.AppendText("There is a problem! Check the connection...\n");
                    terminating = true;
                    textBoxPort.Enabled = true;
                    buttonListen.Enabled = true;
                    serverSocket.Close();
                }
            }
        }

        private string getScores()
        {
            string result = "Score Table:\n";
            var sortedList = users.OrderByDescending(x => x.points);
            foreach (player user in sortedList)
            {
                result += user.name + ": " + user.points.ToString() + "\n";
            }
            return result;
        }

        private int sendQuestion(string question, player user)
        {
            Byte[] buffer = Encoding.Default.GetBytes(question);
            try
            {
                user.socket.Send(buffer);
            }
            catch
            {
                richTextBoxLogs.AppendText("There is a problem! Check the connection...\n");
                terminating = true;
                textBoxPort.Enabled = true;
                buttonListen.Enabled = true;
                serverSocket.Close();
            }

            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    Byte[] incomingBuffer = new Byte[64];
                    user.socket.Receive(incomingBuffer);

                    string incomingMessage = Encoding.Default.GetString(incomingBuffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    if(incomingMessage.Length != 0)
                    {
                        return Int32.Parse(incomingMessage);
                    }

                }
                catch
                {
                    if (!terminating)
                    {
                        richTextBoxLogs.AppendText(user.name+" has disconnected\n");
                    }
                    user.socket.Close();
                    users.Remove(user);
                    connected = false;
                }
            }
            return Int32.MinValue;
        }

        private int compareResult(int firstUserAnswer, int secondUserAnswer, int actualResult)
        {
            if (Math.Abs(actualResult - firstUserAnswer) < Math.Abs(actualResult - secondUserAnswer))
            {
                return 0;
            }
            else if (Math.Abs(actualResult - firstUserAnswer) > Math.Abs(actualResult - secondUserAnswer))
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        private void Quiz()
        {
            richTextBoxLogs.AppendText("in quiz\n");
            for (int i = 0; i<numOfQuestions; i++)
            {
                int nThreadCount = users.Count;
                int[] answers = new int[nThreadCount];
                Thread[] workerThreads = new Thread[nThreadCount];

                for (int k = 0; k< nThreadCount; k++)
                {
                    richTextBoxLogs.AppendText(nThreadCount.ToString() + "\n");
                    workerThreads[k] = new Thread(() => { answers[k] = sendQuestion(questions[i%questions.Count].questionString, users[k]); });
                    workerThreads[k].Start();
                }

                for (int l = 0; l < nThreadCount; l++)
                {
                    workerThreads[l].Join();
                }

                for (int k = 0; k < nThreadCount; k++)
                {
                    sendMessage(users[k].socket, users[0].name + "\'s answer is " + answers[0].ToString() + "\n"+
                                                 users[1].name + "\'s answer is " + answers[1].ToString() + "\n"+
                                                 "The correct answer is " + questions[i].answer.ToString() + "\n");
                }

                int result = compareResult(answers[0], answers[1], questions[i].answer);
                if (result == 0)
                {
                    users[0].addPoint(1);
                    sendMessage(users[0].socket, "You win! You get 1 point\n");
                    sendMessage(users[1].socket, "You lose! You get 0 poıint\n");
                }
                else if (result == 1)
                {
                    users[1].addPoint(1);
                    sendMessage(users[1].socket, "You win! You get 1 point\n");
                    sendMessage(users[0].socket, "You lose! You get 0 point\n");
                }
                else
                {
                    users[0].addPoint(0.5);
                    users[1].addPoint(0.5);
                    sendMessage(users[1].socket, "Tie! You get 0.5 point\n");
                    sendMessage(users[0].socket, "Tie! You get 0.5 point\n");
                }

                for (int k = 0; k < nThreadCount; k++)
                {
                    sendMessage(users[k].socket, getScores());
                }
            }


            for (int k = 0; k < users.Count; k++)
            {
                sendMessage(users[k].socket, "The quiz is over!");
                sendMessage(users[k].socket, getScores());
            }
        }
    }
}
