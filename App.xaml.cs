//namespace SensorApp
//{
//    public partial class App : Application
//    {
//        public App()
//        {
//            InitializeComponent();

//            //MainPage = new AppShell();
//            MainPage = new NavigationPage(new MainPage());
//        }
//    }
//}

using Microsoft.Extensions.Logging;

namespace SensorApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        try
        {
            base.OnStart();
            System.Diagnostics.Debug.WriteLine("Aplicación iniciada");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnStart: {ex.Message}");
        }
    }

    protected override void OnSleep()
    {
        try
        {
            base.OnSleep();
            System.Diagnostics.Debug.WriteLine("Aplicación entrando en suspensión");

            // Notificar a la página principal si está disponible
            if (MainPage is AppShell shell && shell.CurrentPage is MainPage mainPage)
            {
                // El MainPage manejará su propio estado en OnDisappearing
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnSleep: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        try
        {
            base.OnResume();
            System.Diagnostics.Debug.WriteLine("Aplicación reanudando");

            // Notificar a la página principal si está disponible
            if (MainPage is AppShell shell && shell.CurrentPage is MainPage mainPage)
            {
                // El MainPage manejará su propio estado en OnAppearing
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnResume: {ex.Message}");
        }
    }
}