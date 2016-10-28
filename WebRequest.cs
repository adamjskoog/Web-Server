using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace CS422
{
    class WebRequest
    {
        private string _HTTPMethod;
        private string _URI;
        private string _HTTPVersion;
        private ConcurrentDictionary<string, string> _Headers;
        private ConcatStream _RequestBody;
        private NetworkStream _ResponseStream;

        private const string _ResponseTemplate = "{0} {1} {2} \r\n" 
                                            + "Content-Type: text/html\r\n" 
                                            + " Content-Length: {3}" + "\r\n\r\n";

        /*NOTE: The	body	will	often	be	a	ConcatStream	object, since	the	request	object	is	
            created	after	you’ve	seen	received	the	double	line	break	after	the	headers	and	likely	after	you’ve	read	
            some	of	the	body	data	from	the	network	stream.	The	first	byte	of	the	body	stream	must	be	the	first	byte	
            of	the	actual	request	body.
         */
        public WebRequest(string HTTPMethod, string URI, string HTTPVer, ref NetworkStream ns, string data)
        {
            _Headers = new ConcurrentDictionary<string, string>();

            _HTTPMethod = HTTPMethod;
            _URI = URI;
            _HTTPVersion = HTTPVer;
            _ResponseStream = ns;

            /*Check the data of what has been read from the network stream already, as it may have partially read
            part of the body after the double line break. Create a new stream, write the portion of the body to it.
            Then pass it into the Concat constructor with that as the first stream and the network stream as the second*/
            int index = data.IndexOf("\r\n\r\n");
            string body = data.Substring(index);
            MemoryStream bodyStream = new MemoryStream();
            bodyStream.Write(Encoding.ASCII.GetBytes(body), 0, Encoding.ASCII.GetBytes(body).Length - 1);

            string contentLength = null;
            //If "Content-Length" is present in the request then the stream will support quering length
            if(data.IndexOf("Content-Length") != -1)
            {
                string temp = data;
                string[] header = temp.Split(' ');
                for(int i = 0; i < header.Length; i++)
                {
                    if(header[i].Contains("Content-Length"))
                    {
                        contentLength = header[i + 1];
                        break;
                    }
                }


                _RequestBody = new ConcatStream(bodyStream, _ResponseStream, Convert.ToInt64(contentLength));
            }
            else
            {
                _RequestBody = new ConcatStream(bodyStream, _ResponseStream);
            }

        }

        

        public void WriteNotFoundResponse(string pageHTML)
        {
            /*TODO:The	first writes	a response	with	a 404 status	code	and	the	specified	HTML	string	as	the	body	of	
                the	response.	*/
            string reformat = String.Format(_ResponseTemplate, _HTTPVersion, "404", "File Not Found", Convert.ToByte(pageHTML.Length));

            _ResponseStream.Write(Encoding.ASCII.GetBytes(reformat), 0, reformat.Length);
            _ResponseStream.Write(Encoding.ASCII.GetBytes(pageHTML), 0, pageHTML.Length);

        }

        //This writes a response if the correct handler was found
        public bool WriteHTMLResponse(string htmlString)
        {

            string reformat = String.Format(_ResponseTemplate, _HTTPVersion, "200", "OK", Convert.ToByte(htmlString.Length));

            try
            {
                _ResponseStream.Write(Encoding.ASCII.GetBytes(reformat), 0, reformat.Length);
                _ResponseStream.Write(Encoding.ASCII.GetBytes(htmlString), 0, htmlString.Length);
            }
            catch
            {
                return false;
            }
            

            return true;
        }

        public string RequestURI
        {
            get
            {
                return _URI;
            }
        }

        public string HTTPVer
        {
            get
            {
                return _HTTPVersion;
            }
        }

        public string HTTPMethod
        {
            get
            {
                return _HTTPMethod;
            }
        }

    }
}
