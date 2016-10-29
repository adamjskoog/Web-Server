using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace CS422
{
    class FilesWebService : WebService
    {
        private readonly FileSys422 _fs;

        public FilesWebService(FileSys422 fs)
        {
            _fs = fs;
        }

        public override string ServiceURI
        {
            get
            {
                return "/files";
            }
        }

        public override void Handler(WebRequest req)
        {
            if (!req.RequestURI.StartsWith(ServiceURI))
            {
                throw new InvalidOperationException();
            }

            //percent decode here with WebUtility.URLEncode and WebUtility.Decode
            string decodedURI = WebUtility.UrlDecode(req.RequestURI);

            //Base case is that the /files or /files/ url was only acessed, exposed all dirs/files in root
            if (decodedURI == ServiceURI || decodedURI == "/files/")
            {
                req.WriteHTMLResponse(BuildDirHTML(_fs.GetRoot(), ref req));
            }
            else if (!decodedURI.StartsWith("/files/"))
            {
                //Check to see if the base of the request URI is valid, return 404 if not
                req.WriteNotFoundResponse("<html> 404 Page Not Found </html>");
            }
            else
            {
                string[] pieces = decodedURI.Substring(ServiceURI.Length).Split('/');

                //Now that the URI is parsed, get a reference to the root of the FS and find the file/dir
                Dir422 dir = _fs.GetRoot();
                for (int i = 0; i < pieces.Length - 1; i++)
                {
                    string piece = pieces[i];
                    if (string.IsNullOrEmpty(piece))
                    { }
                    else
                    {
                        dir = dir.GetDir(piece);
                        if (dir == null)
                        {
                            req.WriteNotFoundResponse("<html> Page cannot be reached due to unauthorized file or directory access </html>");
                            return;
                        }
                    }

                    
                }

                //Now that we are in the dir that contains the dir/file we are looking for, check its type
                File422 file = dir.GetFile(pieces[pieces.Length - 1]);
                if (file != null)
                {
                    req.WriteFileResponse(file.OpenReadOnly());
                }
                else
                {
                    Dir422 final_dir = dir.GetDir(pieces[pieces.Length - 1]);
                    if (final_dir != null)
                    {
                        if(!string.IsNullOrEmpty(BuildDirHTML(final_dir,ref req)))
                            req.WriteHTMLResponse(BuildDirHTML(final_dir, ref req));
                        else
                            req.WriteNotFoundResponse("");
                    }
                    else
                    {
                        req.WriteNotFoundResponse("");
                    }
                    

                }
            }


            
        }
        

        private string BuildDirHTML(Dir422 directory, ref WebRequest req)
        {
            StringBuilder htmlResponse = new StringBuilder();
            
            htmlResponse.AppendLine("<html>");

            //Get the folders and formats it into html response
            htmlResponse.AppendLine("<h1>Folders</h1>");
            try
            {
                foreach (Dir422 dir in directory.GetDirs())
                {
                    //Encode the URI
                    string encodedURI = WebUtility.UrlEncode(req.RequestURI);

                    htmlResponse.AppendFormat(@"<a href= '{0}/{1}'>{1}</a>", req.RequestURI, dir.Name);
                    htmlResponse.AppendLine("<br>");

                }

                htmlResponse.AppendLine("<h1>Files</h1>");

                foreach (File422 file in directory.GetFiles())
                {
                    //Encode the URI
                    string encodedURI = WebUtility.UrlEncode(req.RequestURI);

                    htmlResponse.AppendFormat(@"<a href= '{0}/{1}'>{1}</a>", req.RequestURI, file.Name);
                    htmlResponse.AppendLine("<br>");
                }

                htmlResponse.AppendLine("</html>");

                return htmlResponse.ToString();
            }
            catch
            {
                //Null for getDirs/files possibly due to unauthorized access
                req.WriteNotFoundResponse("<html> Page cannot be reached due to unauthorized file or directory access </html>");
                return null;
            }
            
        }

    }
}
