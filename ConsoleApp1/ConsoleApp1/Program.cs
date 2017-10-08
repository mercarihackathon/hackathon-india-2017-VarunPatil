using System;
using System.Text;

namespace TCPServer
{
    class Program
    {
        static void Main(string[] args)
        {
            SimpleHTTPServer HTTPServ = new SimpleHTTPServer(".", 3801);

            /* File name for received picture */
            const String FILE_NAME = "Received.jpg";

            /* Port for incoming connections */
            const int PORT = 3800;

            /* The IPEndPoint for the server. IP cannot be localhost */
            System.Net.IPEndPoint remoteIpEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, PORT);
            Console.WriteLine("Listening for connections on port " + PORT.ToString());

            /* After this amount of time has passed, any connection will be terminated
             * Keep high for high latency networks and vice versa */
            const int TIMEOUT = 1000;

            /* Start listening for connections */
            System.Net.Sockets.TcpListener tcpListener = new System.Net.Sockets.TcpListener(remoteIpEndPoint);
            tcpListener.Start();

            /* Create the listening thread */
            Console.WriteLine("Creating the Child Thread");
            System.Threading.Thread connectThread = new System.Threading.Thread(() => StartConnect(tcpListener, TIMEOUT, FILE_NAME));
            connectThread.Start();

            /* Terminate on keypress */
            Console.ReadKey();
            connectThread.Abort();

            /* Clean up and open the received file */
            tcpListener.Stop();
            System.Diagnostics.Process.Start(FILE_NAME);
        }

        public static void StartConnect(System.Net.Sockets.TcpListener tcpListener, int TIMEOUT, String FILE_NAME)
        {
            try
            {
                while (true)
                {
                    /* Create a buffer for receiving */
                    byte[] receiveBytes = new byte[1024];

                    /* The socket that will be used for listening */
                    System.Net.Sockets.Socket sock = null;

                    /* Number and total number of bytes read till the end of loop */
                    int bytesRead = 0;
                    int totalBytesRead = 0;

                    /* Loop till something is read */
                    while (totalBytesRead == 0)
                    {
                        /* Sleep for 100ms if no connection is being made */
                        while (!tcpListener.Pending()) System.Threading.Thread.Sleep(100);

                        sock = tcpListener.AcceptSocket();
                        Console.WriteLine("A");
                        sock.ReceiveTimeout = TIMEOUT;

                        /* Sleep for another 100ms to give the client time to respond */
                        System.Threading.Thread.Sleep(100);
                        int filesize = 0;
                        try
                        {
                            /* Receive the header, terminate if not found */
                            if ((bytesRead = sock.Receive(receiveBytes)) > 0)
                            {
                                string[] headers = System.Text.Encoding.ASCII.GetString(receiveBytes).Split('\n');
                                if (headers[0] == "HEADER")
                                {
                                    Int32.TryParse(headers[1], out filesize);
                                    Console.WriteLine(headers[2].Substring(0,filesize));
                                }
                                else throw new Exception("No header received");
                            }
                            else throw new Exception("No header received");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        /* Close everything */
                        sock.Close();
                        Console.WriteLine("C");
                    }
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                Console.WriteLine("Thread Abort");
            }
        }
    }
}