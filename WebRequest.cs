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
                                            + "Content-Type: {3} \r\n" 
                                            + " Content-Length: {4}" + "\r\n\r\n";
        private const string _PartialResponseTemplate = "{0} {1} {2} \r\n"
                                            + "Content-Range: {3} \r\n"
                                            + " Content-Length: {4} \r\n"
                                            +  "Content-Type: {5} \r\n"+ "\r\n\r\n";

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
            string body = data.Substring(index + "\r\n\r\n".Length);
            MemoryStream bodyStream = new MemoryStream();
            if(body.Length > 0)
                bodyStream.Write(Encoding.ASCII.GetBytes(body), 0, Encoding.ASCII.GetBytes(body).Length - 1);

            string contentLength = null;
            //If "Content-Length" is present in the request then the stream will support querying length
            string temp = data;
            string[] header = temp.Split(' ');
            for(int i = 0; i < header.Length; i++)
            {
                if(header[i].Contains("Content-Length"))
                {
                    contentLength = header[i + 1];
                }

                if(header[i].Contains(":"))
                {
                    _Headers.TryAdd(header[i], header[i + 1]);
                }
            }

            if (!string.IsNullOrEmpty(contentLength))
            {
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
            string reformat = String.Format(_ResponseTemplate, _HTTPVersion, "404", "File Not Found", "text/html", pageHTML.Length);

            _ResponseStream.Write(Encoding.ASCII.GetBytes(reformat), 0, reformat.Length);
            _ResponseStream.Write(Encoding.ASCII.GetBytes(pageHTML), 0, pageHTML.Length);

        }

        public bool WriteFileResponse(Stream fs)
        {
            long totalBytes = 0;
            
            lock(_ResponseStream)
            {
                string rangeVals = "";

                if(_Headers.TryGetValue("range", out rangeVals))
                {
                    WriteRangedResponse(fs, rangeVals);
                }
                else
                {
                    try
                    {
                        string reformat = String.Format(_ResponseTemplate, _HTTPVersion, "200", "OK", fs.Length, Path.GetExtension(_URI));
                        _ResponseStream.Write(Encoding.ASCII.GetBytes(reformat), 0, reformat.Length);

                        int bytesRead = 0;
                        while (true)
                        {
                            byte[] buf = new byte[7500];
                            bytesRead = fs.Read(buf, 0, buf.Length);
                            //Finished reading
                            if (bytesRead == 0) { break; }

                            _ResponseStream.Write(buf, 0, bytesRead);
                            totalBytes += bytesRead;

                        }

                        fs.Position = 0;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            

            return true;
            
        }

        //This writes a response if the correct handler was found
        public bool WriteHTMLResponse(string htmlString)
        {

            string reformat = String.Format(_ResponseTemplate, _HTTPVersion, "200", "OK", "text/html", htmlString.Length);

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

        //Support ranged content headers
        private bool WriteRangedResponse(Stream fs, string range_vals)
        {
            int beginIndex, endIndex;

            bool findRange = GetBeginEndBytes(range_vals, out beginIndex, out endIndex);
            //Format and send response header
            string reformat = String.Format(_PartialResponseTemplate, _HTTPVersion, "206", "Partial Content", (endIndex - beginIndex).ToString(), fs.Length, Path.GetExtension(_URI));
            _ResponseStream.Write(Encoding.ASCII.GetBytes(reformat), 0, reformat.Length);

            int totalBytes = 0;
            //set the begining index
            fs.Position = beginIndex;

            while(totalBytes < (endIndex - beginIndex))
            {

                byte[] buf = new byte[6144];
                //Read from the file stream in chuncks no larger than 8kb
                int read = fs.Read(buf, 0, buf.Length);

                totalBytes += read;


                //Catch the case if we read more than was requested by the range header
                if (totalBytes > (endIndex - beginIndex))
                {
                    int overFlow = totalBytes - (endIndex - beginIndex);
                    _ResponseStream.Write(buf, 0, read - overFlow);
                }
                else
                {
                    _ResponseStream.Write(buf, 0, read);
                }
            }
            return true;

        }

        private bool GetBeginEndBytes(string range_vals, out int begin_byte, out int end_byte)
        {
            //Get the byte range values to write
            string sub_range = range_vals.Substring("bytes=".Length);
            //split the values and 
            string[] pieces = sub_range.Split(new[] { '-' }, 2);
            try
            {
                begin_byte = Convert.ToInt32(pieces[0]);
                end_byte = Convert.ToInt32(pieces[1]);
                return true;
            }
            catch
            {
                end_byte = 0;
                begin_byte = 0;
                return false;
            }


        }

    }
}
