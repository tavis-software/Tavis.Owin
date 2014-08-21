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
                    ExceptionLogger = config.Services.GetService(typeof(IExceptionLogger)) as IExceptionLogger ?? new WebApiExceptionLogger(),
                    ExceptionHandler = config.Services.GetService(typeof(IExceptionHandler)) as IExceptionHandler ?? new WebApiExceptionHandler()
                };
            }
            var handler = new HttpMessageHandlerAdapter(null, options);
            return (env) => handler.Invoke(new OwinContext(env));
        }
    }

    public class WebApiExceptionLogger : IExceptionLogger
    {
        public Task LogAsync(ExceptionLoggerContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(null);
        }
    }

    public class WebApiExceptionHandler : IExceptionHandler
    {
        public Task HandleAsync(ExceptionHandlerContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(null);
        }
    }
}
