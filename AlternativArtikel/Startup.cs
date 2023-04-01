using AlternativArtikel.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlternativArtikel
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            MyConfiguration.ShowSwagger = Configuration.GetValue<bool>("AlternativArtikel:Swagger");
            MyConfiguration.SwaggerEndpoint = Configuration.GetValue<string>("AlternativArtikel:SwaggerEndpoint");

            MyConfiguration.Url1 = Configuration.GetValue<string>("Solr:Url1");
            MyConfiguration.Url2 = Configuration.GetValue<string>("Solr:Url2");

            services.AddLogging(loggingBuilder => {
                loggingBuilder.AddFile("AlternativArtikel.log", append: true);
            });

            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                });
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "AlternativArtikel", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AlternativArtikel v1"));
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "AlternativArtikel v1"));
            } 
            else
            {
                //app.UseDeveloperExceptionPage();
                //app.UseSwagger();
                //app.UseSwaggerUI(c =>
                //{
                //    c.RoutePrefix = "swagger";
                //    c.SwaggerEndpoint("v1/swagger.json", "AlternativArtikel v1");
                //});
                //app.UseSwaggerUI(c => c.SwaggerEndpoint("http://12.0.0.25/int/AlternativArtikel/swagger/v1/swagger.json", "AlternativArtikel v1"));
                //app.UseSwaggerUI(c => c.SwaggerEndpoint("https://www.pvwelt-connect.de/int/AlternativArtikel/swagger/v2/swagger.json", "AlternativArtikel v1.1"));

                if (MyConfiguration.ShowSwagger)
                {
                    app.UseDeveloperExceptionPage();
                    app.UseSwagger();
                    app.UseSwaggerUI(c => c.SwaggerEndpoint(MyConfiguration.SwaggerEndpoint, "ModuleStore_Core v1"));
                }
            }

            //app.UseHttpsRedirection();
            app.UseCors("CorsPolicy");
            app.UseRouting();
            app.UseCors(MyAllowSpecificOrigins);
            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
