//using Android.App;
//using Android.Content.PM;
//using Android.OS;

//namespace SensorApp
//{
//    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
//    public class MainActivity : MauiAppCompatActivity
//    {
//    }
//}

using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace SensorApp.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int ACTIVITY_RECOGNITION_REQUEST_CODE = 1001;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        System.Diagnostics.Debug.WriteLine("MainActivity.OnCreate llamado");
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        try
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            System.Diagnostics.Debug.WriteLine($"OnRequestPermissionsResult - RequestCode: {requestCode}");

            if (requestCode == ACTIVITY_RECOGNITION_REQUEST_CODE)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Permiso ACTIVITY_RECOGNITION concedido en MainActivity");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Permiso ACTIVITY_RECOGNITION denegado en MainActivity");
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnRequestPermissionsResult: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        try
        {
            base.OnResume();
            System.Diagnostics.Debug.WriteLine("MainActivity.OnResume llamado");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en MainActivity.OnResume: {ex.Message}");
        }
    }

    protected override void OnPause()
    {
        try
        {
            base.OnPause();
            System.Diagnostics.Debug.WriteLine("MainActivity.OnPause llamado");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en MainActivity.OnPause: {ex.Message}");
        }
    }
}