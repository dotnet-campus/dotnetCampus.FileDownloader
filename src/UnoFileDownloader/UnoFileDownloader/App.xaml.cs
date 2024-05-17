using System;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

using Uno.Resizetizer;
using UnoFileDownloader.Business;
using UnoFileDownloader.Business.Models;
using UnoFileDownloader.Presentation;
using UnoFileDownloader.Utils;

namespace UnoFileDownloader;
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
       
        
        var builder = this.CreateBuilder(args)
            .UseToolkitNavigation()
            .Configure(host =>
            {
                host.UseConfiguration(configure: configBuilder =>
                        configBuilder
                            .EmbeddedSource<App>()
                            .Section<AppConfig>())
                    .UseLocalization()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddSingleton<IDispatcherQueueProvider>(c =>
                        {
                            return new DispatcherQueueProvider(MainWindow!.DispatcherQueue);
                        });
                        
                        // 下载文件管理器
                        services.AddSingleton<DownloadFileListManager>();
                    })
                    .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
                    ;
            });
        MainWindow = builder.Window;
        //MainWindow = new Window();
#if DEBUG
        MainWindow.EnableHotReload();
#endif
        Host = await builder.NavigateAsync<Shell>();
        var localizer = (Uno.Extensions.Localization.ResourceLoaderStringLocalizer) Host.Services.GetRequiredService<IStringLocalizer>();
        string title = localizer["ApplicationName"];
        
        MainWindow.Title = title;

        //// Do not repeat app initialization when the Window already has content,
        //// just ensure that the window is active
        //if (MainWindow.Content is not Frame rootFrame)
        //{
        //    // Create a Frame to act as the navigation context and navigate to the first page
        //    rootFrame = new Frame();

        //    // Place the frame in the current Window
        //    MainWindow.Content = rootFrame;

        //    rootFrame.NavigationFailed += OnNavigationFailed;
        //}

        //if (rootFrame.Content == null)
        //{
        //    // When the navigation stack isn't restored navigate to the first page,
        //    // configuring the new page by passing required information as a navigation
        //    // parameter
        //    rootFrame.Navigate(typeof(Shell), args.Arguments);
        //}

        MainWindow.SetWindowIcon();
        // Ensure the current window is active
        MainWindow.Activate();
    }
    
    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register
        (
            new ViewMap(ViewModel: typeof(ShellModel), View: typeof(Shell)),
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<AboutPage, AboutModel>(),
            new ViewMap<NewTaskPage, NewTaskModel>(),
            new DataViewMap<SecondPage, SecondModel, Entity>()
        );
        
        // 必须写注册哦，否则无法跳转
        routes.Register
        (
            new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                Nested: new RouteMap[]
                {
                    new RouteMap("Main",   View: views.FindByViewModel<MainModel>()),
                    new RouteMap("Second", View: views.FindByViewModel<SecondModel>()),
                    new RouteMap("About",  View: views.FindByViewModel<AboutModel>()),
                    new RouteMap("NewTask",View: views.FindByViewModel<NewTaskModel>()),
                }
            )
        );
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__ || __MACCATALYST__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
