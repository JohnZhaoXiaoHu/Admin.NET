﻿using Admin.NET.Core;
using Admin.NET.Core.Hubs;
using Admin.NET.Core.Service;
using Furion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OnceMi.AspNetCore.OSS;
using Serilog;
using Yitter.IdGenerator;

namespace Admin.NETApp.Web.Core
{
    public class Startup : AppStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddJwt<JwtHandler>(enableGlobalAuthorize: true);
            services.AddCorsAccessor();
            services.AddRemoteRequest();
            services.AddControllersWithViews()
                    .AddMvcFilter<RequestActionFilter>()
                    .AddInjectWithUnifyResult<XnRestfulResultProvider>()
                    .AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    });
            services.AddRemoteRequest();
            services.AddViewEngine();
            services.AddSignalR();
            services.AddSimpleEventBus();

            if (App.Configuration["Cache:CacheType"] == "RedisCache")
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = App.Configuration["Cache:RedisConnectionString"]; // redis连接配置
                    options.InstanceName = App.Configuration["Cache:InstanceName"]; // 键名前缀
                });
            }

            //// default minio
            //// 添加默认对象储存配置信息
            //services.AddOSSService(option =>
            //{
            //    option.Provider = OSSProvider.Minio;
            //    option.Endpoint = "oss.oncemi.com:9000";
            //    option.AccessKey = "Q*************9";
            //    option.SecretKey = "A**************************Q";
            //    option.IsEnableHttps = true;
            //    option.IsEnableCache = true;
            //});

            // aliyun oss
            // 添加名称为‘aliyunoss’的OSS对象储存配置信息
            services.AddOSSService("aliyunoss", option =>
            {
                option.Provider = OSSProvider.Aliyun;
                option.Endpoint = "oss-cn-hangzhou.aliyuncs.com";
                option.AccessKey = "L*******************U";
                option.SecretKey = "5*******************************T";
                option.IsEnableCache = true;
            });

            //// qcloud oss
            //// 从配置文件中加载节点为‘OSSProvider’的配置信息
            //services.AddOSSService("QCloud", "OSSProvider");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            //  NGINX 反向代理获取真实IP
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // 添加状态码拦截中间件
            app.UseUnifyResultStatusCodes();

            app.UseHttpsRedirection(); // 强制https
            app.UseStaticFiles();

            // Serilog请求日志中间件---必须在 UseStaticFiles 和 UseRouting 之间
            app.UseSerilogRequestLogging();

            app.UseRouting();

            app.UseCorsAccessor();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseInject(string.Empty);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ChatHub>("/hubs/chathub");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            // 设置雪花Id的workerId，确保每个实例workerId都应不同
            var workerId = ushort.Parse(App.Configuration["SnowId:WorkerId"] ?? "1");
            YitIdHelper.SetIdGenerator(new IdGeneratorOptions { WorkerId = workerId });

            // 开启自启动定时任务
            App.GetService<ISysTimerService>().StartTimerJob();
        }
    }
}