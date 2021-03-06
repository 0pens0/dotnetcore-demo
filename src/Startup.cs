﻿using System;
using System.Data.Common;
using System.Linq;
using Articulate.Models;
using Articulate.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Steeltoe.CloudFoundry.Connector;
using Steeltoe.CloudFoundry.Connector.MySql;
using Steeltoe.CloudFoundry.Connector.MySql.EFCore;
using Steeltoe.CloudFoundry.Connector.Services;
using Steeltoe.Discovery.Client;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Management.CloudFoundry;
using Steeltoe.Management.Endpoint;
using Steeltoe.Management.Endpoint.Env;
using Steeltoe.Management.Hypermedia;
using Steeltoe.Management.Tracing;

namespace Articulate
{
    public class Startup
    {
        private bool _isMySqlServiceBound;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            _isMySqlServiceBound = Configuration.GetServiceInfos<MySqlServiceInfo>().Any();
        }

      

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureCloudFoundryOptions(Configuration);
            services.AddMvc(options => options.EnableEndpointRouting = false);
            services.AddSingleton(Configuration);

            services.AddCloudFoundryActuators(Configuration, MediaTypeVersion.V2, ActuatorContext.ActuatorAndCloudFoundry);
            services.AddEnvActuator(Configuration);
            services.AddScoped<AppEnv>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddDistributedTracing(Configuration);
            services.AddDbContext<AttendeeContext>(options =>
            {
                if (_isMySqlServiceBound)
                    options.UseMySql(Configuration);
                else
                    options.UseSqlite("DataSource=:memory:");
                
            }, _isMySqlServiceBound ? ServiceLifetime.Scoped : ServiceLifetime.Singleton);
            if (_isMySqlServiceBound)
            {
                services.AddMySqlConnection(Configuration);
            }
            
            services.AddDiscoveryClient(Configuration);
            // return services.BuildServiceProvider(false);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseFileServer();
            app.UseEnvActuator();
            app.UseCloudFoundryActuators(MediaTypeVersion.V2,ActuatorContext.ActuatorAndCloudFoundry);

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
            app.EnsureMigrationOfContext<AttendeeContext>();
            
            app.UseDiscoveryClient();
        }
        
    }
    public static class EnsureMigration
    {
        static DbConnection _connection;
        public static void EnsureMigrationOfContext<T>(this IApplicationBuilder app) where T : DbContext
        {
            
            var context = app.ApplicationServices.CreateScope().ServiceProvider.GetService<T>();
            _connection = context.Database.GetDbConnection();
            _connection.Open();
            context.Database.Migrate();
        }
    }
}