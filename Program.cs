using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS422
{
    class Program
    {
        static void Main(string[] args)
        {

            DemoService ds = new DemoService();
            StandardFileSystem fs = StandardFileSystem.Create(@"C:\Users\adamskoog\Documents");
            FilesWebService fws = new FilesWebService(fs);
            //WebServer.AddService(ds);
            WebServer.AddService(fws);


            WebServer.Start(4000, 23);

            Console.ReadKey();
        }
    }
}
