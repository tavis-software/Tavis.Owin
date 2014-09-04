using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tavis.Owin;
using Topshelf.Logging;
using appFunc = System.Func<System.Collections.Generic.IDictionary<string,object>,System.Threading.Tasks.Task>;
namespace SampleService
{
    class Program
    {
        static void Main(string[] args)
        {
            //var ts = new TraceSource("Foo");
            //ts.TraceInformation("Hello world");
            //ts.Flush();
            Console.WriteLine("Starting service on http://localhost:1002/");
            var service = new OwinServiceHost(new Uri("http://localhost:1002/"), simpleApp)
            {
                ServiceName = "SampleService",
                ServiceDisplayName = "Sample Service",
                ServiceDescription = "A sample service"
            };                                                                              
            service.Initialize();
            
        }

        private static appFunc simpleApp = (env) =>
        {
            var sw = new StreamWriter((Stream) env["owin.ResponseBody"]);
            var headers = (IDictionary<string, string[]>) env["owin.ResponseHeaders"];
            var content = "Hello World";
            headers["Content-Length"] = new string[] {content.Length.ToString()};
            headers["Content-Type"] = new string[] {"text/plain"};
            var task = sw.WriteAsync(content);
            sw.Flush();

            return task;
        };
    }
}
