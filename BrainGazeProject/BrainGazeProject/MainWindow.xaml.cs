﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using EyeXFramework.Wpf;
using Tobii.EyeX.Framework;
using EyeXFramework;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace BrainGazeProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        //SETUP VARIABLES//
        int initialImg = 0; //0 for brain, 1 for optic tract diagram
        private static string defaultSenderIP = "169.254.50.139"; //169.254.41.115 A, 169.254.50.139 B
        string compID = "B";

        EyeXHost eyeXHost;

        private bool SenderOn = true;
        private bool ReceiverOn = true;
        private static int ReceiverPort = 11000, SenderPort = 11000;//ReceiverPort is the port used by Receiver, SenderPort is the port used by Sender
        private bool communication_started_Receiver = false;//indicates whether the Receiver is ready to receive message(coordinates). Used for thread control
        private bool communication_started_Sender = false;//Indicate whether the program is sending its coordinates to others. Used for thread control
        private System.Threading.Thread communicateThread_Receiver; //Thread for receiver
        private System.Threading.Thread communicateThread_Sender;   //Thread for sender
        private static string SenderIP = "", ReceiverIP = ""; //The IP's for sender and receiver.
        private static string IPpat = @"(\d+)(\.)(\d+)(\.)(\d+)(\.)(\d+)\s+"; // regular expression used for matching ip address
        private Regex r = new Regex(IPpat, RegexOptions.IgnoreCase);//regular expression variable
        private static string NumPat = @"(\d+)\s+";
        private Regex regex_num = new Regex(NumPat, RegexOptions.IgnoreCase);
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;
        private static String sending;
        private static String received;
        int ind_1, ind_2, ind_3, ind_4;

        //log
        string pathfolder = "C:/Users/master/Documents/gamelog/";
        string path;
        string time;
        string datapoint;
        double timediff;
        double t0;
        TimeSpan timerStart;

        //settings
        bool gazeSetting = false;
        bool fixSetting = false;
        bool highlightSetting = false;

        //Fixation vis
        Point fixationTrack = new Point(0, 0);
        Point fastTrack = new Point(0, 0);
        Point otherFixationTrack = new Point(0, 0);
        Point otherFastTrack = new Point(0, 0);
        double fixationStart = 0;
        bool fixStart = true;
        bool fixShift = false;
        double fadeTimer = 0;

        //Double vis
        int shareTime = 0;
        int awayTime = 0;
        bool shareStart = true;
        double shareX, shareY;
        
        //Highlight vis
        int oldID = 0;
        int fixTime = 0;
        Rectangle oldSet = new Rectangle();
        bool highlight = false;

        //Fade vis
        bool fade = false;
        //images
        Rectangle img = null;
        Rectangle rectangle = null;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            if (initialImg == 1)
            {
                Window1 window1 = new Window1();
                window1.Show();
                this.Close();
            }
            else
            {
                setupMainWindow();

            }
        }
        #region setup
        private void setupMainWindow()
        {
            DataContext = this;
            InitializeComponent();
            eyeXHost = new EyeXHost();
            eyeXHost.Start();

            var fixationData = eyeXHost.CreateFixationDataStream(FixationDataMode.Sensitive);
            var gazeData = eyeXHost.CreateGazePointDataStream(GazePointDataMode.LightlyFiltered);
            fixationData.Next += fixTrack;
            gazeData.Next += trackDot;

            if (ReceiverOn)
            {
                IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                Receive_Status_Text.Text = "Receiving Data at\nIP:" + ipAddress.ToString();
                Receive_Status_Text.Visibility = Visibility.Visible;
            }
            if (SenderOn)
            {
                SenderIP = defaultSenderIP;
                Share_Status_Text.Text = "Sharing Data to\nIP:" + SenderIP.ToString();
                Share_Status_Text.Visibility = Visibility.Visible;
                communication_started_Sender = false;
            }
            setupTimer();
            //setupTask();
        }
        private void setupTimer()
        {
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            dispatcherTimer.Tick += new EventHandler(update);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 20);
            dispatcherTimer.Start();
        }
        private void setupTask()
        {
            initLog();
            t0 = DateTime.Now.TimeOfDay.TotalSeconds;
            timerStart = DateTime.Now.TimeOfDay;
        }
        #endregion

        #region buttons
        private void gazeButton(object sender, RoutedEventArgs e)
        {
            gazeSetting = !gazeSetting;
            if (gazeSetting)
            {
                GazeButton.Content = "Turn off Gazepath";
                otrack0.Visibility = Visibility.Visible;
                otrack1.Visibility = Visibility.Visible;
                otrack1.Opacity = 1;
                otrackLine.Visibility = Visibility.Visible;
            }
            else
            {
                GazeButton.Content = "Turn on Gazepath";
                otrack0.Visibility = Visibility.Hidden;
                otrack1.Visibility = Visibility.Hidden;
                otrackLine.Visibility = Visibility.Hidden;
            }
        }
        private void fixButton(object sender, RoutedEventArgs e)
        {
            fixSetting = !fixSetting;
            if (fixSetting)
            {
                FixButton.Content = "Turn off Fixation";
                doubleHighlight.Visibility = Visibility.Visible;
            }
            else
            {
                FixButton.Content = "Turn on Fixation";
                doubleHighlight.Visibility = Visibility.Hidden;
            }
        }
        private void highlightButton(object sender, RoutedEventArgs e)
        {
            highlight = !highlight;
            if (highlight)
            {
                HighlightButton.Content = "Turn off Highlight";
            }
            else {
                HighlightButton.Content = "Turn on Highlight";
            }
        }
        private void fadeButton(object sender, RoutedEventArgs e)
        {
            fade = !fade;
            if (fade)
            {
                otrack1.Visibility = Visibility.Visible;
                FadeButton.Content = "Turn off Fade";
            }
            else
            {
                otrack1.Visibility = Visibility.Hidden;
                FadeButton.Content = "Turn on Fade";
            }
        }
        #endregion

        private void OnHasGazeChanged(object sender, RoutedEventArgs e)
        {
            if (highlightSetting)
            {
                rectangle = e.Source as Rectangle;
                if (null == rectangle) { return; }
                img = FindName((rectangle.Name).Substring(0, (rectangle.Name).Length - 1)) as Rectangle;
                if (rectangle.GetHasGaze())
                {
                    img.Visibility = Visibility.Visible;
                }
                else
                {
                    img.Visibility = Visibility.Hidden;
                }
            }
        }

        void update(object sender, EventArgs e)
        {
            string timetemp = DateTime.Now.TimeOfDay.Subtract(timerStart).ToString();
            Timer.Text = timetemp.Substring(0, timetemp.Length - 8);

            if (fixShift & fixationTrack.X != double.NaN & fixationTrack.Y != double.NaN)
            {
                fixationTrack = PointFromScreen(fixationTrack);
                //Canvas.SetLeft(track1, Canvas.GetLeft(track0));
                //Canvas.SetTop(track1, Canvas.GetTop(track0));
                Canvas.SetLeft(track0, fixationTrack.X);
                Canvas.SetTop(track0, fixationTrack.Y);
                trackLine.X1 = fixationTrack.X + 5;
                trackLine.Y1 = fixationTrack.Y + 5;
                fadeTimer = 150;
                //trackLine.X2 = Canvas.GetLeft(track1) + 10;
                //trackLine.Y2 = Canvas.GetTop(track1) + 10;
                fixShift = false;
            }
            fastTrack = PointFromScreen(fastTrack);
            track0.Opacity = fadeTimer / 150;
            trackLine.Opacity = fadeTimer / 150;
            fadeTimer--;
            double left = Canvas.GetLeft(track1);
            double top = Canvas.GetTop(track1);
            Canvas.SetLeft(track1, (fastTrack.X - left) / 1.3 + left);
            Canvas.SetTop(track1, (fastTrack.Y - top) / 1.3 + top);
            trackLine.X2 = Canvas.GetLeft(track1) + 5;
            trackLine.Y2 = Canvas.GetTop(track1) + 5;

            sending = ((int)fastTrack.X).ToString() + "|" + ((int)fastTrack.Y).ToString() + ":" + ((int)fixationTrack.X).ToString() + "!" + ((int)fixationTrack.Y).ToString() + "(" + ((int)(100 * track0.Opacity)).ToString();
            //received = sending;
            //If user pressed Receiver or Cursor button but communication haven't started yet or has terminated, start a thread on tryCommunicateReceiver()
            if (ReceiverOn && communication_started_Receiver == false)
            {
                communication_started_Receiver = true;
                communicateThread_Receiver = new System.Threading.Thread(new ThreadStart(() => tryCommunicateReceiver(sending)));
                communicateThread_Receiver.Start();
            }

            //If user pressed Sender button but communication haven't started yet or has terminated, start a thread on tryCommunicateSender()
            if (SenderOn && communication_started_Sender == false)
            {
                communication_started_Sender = true;
                communicateThread_Sender = new System.Threading.Thread(new ThreadStart(() => tryCommunicateSender(sending)));
                communicateThread_Sender.Start();
            }
            if (received != null)
            {
                test.Text = received.ToString();
                ind_1 = received.IndexOf("|");
                ind_2 = received.IndexOf(":");
                ind_3 = received.IndexOf("!");
                ind_4 = received.IndexOf("(");
                int p1, p2, p3, p4;
                double p5;
                if (Int32.TryParse(received.Substring(0, ind_1), out p1))
                {
                    otherFastTrack.X = p1;
                }
                if (Int32.TryParse(received.Substring(ind_1 + 1, ind_2 - ind_1 - 1), out p2))
                {
                    otherFastTrack.Y = p2;
                }
                if (Int32.TryParse(received.Substring(ind_2 + 1, ind_3 - ind_2 - 1), out p3))
                {
                    otherFixationTrack.X = p3;
                }
                if (Int32.TryParse(received.Substring(ind_3 + 1, ind_4 - ind_3 - 1), out p4))
                {
                    otherFixationTrack.Y = p4;
                }
                if (Double.TryParse(received.Substring(ind_4 + 1, received.Length - ind_4 - 1), out p5))
                {
                    otrack0.Opacity = p5 / 100;
                }
                //otherFastTrack.X = Convert.ToInt32(received.Substring(0, ind_1));
                //otherFastTrack.Y = Convert.ToInt32(received.Substring(ind_1 + 1, ind_2 - ind_1 - 1));
                //otherFixationTrack.X = Convert.ToInt32(received.Substring(ind_2 + 1, ind_3 - ind_2 - 1));
                //otherFixationTrack.Y = Convert.ToInt32(received.Substring(ind_3 + 1, ind_4 - ind_3 - 1));
                //otrack0.Opacity = Convert.ToDouble(received.Substring(ind_4 + 1, received.Length - ind_4 - 1)) / 100;
                otrackLine.Opacity = otrack0.Opacity;

                //otherFixationTrack = PointFromScreen(otherFixationTrack);
                Canvas.SetLeft(otrack0, otherFixationTrack.X);
                Canvas.SetTop(otrack0, otherFixationTrack.Y);
                otrackLine.X1 = otherFixationTrack.X + 5;
                otrackLine.Y1 = otherFixationTrack.Y + 5;

                //otherFastTrack = PointFromScreen(otherFastTrack);
                Canvas.SetLeft(otrack1, otherFastTrack.X);
                Canvas.SetTop(otrack1, otherFastTrack.Y);
                otrackLine.X2 = Canvas.GetLeft(otrack1) + 5;
                otrackLine.Y2 = Canvas.GetTop(otrack1) + 5;
            }

            doubleTrack();
            brainHighlight();
            fadeTrack();
        }

        private void brainHighlight() {
            Rectangle box;
            double offset;
            double X1, X2, Y1, Y2;
            double leneX;
            double minLeft = 1000;
            int leftID = -1;

            foreach (UIElement child in canv.Children) {
                if (child.GetType().Equals(b0.GetType()) && (box = child as Rectangle).Name.Substring(0, 1).Equals("x"))
                {
                    Y1 = Canvas.GetTop(box) + box.Height;
                    Y2 = Canvas.GetTop(box);

                    if (fastTrack.Y > Y2 && fastTrack.Y < Y1)
                    {
                        offset = (box.Name.Substring(1, 1).Equals("L")) ? box.Width : 0;
                        X1 = Canvas.GetLeft(box) + offset;
                        X1 = Canvas.GetLeft(box) + offset;
                        X2 = Canvas.GetLeft(box) + box.Width - offset;
                        leneX = (X1 - X2) / (Y1 - Y2) * (fastTrack.Y - Y2) + X2;

                        if (leneX < fastTrack.X && fastTrack.X - leneX < minLeft)
                        {
                            minLeft = fastTrack.X - leneX;
                            leftID = Convert.ToInt32(box.Name.Substring(2, box.Name.IndexOf("O") - 2));
                        }
                    }
                }
            }
            if (leftID == oldID)
            {
                fixTime++;
            }
            else {
                fixTime = 0;
            }
            if (leftID >= 0 && fixTime > 2)
            {
                if (highlight)
                {
                    Rectangle temp = canv.FindName("b" + leftID.ToString()) as Rectangle;
                    oldSet.Visibility = Visibility.Hidden;
                    temp.Visibility = Visibility.Visible;
                    oldSet = temp;
                }
                else {
                    oldSet.Visibility = Visibility.Hidden;
                    b0.Visibility = Visibility.Visible;
                }
            }

            oldID = leftID;
        }

        private void fadeTrack() {
            if (fade) {
                otrack1.Opacity =otrack1.Opacity*.75 +(1 - distance(fastTrack, otherFastTrack) / 300)*.25;
            }
        }

        private double distance(Point p1, Point p2) {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        #region logData
        private void initLog()
        {
            path = pathfolder + compID + "_" + DateTime.Now.ToString("MM-dd_hh-mm") + "_Img1.txt";
            time = DateTime.Now.ToString("hh:mm:ss.ff");
            datapoint = "Starting @ " + time + "\n";
            System.IO.StreamWriter file = new System.IO.StreamWriter(path, true);
            file.WriteLine(datapoint);
            file.Close();
        }
        private void logTime(double currTime, double prevTime)
        {
            time = DateTime.Now.ToString("hh:mm:ss.fff");
            timediff = (currTime - prevTime);
            System.IO.StreamWriter file = new System.IO.StreamWriter(path, true);
            //file.WriteLine(datapoint);
            file.Close();
        }
        #endregion

        #region gazetracking

        private void fixTrack(object s, EyeXFramework.FixationEventArgs e)
        {
            if (e.EventType == FixationDataEventType.Begin)
            {
                fixationStart = e.Timestamp;
            }
            double fixationtime = e.Timestamp - fixationStart;
            if (fixationtime > 700 & fixStart)
            {
                fixationTrack = new Point(e.X, e.Y);
                fixShift = true;
                fixStart = false;
            }
            if (e.EventType == FixationDataEventType.End)
            {
                fixStart = true;
            }
        }
        private void trackDot(object s, EyeXFramework.GazePointEventArgs e)
        {
            fastTrack = new Point(e.X, e.Y);
        }
        private void doubleTrack()
        {
            double distance = Math.Sqrt(Math.Pow(fastTrack.X - otherFastTrack.X, 2) + Math.Pow(fastTrack.Y - otherFastTrack.Y, 2));
            if (distance < 125)
            {
                shareX = (.7 * shareX + .3 * ((fastTrack.X + otherFastTrack.X) / 2));
                shareY = (.7 * shareY + .3 * ((fastTrack.Y + otherFastTrack.Y) / 2));
                shareTime++;
                awayTime = 0;

                doubleHighlight.Width = -75 / (1 + Math.Pow(Math.E, shareTime * .5 - 10)) + 75;
                doubleHighlight.Height = doubleHighlight.Width;
                doubleHighlight.StrokeThickness = doubleHighlight.Width / 15;
                Canvas.SetLeft(doubleHighlight, shareX - doubleHighlight.Width / 2);
                Canvas.SetTop(doubleHighlight, shareY - doubleHighlight.Height / 2);
                if (shareStart)
                {
                    shareX = (fastTrack.X + otherFastTrack.X) / 2;
                    shareY = (fastTrack.Y + otherFastTrack.Y) / 2;
                    Canvas.SetLeft(doubleHighlight, shareX - doubleHighlight.Width / 2);
                    Canvas.SetTop(doubleHighlight, shareY - doubleHighlight.Height / 2);
                    shareStart = false;
                }
            }
            else
            {
                awayTime++;
                if (awayTime > 10)
                {
                    doubleHighlight.Width = 0;
                    doubleHighlight.Height = 0;
                    doubleHighlight.StrokeThickness = 0;
                    shareTime = 0;
                }
                shareStart = true;
            }
        }
        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            communication_started_Receiver = false;
            communication_started_Sender = false;
            if (initialImg == 0)
            {
                dispatcherTimer.Stop();
                eyeXHost.Dispose();
            }
            try
            {
                communicateThread_Receiver.Abort();
                communicateThread_Sender.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            base.OnClosing(e);
        }

        #region Sender/Receiver Methods
        public void tryCommunicateReceiver(String x)
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            ReceiverIP = ipHostInfo.AddressList[0].ToString();

            while (ReceiverIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            AsynchronousSocketListener.StartListening();
        }
        public void tryCommunicateSender(String x)
        {
            while (SenderIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            SynchronousClient.StartClient(x); //start sending info
            communication_started_Sender = false;

            //AsynchronousSocketListener.StartListening();
        }
        public class StateObject
        {
            // Client  socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 1024;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
        }
        //THis is the Receiver function (Asyncronous)
        // Citation: https://msdn.microsoft.com/en-us/library/fx6588te%28v=vs.110%29.aspx
        public class AsynchronousSocketListener
        {
            // Thread signal.
            public static ManualResetEvent allDone = new ManualResetEvent(false);
            public AsynchronousSocketListener()
            {
            }
            public static void StartListening()
            {
                if (ReceiverIP != "")
                {
                    // Data buffer for incoming data.
                    byte[] bytes = new Byte[1024];

                    // Establish the local endpoint for the socket.
                    // The DNS name of the computer
                    IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(ReceiverIP);
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, ReceiverPort);

                    // Create a TCP/IP socket.
                    Socket listener = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Bind the socket to the local endpoint and listen for incoming connections.
                    try
                    {
                        listener.Bind(localEndPoint);
                        listener.Listen(100);
                        //ommunication_received==false
                        while (true)
                        {
                            // Set the event to nonsignaled state.
                            allDone.Reset();

                            // Start an asynchronous socket to listen for connections.
                            //Console.WriteLine("Waiting for a connection...");
                            listener.BeginAccept(
                                new AsyncCallback(AcceptCallback),
                                listener);

                            allDone.WaitOne();

                            // Wait until a connection is made before continuing.
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                    //Console.WriteLine("\nPress ENTER to continue...");
                    //Console.Read();
                }
            }
            public static void AcceptCallback(IAsyncResult ar)
            {
                // Signal the main thread to continue.
                allDone.Set();

                // Get the socket that handles the client request.
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
            }
            public static void ReadCallback(IAsyncResult ar)
            {
                String content = String.Empty;

                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(
                        state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not there, read more data.
                    content = state.sb.ToString();
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        // All the data has been read from the client. Display it on the console.
                        int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int y_start_ind = content.IndexOf("y: "), y_end_ind = content.IndexOf("yend ");

                        if (x_start_ind > -1 && x_end_ind > -1)
                        {
                            try
                            {
                                //convert the received string into x and y                                
                                // x_received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                // y_received = Convert.ToInt32(content.Substring(y_start_ind + 3, y_end_ind - (y_start_ind + 3)));
                                string s = content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3));
                                //received_cards_arr = s.Split(',').Select(str => int.Parse(str)).ToArray(); ;
                                // received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                received = s;
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("Input string is not a sequence of digits.");
                            }
                            catch (OverflowException)
                            {
                                Console.WriteLine("The number cannot fit in an Int32.");
                            }
                        }
                        // Show the data on the console.
                        //Console.WriteLine("x : {0}  y: {1}", x_received, y_received);

                        // Echo the data back to the client.
                        Send(handler, content);
                    }
                    else
                    {
                        // Not all data received. Get more.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }

            private static void Send(Socket handler, String data)
            {
                // Convert the string data to byte data using ASCII encoding.
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device.
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            }

            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object.
                    Socket handler = (Socket)ar.AsyncState;

                    // Complete sending the data to the remote device.
                    int bytesSent = handler.EndSend(ar);
                    //Console.WriteLine("Sent {0} bytes to client.", bytesSent);x

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        //This is the sending function (Syncronous)
        public class SynchronousClient
        {

            public static void StartClient(String x)
            {
                // Data buffer for incoming data.
                byte[] bytes = new byte[1024];

                // Connect to a remote device.
                try
                {
                    // Establish the remote endpoint for the socket.
                    // This example uses port 11000 on the local computer.
                    IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(SenderIP);
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, SenderPort);

                    // Create a TCP/IP  socket.
                    Socket sender = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Connect the socket to the remote endpoint. Catch any errors.
                    try
                    {
                        sender.Connect(remoteEP);

                        Console.WriteLine("Socket connected to {0}",
                            sender.RemoteEndPoint.ToString());
                        //
                        string array_to_string = string.Join(",", x);
                        string message_being_sent = "x: " + x + "xend <EOF>";
                        //string message_being_sent = "x: " + x + "xend y: " + y + "yend cursorx: " +
                        //    System.Windows.Forms.Cursor.Position.X + "cursorxend cursory: " +
                        //    System.Windows.Forms.Cursor.Position.Y + "cursoryend <EOF>";
                        // Encode the data string into a byte array.
                        byte[] msg = Encoding.ASCII.GetBytes(message_being_sent);

                        // Send the data through the socket.
                        int bytesSent = sender.Send(msg);

                        // Receive the response from the remote device.
                        int bytesRec = sender.Receive(bytes);
                        Console.WriteLine("Echoed test = {0}",
                            Encoding.ASCII.GetString(bytes, 0, bytesRec));

                        // Release the socket.
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Close();

                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("SocketException : {0}", se.ToString());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected exception : {0}", e.ToString());
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            public static string data = null;


        }
        #endregion
    }
}
