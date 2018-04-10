using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XMoat.Common;
using System.Net;
using System.Threading;

namespace XMoat.Service
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddSingleton<HotfixLoader>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

            // 异步方法全部会回掉到主线程：KNet必须单线程！
            OneThreadSynchronizationContext contex = new OneThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(contex);

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234);
            var netcom = new NetworkComponent();
            netcom.Awake(NetworkProtocol.KCP, ipEndPoint);


            contex.Update();
            //todo: 谁来调用Update???
            //netcom.Update();

            //var hotfixLoader = app.ApplicationServices.GetService<HotfixLoader>();
            //Task thot = hotfixLoader.Load();
        }
    }
}
