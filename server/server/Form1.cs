using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{
    public struct player
    {
        public int id;
        public string name;
        public double points;
        public Socket socket;

        public player(int id, string name, Socket socket)
        {
            this.id = id;
            this.name = name;
            this.points = 0.0;
            this.socket = socket;
        }

        public player addPoint(double point)
        {
            this.points = this.points + point;
            return this;
        }

        public player zeroPoint()
        {
            this.points = 0;
            return this;
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
        int[] answers;

        int numOfQuestions = 0;
        bool terminating = false;
        bool listening = false;
        bool isEnoughUser = false;
        int answeredUserCount = 0;
        int currentQuestionNumber = 0;
        int idCount = 0;
        bool isQuizStarted = false;
        int disconnectedCount = 0;

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
            serverSocket.Close();
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
                textBoxNumberOfQuestions.Enabled = false;
                buttonListen.Enabled = false;

                Thread acceptThread = new Thread(Accept);
                acceptThread.IsBackground = true;
                acceptThread.Start();

                richTextBoxLogs.AppendText("Started listening on port " + serverPort + "\n");
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
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"..\..\questions.txt");
            List<string> lines = File.ReadLines(path).ToList();
            for (int i=0; i<lines.Count; i=i+2)
            {
                question newQuestion = new question(lines[i], Int32.Parse(lines[i + 1]));
                questions.Add(newQuestion);
            }
        }

        private void terminateQuiz()
        {
            textBoxPort.Enabled = true;
            textBoxNumberOfQuestions.Enabled = true;
            buttonListen.Enabled = true;
            isQuizStarted = false;

            for (int k = 0; k < users.Count; k++)
            {
                users[k].socket.Close();
            }

            users.Clear();
            questions.Clear();
            serverSocket.Close();

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            numOfQuestions = 0;
            terminating = false;
            listening = false;
            isEnoughUser = false;
            answeredUserCount = 0;
            currentQuestionNumber = 0;
            idCount = 0;
            isQuizStarted = false;
            disconnectedCount = 0;
        }

        private void Accept()
        {
            while (listening)
            {
                try
                {
                    while(!isEnoughUser)
                    {
                        Socket newClient = serverSocket.Accept();
                        Byte[] buffer = new Byte[1024];
                        newClient.Receive(buffer);
                        string nameOfClient = Encoding.Default.GetString(buffer);
                        nameOfClient = nameOfClient.Substring(0, nameOfClient.IndexOf("\0"));
                        //check unique name
                        if (!isNameExists(nameOfClient))
                        {
                            player newUser = new player(idCount, nameOfClient, newClient);
                            users.Add(newUser);
                            idCount++;
                            sendMessage(newClient, "Connection Success!");

                            Thread receiveThread = new Thread(() => Receive(newUser));
                            receiveThread.IsBackground = true;
                            receiveThread.Start();

                            richTextBoxLogs.AppendText(nameOfClient + " is connected.\n");
                            if (users.Count == 2)
                            {
                                isEnoughUser = true;
                            }
                        }
                        else
                        {
                            richTextBoxLogs.AppendText(nameOfClient + " tries to connect but this name already connected.\n");
                            sendMessage(newClient, "Connection Failed! This name is already in use. Try with another name.");
                            newClient.Close();
                        }
                    }

                    Thread externalUserThread = new Thread(externalUser);
                    externalUserThread.IsBackground = true;
                    externalUserThread.Start();

                    if (!updateOrGetIsQuizStarted(1))
                    {
                        answers = new int[users.Count];
                        updateOrGetIsQuizStarted(3);
                        readQuestionsTxt();
                        Quiz();
                        terminateQuiz();
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


        private void externalUser()
        {
            while (listening)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();
                    sendMessage(newClient, "Connection Failed! There is a running quiz right now!");
                    newClient.Close();
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

        private string receiveMessage(Socket socket)
        {
            Byte[] buffer = new Byte[1024];
            socket.Receive(buffer);
            string message = Encoding.Default.GetString(buffer);
            message = message.Substring(0, message.IndexOf("\0"));
            return message;
        }

        private void sendMessage(Socket socket, string message)
        {
            if (message != "" && message.Length <= 1024)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                try
                {
                    socket.Send(buffer);
                }
                catch
                {
                    richTextBoxLogs.AppendText("There is a problem in sendMessage! Check the connection...\n");
                    terminating = true;
                    textBoxPort.Enabled = true;
                    buttonListen.Enabled = true;
                    serverSocket.Close();
                }
            }
            Thread.Sleep(1000);
        }

        private void proccessAnswer(ref player player, ref bool connected, ref string message)
        {
            lock (this)
            {
                string[] splittedMessage = message.Split(':');
                string qNumber = splittedMessage[0];
                string answerString = splittedMessage[1];
                int answer;
                if(Int32.TryParse(answerString, out answer))
                {
                    if (qNumber == currentQuestionNumber.ToString())
                    {
                        sendMessage(player.socket, "Your answer for Question "+ (currentQuestionNumber+1)+ " is received! Waiting for other user to answer...");
                        answers[player.id] = answer;
                        updateAnsweredUserCount(1);
                    }
                }
                else
                {
                    sendMessage(player.socket, "Invalid answer! Your answer should be numeric");
                }
            }
        }
        private void Receive(player player)
        {
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    string message = receiveMessage(player.socket);
                    if (message.Contains(':'))
                    {
                        proccessAnswer(ref player, ref connected, ref message);
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        richTextBoxLogs.AppendText(player.name+" has disconnected\n");
                        disconnectedCount += 1;
                    }
                    player.socket.Close();
                    connected = false;
                    Thread.Sleep(1000);
                    if (updateOrGetIsQuizStarted(1) && disconnectedCount != 2)
                    {
                        foreach (player user in users)
                        {
                            if (user.id != player.id)
                            {
                                sendMessage(user.socket, "The other player is disconnected. You win the game!");
                                users[player.id] = player.zeroPoint();
                                sendMessage(user.socket, getScores());
                                user.socket.Close();
                                users.Remove(user);
                                users.Remove(player);
                                terminateQuiz();
                                break;
                            }
                        }
                    }
                }
            }
        }

        private string getScores()
        {
            string result = "Score Table\n";
            var sortedList = users.OrderByDescending(x => x.points);
            foreach (player user in sortedList)
            {
                result += user.name + " - " + user.points.ToString() + "\n";
            }
            return result;
        }

        private int getWinner()
        {
            if (users[0].points > users[1].points)
            {
                return 0;
            }
            else if (users[1].points > users[0].points)
            {
                return 1;
            }
            else return -1;
        }

        private void sendQuestion(string question, player user)
        {
            if (currentQuestionNumber < numOfQuestions)
            {
                Byte[] buffer = Encoding.Default.GetBytes(currentQuestionNumber.ToString() + ":" + question);
                try
                {
                    richTextBoxLogs.AppendText((currentQuestionNumber + 1).ToString() + ". question sent to " + user.name + "\n");
                    user.socket.Send(buffer);
                }
                catch
                {
                    richTextBoxLogs.AppendText("There is a problem in! Check the connection...\n");
                    terminating = true;
                    textBoxPort.Enabled = true;
                    buttonListen.Enabled = true;
                    serverSocket.Close();
                }
            }
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

        private void updateAnsweredUserCount(int i)
        {
            lock (this)
            {
                answeredUserCount += i;
            }
        }

        private void incrementCurrentQuestionNumber()
        {
            lock (this)
            {
                currentQuestionNumber += 1;
            }
        }

        private bool updateOrGetIsQuizStarted(int a)
        {
            lock (this)
            {
                if (a == 1) //get isQuizStarted
                {
                    return isQuizStarted;
                }
                else if (a == 2) //make isQuizStarted False
                {
                    isQuizStarted = false;
                    return isQuizStarted;
                }
                else if (a == 3) //make isQuizStarted True
                {
                    isQuizStarted = true;
                    return isQuizStarted;
                }
                else
                {
                    return isQuizStarted;
                }
            }
            
        }

        private void Quiz()
        {
            if (updateOrGetIsQuizStarted(1) && currentQuestionNumber < numOfQuestions)
            {
                for (int i = 0; i < numOfQuestions; i++)
                {
                    int userCount = users.Count;
                    for (int k = 0; k < userCount; k++)
                    {
                        sendQuestion(questions[i % questions.Count].questionString, users[k]);
                    }

                    richTextBoxLogs.AppendText("Question - "+ (i+1).ToString() +" "+ questions[i % questions.Count].questionString + "\n\n");

                    while (answeredUserCount != userCount)
                    {
                        ;
                    }

                    if (answeredUserCount == userCount)
                    {
                        for (int k = 0; k < userCount; k++)
                        {
                            sendMessage(users[k].socket, users[0].name + "\'s answer is " + answers[0].ToString() + "\n" +
                                                            users[1].name + "\'s answer is " + answers[1].ToString() + "\n" +
                                                            "The correct answer was " + questions[i % questions.Count].answer.ToString() + "\n");
                            
                        }

                        richTextBoxLogs.AppendText(users[0].name + "\'s answer is " + answers[0].ToString() + "\n" +
                                                            users[1].name + "\'s answer is " + answers[1].ToString() + "\n" +
                                                            "The correct answer was " + questions[i % questions.Count].answer.ToString() + "\n\n");
                        int result = compareResult(answers[0], answers[1], questions[i % questions.Count].answer);
                        if (result == 0)
                        {
                            users[0] = users[0].addPoint(1);
                            sendMessage(users[0].socket, "You win! You get 1 point\n");
                            richTextBoxLogs.AppendText(users[0].name + " get 1 point\n");
                            sendMessage(users[1].socket, "You lose! You get 0 point\n");
                            richTextBoxLogs.AppendText(users[1].name + " get 0 point\n\n");
                        }
                        else if (result == 1)
                        {
                            users[1] = users[1].addPoint(1);
                            sendMessage(users[1].socket, "You win! You get 1 point\n");
                            sendMessage(users[0].socket, "You lose! You get 0 point\n");
                            richTextBoxLogs.AppendText(users[0].name + " get 0 point\n");
                            richTextBoxLogs.AppendText(users[1].name + " get 1 point\n\n");
                        }
                        else
                        {
                            users[0] = users[0].addPoint(0.5);
                            users[1] = users[1].addPoint(0.5);
                            sendMessage(users[1].socket, "Tie! You get 0.5 point\n");
                            sendMessage(users[0].socket, "Tie! You get 0.5 point\n");
                            richTextBoxLogs.AppendText(users[0].name + " get 0.5 point\n");
                            richTextBoxLogs.AppendText(users[1].name + " get 0.5 point\n\n");
                        }

                        for (int k = 0; k < userCount; k++)
                        {
                            sendMessage(users[k].socket, getScores());
                        }
                        richTextBoxLogs.AppendText(getScores() + "\n");
                        incrementCurrentQuestionNumber();
                        updateAnsweredUserCount(-2);
                    }
                }
                bool isFirst = true;
                for (int k = 0; k < users.Count; k++)
                {
                    sendMessage(users[k].socket, "The quiz is over!");
                    sendMessage(users[k].socket, getScores());
                    int winner = getWinner();
                    if(winner == -1)
                    {
                        sendMessage(users[k].socket, "Tie!");
                        if (isFirst)
                        {
                            richTextBoxLogs.AppendText("The quiz is over!\n");
                            richTextBoxLogs.AppendText("The result is tie\n");
                            isFirst = false;
                        }
                    }
                    else if (k == winner)
                    {
                        sendMessage(users[k].socket, "Congratulations! You win!");
                        richTextBoxLogs.AppendText("The quiz is over!\n");
                        richTextBoxLogs.AppendText(users[k].name + " is the winner\n");
                    }
                    else
                    {
                        sendMessage(users[k].socket, "You lose!");
                    }
                    isFirst = false;
                }
                updateOrGetIsQuizStarted(2);
            }
        }
    }
}
