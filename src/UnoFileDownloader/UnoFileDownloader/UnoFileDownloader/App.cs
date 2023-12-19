using Microsoft.Extensions.DependencyInjection;

using UnoFileDownloader.Utils;

namespace UnoFileDownloader
{
    public class App : Application
    {
        protected Window? MainWindow { get; private set; }
        protected IHost? Host { get; private set; }

        protected async override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var builder = this.CreateBuilder(args)
                // Add navigation support for toolkit controls such as TabBar and NavigationView
                .UseToolkitNavigation()
                .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                    .UseLogging(configure: (context, logBuilder) =>
                    {
                        // Configure log levels for different categories of logging
                        logBuilder
                            .SetMinimumLevel(
                                context.HostingEnvironment.IsDevelopment() ?
                                    LogLevel.Information :
                                    LogLevel.Warning)

                            // Default filters for core Uno Platform namespaces
                            .CoreLogLevel(LogLevel.Warning);

                        // Uno Platform namespace filter groups
                        // Uncomment individual methods to see more detailed logging
                        //// Generic Xaml events
                        //logBuilder.XamlLogLevel(LogLevel.Debug);
                        //// Layout specific messages
                        //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                        //// Storage messages
                        //logBuilder.StorageLogLevel(LogLevel.Debug);
                        //// Binding related messages
                        //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                        //// Binder memory references tracking
                        //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                        //// DevServer and HotReload related
                        //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                        //// Debug JS interop
                        //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                    }, enableUnoLogging: true)
                    .UseConfiguration(configure: configBuilder =>
                        configBuilder
                            .EmbeddedSource<App>()
                            .Section<AppConfig>()
                    )
                    // Enable localization (see appsettings.json for supported languages)
                    .UseLocalization()
                    // Register Json serializers (ISerializer and ISerializer)
                    .UseSerialization((context, services) => services
                        .AddContentSerializer(context)
                        //.AddJsonTypeInfo(WeatherForecastContext.Default.IImmutableListWeatherForecast)
                        )
                    .UseHttp((context, services) => services
                    // Register HttpClient
#if DEBUG
                    // DelegatingHandler will be automatically injected into Refit Client
                    .AddTransient<DelegatingHandler, DebugHttpHandler>()
#endif
                        )
                    .ConfigureServices((context, services) =>
                    {
                        // TODO: Register your services
                        //services.AddSingleton<IMyService, MyService>();
                        services.AddSingleton<IDispatcherQueueProvider>(c =>
                        {
                            return new DispatcherQueueProvider(MainWindow!.DispatcherQueue);
                        });
                    })
                    .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
                );
            MainWindow = builder.Window;

#if DEBUG
            MainWindow.EnableHotReload();
#endif

            Host = await builder.NavigateAsync<AboutPage>();
        }

        private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
        {
            views.Register(
                new ViewMap(ViewModel: typeof(ShellModel), View: typeof(Shell)),
                new ViewMap<MainPage, MainModel>(),
                new ViewMap<AboutPage, AboutModel>(),
                new DataViewMap<SecondPage, SecondModel, Entity>()
            );

            routes.Register(
                new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                    Nested: new RouteMap[]
                    {
                    new RouteMap("Main", View: views.FindByViewModel<MainModel>()),
                    new RouteMap("Second", View: views.FindByViewModel<SecondModel>()),
                    new RouteMap("About", View: views.FindByViewModel<AboutModel>()),
                    }
                )
            );
        }
    }
}
