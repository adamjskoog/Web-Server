using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace CS422
{
    class WebServer
    {
        private static string _ResponseURI;
        private static BlockingCollection<Thread> _ThreadPool = new BlockingCollection<Thread>();
        private static ConcurrentQueue<Client> _Clients = new ConcurrentQueue<Client>();
        private static ConcurrentBag<WebService> _Services = new ConcurrentBag<WebService>();
        private static Thread _ListenerThread;
        private static int _ActiveThreads;
        private static int _Port;
        private const int _FirstCRLFLimit = 2048;
        private const int _DoubleLineBreakLimit = 102400;

        private class Client: IDisposable
        {
            // Flag: Has Dispose already been called?
            bool disposed = false;
            private TcpClient _Client = null;

            public Client(TcpClient tcp)
            {
                _Client = tcp;
            }

            public TcpClient GetClient
            {
                get { return _Client; }

            }

            // Public implementation of Dispose pattern callable by consumers.
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            // Protected implementation of Dispose pattern.
            protected virtual void Dispose(bool disposing)
            {
                if (disposed)
                    return;

                if (disposing)
                {
                    _Client.Close();
                }
                disposed = true;
            }

            ~Client()
            {
                Dispose(false);
            }
        }

        public static int ThreadCount
        {
            get
            {
                return _ActiveThreads;
            }
        }

        public static bool Start(int port, int threadCount)
        {
            //Reject reserved ports
            if(port <= 80)
            {
                return false;
            }

            //check if threadcount is <0, if so default to 64
            if (threadCount <= 0)
            {
                for (int i = 0; i < 64; i++)
                {
                    
                    Thread th = new Thread(delegate () { ThreadWork(); });

                    _ThreadPool.Add(th);
                }

            }
            else
            {
                for (int i = 0; i < threadCount; i++)
                {
                    
                    Thread th = new Thread(delegate () { ThreadWork(); });

                    _ThreadPool.Add(th);
                }
            }

            _Port = port;
            Thread listen = new Thread(delegate () { Listen(); });
            _ListenerThread = listen;
            _ListenerThread.Start();

            return true;

           
        }

        //Main listener thread
        private static void Listen()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _Port);
            listener.Start();

            while (true)
            {
                Console.Write("Waiting for a connection... ");

                //Server blocking call
                var client = listener.AcceptTcpClient();
                Console.WriteLine("Connected! ");
                Client wrappedClient = new Client(client);

                _Clients.Enqueue(wrappedClient);
                _ThreadPool.Take().Start();
                Interlocked.Increment(ref _ActiveThreads);


            }
        }

        public static void Stop()
        {
            if(_ActiveThreads == 0)
            {

            }
            else
            {
                
                while (_ActiveThreads != 0)
                {
                    //Blocking call, waiting for all threads to finish
                }

            }

            _ListenerThread.Abort();
        }


        private static void ThreadWork()
        {
            Client client = null;

            //Try to dequeue a client to perform work
            _Clients.TryDequeue(out client);

            //Build a web request
            WebRequest wr = null;
            //Check for malicious request greater than 10 seconds
            Thread thr = new Thread( () => { wr = BuildRequest(client.GetClient); });
            try
            {
                thr.Start();
                if (!thr.Join(TimeSpan.FromSeconds(10)))
                {
                    TerminateSocketConnection(client);
                    return;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
           


            
            //If it is not a valid request then close the client connection and return the thread to wait
            if (wr == null)
            {
                TerminateSocketConnection(client);
            }
            else
            {
                //Need a boolean flag to see if a service was found for the web request
                bool foundService = false;

                /*We will use a simple definition of how to know whether or not a WebService object can handle
                a request: if the request-target/URI starts with the string specified by the WebService object’s
                ServiceURI parameter, then it can process that request.*/
                foreach (WebService ws in _Services)
                {
                    if (wr.RequestURI.StartsWith(ws.ServiceURI))
                    {
                        ws.Handler(wr);
                        foundService = true;
                    }
                }

                //If no service was found, then return a 404 response
                if (!foundService)
                {
                    wr.WriteHTMLResponse("<html>404: Page not found </html>");
                }

                TerminateSocketConnection(client);

            }


        }

        private static void TerminateSocketConnection(Client client)
        {

            client.Dispose();
            Thread th = new Thread(delegate () { ThreadWork(); });
            _ThreadPool.Add(th);
            Interlocked.Decrement(ref _ActiveThreads);

        }


        //Add a service to a thread safe bag on the server
        public static void AddService(WebService service)
        {
            _Services.Add(service);
        }

        private static WebRequest BuildRequest(TcpClient client)
        {
            NetworkStream _Ns = client.GetStream(); 

            //Buffer for reading
            byte[] buf = new byte[4096];

            //i = number of bytes read in
            int i = -1, totalBytesRead = 0;
            string data = null;

            /*
            *   Boolean array to see if certain checks have passed, by default all are set to false
            *   flags[0] = "GET " check
            *   flags[1] = Proper HTTP version check
            *   flags[2] = 
            */
            bool[] flags = new bool[5];

            _Ns.ReadTimeout = 1500;// Implementing this line crashes me all the time

            

            //Read through all the data sent by the client
            while(i != 0)
            {
                //Check for malicious clients by not letting a read call go longer than 2 seconds
                try
                {
                    i = _Ns.Read(buf, 0, buf.Length);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }

                totalBytesRead += i;

                //Translate data bytes to an ASCII string
                data += Encoding.ASCII.GetString(buf, 0, i);


                //Request Size limits
                if (totalBytesRead > _FirstCRLFLimit && !data.Contains("\r\n"))
                {
                    return null;
                }



                //Process the data that was just received
                if (!flags[0])
                {
                    //First case is to see if the first 4 bytes are "GET "
                    if (data.Length >= 4 && !data.StartsWith("GET "))
                    {
                        //if we have entered here, then not a proper function call
                        //close the networkstream and return false
                        _Ns.Close();
                        return null;
                    }

                    //Valid HTTP method check
                    if (data.Length >= 4 && data.StartsWith("GET "))
                        flags[0] = true;
                }


                //HTTP version check
                if (!flags[1])
                {
                    string temp = data;
                    string[] data_arr = temp.Split(' ');

                    //Only accept HTTP/1.1
                    if (data_arr.Length >= 3 && data_arr[2].Contains("HTTP"))
                    {
                        /*  Test for the edge case in that the network stream sent
                        *   part of the HTTP but not the full HTTP string, by default
                        *   the string must have 8 characters and they must be HTTP/1.1
                        */
                        if (data_arr[2].Length >= 8 && !(data_arr[2].Substring(0, 8) == "HTTP/1.1"))
                        {
                            _Ns.Close();
                            return null;
                        }

                        if (data_arr[2].Length >= 8 && data_arr[2].Substring(0, 8) == "HTTP/1.1")
                            flags[1] = true;

                    }

                }

                //This check is to see if we have encountered the character combination '\r\n\r\n'
                if (flags[0] && flags[1])
                {
                    if (data.IndexOf("\r\n\r\n") != -1)
                    {
                        flags[2] = true;
                    }
                }

                //If we have passed both GET and HTTP Tests
                if (flags[0] && flags[1] && flags[2])
                {
                    //Parse out the URI to return in the response
                    string temp = data;
                    int index = temp.IndexOf(' ');
                    string augmentedStr = temp.Substring(index + 1);
                    index = augmentedStr.IndexOf(' ');
                    string responseURI = augmentedStr.Substring(0, index);

                    return new WebRequest("GET", responseURI, "HTTP/1.1", ref _Ns, data);
                }

                //If	you	have	read	(100	*	1024)	bytes	or	more	and	have	not	received	the	double	line	
                //break,	then terminate   the request
                if (totalBytesRead > _DoubleLineBreakLimit)
                {
                    return null;

                }




            }

            return null;
            
        }
        
    }
}