using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tavis.Owin.Tests
{
    public class OwinServiceTests
    {
        [Fact]
        public void CreateHost()
        {
            var host = new OwinServiceHost(new Uri("http://localhost:1200/"),(d)=> null );
            Assert.NotNull(host);
        }

        [Fact]
        public void InitializeHost()
        {
            var host = new OwinServiceHost(new Uri("http://localhost:1200/"), (d) => null);
            var returncode = host.Initialize();

        }
    }

    
}
