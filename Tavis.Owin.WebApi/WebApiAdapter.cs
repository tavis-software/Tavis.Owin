using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Owin;
using Microsoft.Owin;

namespace Tavis.Owin
{
    public static class WebApiAdapter
    {
        public static Func<IDictionary<string, object>, Task> CreateWebApiAppFunc(HttpConfiguration config, HttpMessageHandlerOptions options = null)
        {
            var app = new HttpServer(config);
            if (options == null)
            {
                options = new HttpMessageHandlerOptions()
                {
                    MessageHandler = app,
                    BufferPolicySelector = new OwinBufferPolicySelector(),
                    ExceptionLogger = ExceptionServices.GetLogger(config),
                    ExceptionHandler = ExceptionServices.GetHandler(config)
                };
            }
            var handler = new HttpMessageHandlerAdapter(null, options);
            return (env) => handler.Invoke(new OwinContext(env));
        }
    }
}
