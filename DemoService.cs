using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CS422
{
    class DemoService:WebService
    {
        private const string c_template =
                                     "<html>This is the response to the request:<br>" +
                                     "Method: {0}<br>Request-Target/URI: {1}<br>" +
                                     "Request body size, in bytes: {2}<br><br>" +
                                     "Student ID: {3}<br>" + "</html>";

        public override void Handler(WebRequest req)
        {

            string temp = String.Format(c_template, req.HTTPMethod, req.RequestURI, 0, 11114444);

            temp = String.Format(c_template, req.HTTPMethod, req.RequestURI, Encoding.ASCII.GetByteCount(temp), 11114444);

            req.WriteHTMLResponse(temp);
        }

        public override string ServiceURI
        {
            get
            {
                return "/";
            }
        }
    }
}
