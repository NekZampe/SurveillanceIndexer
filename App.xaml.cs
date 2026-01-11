using SurveillanceIndexer.Contexts;
using SurveillanceIndexer.Services;
using System.Configuration;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using System.IO;

namespace SurveillanceIndexer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }
        public static DatabaseService DbService { get; private set; }
        public App()
        {
            // 1. Create the Host Builder
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // 2. Configure the Database Factory
                    services.AddDbContextFactory<SurveillanceContext>(options =>
                    {
                        // Create Data folder if it doesn't exist
                        string dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                        Directory.CreateDirectory(dataFolder);

                        string dbPath = Path.Combine(dataFolder, "surveillance.db");

                        System.Diagnostics.Debug.WriteLine($"[DATABASE PATH] {dbPath}");

                        options.UseSqlite($"Data Source={dbPath}");
                        options.UseLazyLoadingProxies();
                    });

                    services.AddSingleton<DatabaseService>();

                    // 4.Register MainWindow so it can use DI too
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            // 5. Ask the Host for the DatabaseService instance
            // This triggers the constructor: new DatabaseService(factory)
            DbService = AppHost.Services.GetRequiredService<DatabaseService>();

            // 6. Run your custom initialization logic
            DbService.Initialize();

            // 7. Open the MainWindow
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // Clean up the Host gracefully
            using (AppHost)
            {
                await AppHost!.StopAsync();
            }
            base.OnExit(e);
        }
    }

}
