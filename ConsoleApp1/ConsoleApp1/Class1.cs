// HTTP Server Code --
// MIT License - Copyright (c) 2016 Can Güney Aksakalli
// https://aksakalli.github.io/2014/02/24/simple-http-server-with-csparp.html

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Security.Principal;

class SimpleHTTPServer
{
    public Dictionary<Int32, Tuple<double, double>> LocationDict;
    public Dictionary<Int32, bool> UnusedDict;
    public Dictionary<Int32, string> LastUser;
    public Dictionary<Int32, DateTime> LastUpdate;

    private readonly string[] _indexFiles = {
        "index.html",
        "index.htm",
        "default.html",
        "default.htm"
    };

    private static IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
        #region extension to MIME type list
        {".asf", "video/x-ms-asf"},
        {".asx", "video/x-ms-asf"},
        {".avi", "video/x-msvideo"},
        {".bin", "application/octet-stream"},
        {".cco", "application/x-cocoa"},
        {".crt", "application/x-x509-ca-cert"},
        {".css", "text/css"},
        {".deb", "application/octet-stream"},
        {".der", "application/x-x509-ca-cert"},
        {".dll", "application/octet-stream"},
        {".dmg", "application/octet-stream"},
        {".ear", "application/java-archive"},
        {".eot", "application/octet-stream"},
        {".exe", "application/octet-stream"},
        {".flv", "video/x-flv"},
        {".gif", "image/gif"},
        {".hqx", "application/mac-binhex40"},
        {".htc", "text/x-component"},
        {".htm", "text/html"},
        {".html", "text/html"},
        {".ico", "image/x-icon"},
        {".img", "application/octet-stream"},
        {".iso", "application/octet-stream"},
        {".jar", "application/java-archive"},
        {".jardiff", "application/x-java-archive-diff"},
        {".jng", "image/x-jng"},
        {".jnlp", "application/x-java-jnlp-file"},
        {".jpeg", "image/jpeg"},
        {".jpg", "image/jpeg"},
        {".js", "application/x-javascript"},
        {".mml", "text/mathml"},
        {".mng", "video/x-mng"},
        {".mov", "video/quicktime"},
        {".mp3", "audio/mpeg"},
        {".mpeg", "video/mpeg"},
        {".mpg", "video/mpeg"},
        {".msi", "application/octet-stream"},
        {".msm", "application/octet-stream"},
        {".msp", "application/octet-stream"},
        {".pdb", "application/x-pilot"},
        {".pdf", "application/pdf"},
        {".pem", "application/x-x509-ca-cert"},
        {".pl", "application/x-perl"},
        {".pm", "application/x-perl"},
        {".png", "image/png"},
        {".prc", "application/x-pilot"},
        {".ra", "audio/x-realaudio"},
        {".rar", "application/x-rar-compressed"},
        {".rpm", "application/x-redhat-package-manager"},
        {".rss", "text/xml"},
        {".run", "application/x-makeself"},
        {".sea", "application/x-sea"},
        {".shtml", "text/html"},
        {".sit", "application/x-stuffit"},
        {".swf", "application/x-shockwave-flash"},
        {".tcl", "application/x-tcl"},
        {".tk", "application/x-tcl"},
        {".txt", "text/plain"},
        {".war", "application/java-archive"},
        {".wbmp", "image/vnd.wap.wbmp"},
        {".wmv", "video/x-ms-wmv"},
        {".xml", "text/xml"},
        {".xpi", "application/x-xpinstall"},
        {".zip", "application/zip"},
        #endregion
    };
    private Thread _serverThread;
    private string _rootDirectory;
    private HttpListener _listener;
    private int _port;

    public int Port
    {
        get { return _port; }
        private set { }
    }

    private void Update_Location(int id, double lat, double lng, bool unused)
    {
        LocationDict[id]= new Tuple<double, double>(lat, lng);
        UnusedDict[id] = unused;
        if (!LastUser.ContainsKey(id)) LastUser[id] = String.Empty;
        LastUpdate[id] = DateTime.UtcNow;
    }

    private string GetNearbyVehicles(double lat, double lng, bool admin)
    {
        //Use Gmaps API to show only relevant things
        string tosend = "{";
        bool skippedfirst = false;        
        foreach (KeyValuePair<Int32, Tuple<double, double>> entry in LocationDict)
        {
            string state = "0";
            if (!UnusedDict[entry.Key]) { if (!admin) continue; state = "1"; }
            if (DateTime.UtcNow.Subtract(LastUpdate[entry.Key]).TotalSeconds >= 4 && entry.Key > 3)
            { if (!admin) continue; state = "2"; }

            if (!skippedfirst) skippedfirst = true;
            else tosend += ",";
            tosend += "\"" + entry.Key.ToString() + "\":\"" + entry.Value.Item1.ToString() + "," + entry.Value.Item2.ToString() + "," + state + "\"";
        }
        tosend += "}";
        return tosend;
    }

    /// <summary>
    /// Construct server with given port.
    /// </summary>
    /// <param name="path">Directory path to serve.</param>
    /// <param name="port">Port of the server.</param>
    public SimpleHTTPServer(string path, int port)
    {
        this.Initialize(path, port);
        LocationDict = new Dictionary<int, Tuple<double, double>> ();
        UnusedDict = new Dictionary<int, bool> ();
        LastUser = new Dictionary<int, string>();
        LastUpdate = new Dictionary<int, DateTime> ();
        //Update_Location(1, 19.103684, 72.871368, true);
        //Update_Location(2, 19.103884, 72.871668, true);
        //Update_Location(3, 19.103284, 72.871468, true);
    }

    /// <summary>
    /// Construct server with suitable port.
    /// </summary>
    /// <param name="path">Directory path to serve.</param>
    public SimpleHTTPServer(string path)
    {
        //get an empty port
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        this.Initialize(path, port);
    }

    /// <summary>
    /// Stop server and dispose all functions.
    /// </summary>
    public void Stop()
    {
        _serverThread.Abort();
        _listener.Stop();
    }

    private void Listen()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:" + _port.ToString() + "/");
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            _listener.Prefixes.Add("http://*:" + _port.ToString() + "/");
        
        _listener.Start();
        while (true)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                Process(context);
            }
            catch (Exception ex)
            {

            }
        }
    }

    private void Process(HttpListenerContext context)
    {
        string filename = context.Request.Url.AbsolutePath;
        Console.WriteLine(filename);
        filename = filename.Substring(1);

        if (string.IsNullOrEmpty(filename))
        {
            foreach (string indexFile in _indexFiles)
            {
                if (File.Exists(Path.Combine(_rootDirectory, indexFile)))
                {
                    filename = indexFile;
                    break;
                }
            }
        }

        filename = Path.Combine(_rootDirectory, filename);

        string URL = context.Request.Url.AbsolutePath;
        string[] URLPath = URL.Split("/".ToCharArray()[0]);
        System.Collections.Specialized.NameValueCollection Queries = context.Request.QueryString;

        if (URLPath[1] == "getdata")
        {
            double lat;
            double lng;
            if (!Double.TryParse(Queries["lat"], out lat) || !Double.TryParse(Queries["lng"], out lng)) return;

            byte[] buffer = new byte[1024 * 16];
            string tosend = GetNearbyVehicles(lat, lng, URLPath[2]=="admin");

            int nbytes = tosend.Length;
            buffer = Encoding.UTF8.GetBytes(tosend);
            context.Response.OutputStream.Write(buffer, 0, nbytes);
        }
        else if (URLPath[1] == "startuse")
        {
            int id;
            if (!Int32.TryParse(Queries["id"], out id)) return;
            UnusedDict[id] = false;
            LastUser[id] = Queries["user"];
            Console.WriteLine("startuse : " + id.ToString());
        }
        else if (URLPath[1] == "enduse")
        {
            int id;
            if (!Int32.TryParse(Queries["id"], out id)) return;
            UnusedDict[id] = true;
            if (LastUser[id] != Queries["user"]) Console.WriteLine("SOMETHING WICKED HAPPENED WITH " + Queries["user"]);
            LastUser[id] = Queries["user"];
            Console.WriteLine("startuse : " + id.ToString());
        }
        else if (URLPath[1] == "updateloc")
        {
            double lat;
            double lng;
            int id;
            int unused;
            if (!Int32.TryParse(Queries["id"], out id) || !Int32.TryParse(Queries["unused"], out unused)) return;
            if (!Double.TryParse(Queries["lat"], out lat) || !Double.TryParse(Queries["lng"], out lng)) return;
            Update_Location(id, lat, lng, unused==1);
        }
        else if (File.Exists(filename))
        {
            try
            {
                Stream input = new FileStream(filename, FileMode.Open);

                //Adding permanent http response headers
                string mime;
                context.Response.ContentType = _mimeTypeMappings.TryGetValue(Path.GetExtension(filename), out mime) ? mime : "application/octet-stream";
                context.Response.ContentLength64 = input.Length;
                context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                context.Response.AddHeader("Last-Modified", System.IO.File.GetLastWriteTime(filename).ToString("r"));

                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                    context.Response.OutputStream.Write(buffer, 0, nbytes);
                input.Close();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Flush();
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }

        context.Response.OutputStream.Close();
    }

    private void Initialize(string path, int port)
    {
        this._rootDirectory = path;
        this._port = port;
        _serverThread = new Thread(this.Listen);
        _serverThread.Start();
    }


}