using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

#if ANDROID
using Android;
using AndroidX.Core.Content;
using Android.Content.PM;
using Android.Hardware;
using Android.Content;
using AndroidX.Core.App;
#endif

namespace SensorApp;

public partial class MainPage : ContentPage
{
#if ANDROID
    SensorManager _sensorManager;
    Sensor _stepCounter;
    SensorListener _listener;
    private bool _isListenerActive = false;
    private const int ACTIVITY_RECOGNITION_REQUEST_CODE = 1001;
#endif

    private int _stepCount = 0;
    private int _initialStepCount = -1;

    public MainPage()
    {
        InitializeComponent();
        CheckAndAskAgeAsync();

#if ANDROID
        InitializeAndroidSensors();
#endif
    }

#if ANDROID
    private void InitializeAndroidSensors()
    {
        try
        {
            // Obtener SensorManager
            var context = Platform.CurrentActivity?.BaseContext ?? Android.App.Application.Context;
            _sensorManager = (SensorManager)context.GetSystemService(Context.SensorService);

            if (_sensorManager == null)
            {
                System.Diagnostics.Debug.WriteLine("No se pudo obtener SensorManager");
                return;
            }

            // Obtener sensor de pasos
            _stepCounter = _sensorManager.GetDefaultSensor(SensorType.StepCounter);

            if (_stepCounter == null)
            {
                System.Diagnostics.Debug.WriteLine("Sensor de pasos no disponible en este dispositivo");
                return;
            }

            // Crear listener
            _listener = new SensorListener();
            _listener.SensorChanged += OnSensorChanged;

            System.Diagnostics.Debug.WriteLine($"Sensor inicializado: {_stepCounter.Name}");
            System.Diagnostics.Debug.WriteLine($"Vendor: {_stepCounter.Vendor}");

            // Inicializar el sensor
            InitializeSensor();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error inicializando sensores Android: {ex.Message}");
        }
    }

    private async void InitializeSensor()
    {
        if (await CheckAndRequestActivityRecognitionPermission())
        {
            RegisterSensorListener();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Permiso denegado para sensores");
        }
    }

    private async Task<bool> CheckAndRequestActivityRecognitionPermission()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;

            // Para Android 10 (API 29) y superior, necesitamos ACTIVITY_RECOGNITION
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
            {
                // Verificar si ya tenemos el permiso
                if (ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition)
                    == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Permiso ACTIVITY_RECOGNITION ya concedido");
                    return true;
                }

                // Solicitar el permiso
                if (Platform.CurrentActivity is AndroidX.AppCompat.App.AppCompatActivity activity)
                {
                    ActivityCompat.RequestPermissions(activity,
                        new string[] { Android.Manifest.Permission.ActivityRecognition },
                        ACTIVITY_RECOGNITION_REQUEST_CODE);

                    // Esperar un momento para que se procese
                    await Task.Delay(2000);

                    // Verificar nuevamente
                    var finalStatus = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                    bool granted = finalStatus == Permission.Granted;

                    System.Diagnostics.Debug.WriteLine($"Permiso ACTIVITY_RECOGNITION después de solicitud: {granted}");
                    return granted;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo obtener la Activity para solicitar permisos");
                    return false;
                }
            }
            else
            {
                // Para versiones anteriores a Android 10, no se necesita permiso específico
                System.Diagnostics.Debug.WriteLine("Android < 10, no se requiere ACTIVITY_RECOGNITION");
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando permisos: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            return false;
        }
    }

    // Método alternativo usando solo verificación nativa
    private bool CheckActivityRecognitionPermissionNative()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
            {
                var permission = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                bool granted = permission == Permission.Granted;
                System.Diagnostics.Debug.WriteLine($"Permiso nativo ACTIVITY_RECOGNITION: {granted}");
                return granted;
            }

            return true; // Para versiones anteriores
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando permiso nativo: {ex.Message}");
            return false;
        }
    }

    private void RegisterSensorListener()
    {
        try
        {
            // Verificación adicional de permiso usando método nativo
            if (!CheckActivityRecognitionPermissionNative())
            {
                System.Diagnostics.Debug.WriteLine("Sin permiso ACTIVITY_RECOGNITION a nivel sistema");
                return;
            }

            if (_stepCounter != null && _listener != null && !_isListenerActive)
            {
                System.Diagnostics.Debug.WriteLine($"Registrando sensor: {_stepCounter.Name}");
                System.Diagnostics.Debug.WriteLine($"Vendor: {_stepCounter.Vendor}");
                System.Diagnostics.Debug.WriteLine($"Tipo: {_stepCounter.Type}");

                bool registered = _sensorManager.RegisterListener(_listener, _stepCounter, SensorDelay.Normal);
                _isListenerActive = registered;

                System.Diagnostics.Debug.WriteLine($"Listener registrado: {registered}");

                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("Falló el registro del listener del sensor");

                    // Diagnóstico adicional
                    var availableSensors = _sensorManager.GetSensorList(SensorType.StepCounter);
                    System.Diagnostics.Debug.WriteLine($"Sensores de pasos disponibles: {availableSensors.Count}");

                    foreach (var sensor in availableSensors)
                    {
                        System.Diagnostics.Debug.WriteLine($"- Sensor: {sensor.Name}, Vendor: {sensor.Vendor}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Condiciones no cumplidas:");
                System.Diagnostics.Debug.WriteLine($"- _stepCounter null: {_stepCounter == null}");
                System.Diagnostics.Debug.WriteLine($"- _listener null: {_listener == null}");
                System.Diagnostics.Debug.WriteLine($"- _isListenerActive: {_isListenerActive}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error registrando listener: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }

    // Clase para el listener del sensor
    private class SensorListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<SensorEvent> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            // No necesario para nuestro caso
        }

        public void OnSensorChanged(SensorEvent e)
        {
            SensorChanged?.Invoke(e);
        }
    }
#endif

    private async Task ListAvailableSensors()
    {
#if ANDROID
        try
        {
            if (_sensorManager != null)
            {
                var allSensors = _sensorManager.GetSensorList(SensorType.All);
                System.Diagnostics.Debug.WriteLine($"Total de sensores disponibles: {allSensors.Count}");

                var sensorInfo = new System.Text.StringBuilder();
                sensorInfo.AppendLine($"Sensores disponibles ({allSensors.Count}):");

                foreach (var sensor in allSensors)
                {
                    sensorInfo.AppendLine($"- {sensor.Name} (Tipo: {sensor.Type})");
                    System.Diagnostics.Debug.WriteLine($"Sensor: {sensor.Name} - Tipo: {sensor.Type} - Vendor: {sensor.Vendor}");
                }

                // Mostrar información de sensores en un alert (opcional, puedes comentar esta línea)
                await DisplayAlert("Sensores Disponibles", sensorInfo.ToString(), "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error listando sensores: {ex.Message}");
        }
#endif
    }

    private async void CheckAndAskAgeAsync()
    {
        if (!Preferences.ContainsKey("user_age"))
        {
            string ageInput = await DisplayPromptAsync("Edad", "Por favor, ingresa tu edad:");

            if (int.TryParse(ageInput, out int age))
            {
                Preferences.Set("user_age", age);
                int goal = 0;
                string meta = "";

                if (age >= 6 && age <= 17)
                {
                    goal = 12000;
                    meta = "12,000 – 15,000 pasos";
                }
                else if (age >= 18 && age <= 64)
                {
                    goal = 10000;
                    meta = "7,000 – 10,000 pasos";
                }
                else if (age >= 65)
                {
                    goal = 8000;
                    meta = "6,000 – 8,000 pasos";
                }
                else
                {
                    meta = "Edad no válida";
                }

                Preferences.Set("daily_steps_goal_text", meta);
                Preferences.Set("daily_steps_goal_value", goal);
                StepsGoalLabel.Text = $"Meta diaria: {meta}";
            }
            else
            {
                await DisplayAlert("Error", "Edad no válida. Intenta nuevamente.", "OK");
                await Task.Delay(500);
                CheckAndAskAgeAsync();
            }
        }
        else
        {
            string savedGoalText = Preferences.Get("daily_steps_goal_text", "Meta no establecida");
            StepsGoalLabel.Text = $"Meta diaria: {savedGoalText}";
        }

        LoadSavedSteps();
    }

    private void LoadSavedSteps()
    {
        var today = DateTime.Today;
        _stepCount = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
        _initialStepCount = Preferences.Get($"initial_steps_{today:yyyy-MM-dd}", -1);

        StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
        UpdateStepsRemaining();

        System.Diagnostics.Debug.WriteLine($"Pasos cargados: {_stepCount}, Inicial: {_initialStepCount}");
    }

#if ANDROID
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_stepCounter != null && _listener != null && !_isListenerActive)
        {
            RegisterSensorListener();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnregisterSensorListener();
    }

    private void UnregisterSensorListener()
    {
        try
        {
            if (_stepCounter != null && _listener != null && _isListenerActive)
            {
                _sensorManager.UnregisterListener(_listener);
                _isListenerActive = false;
                System.Diagnostics.Debug.WriteLine("Listener desregistrado");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error desregistrando listener: {ex.Message}");
        }
    }

    private void OnSensorChanged(SensorEvent e)
    {
        try
        {
            if (e.Sensor.Type == SensorType.StepCounter)
            {
                int sensorValue = (int)e.Values[0];
                var today = DateTime.Today;

                System.Diagnostics.Debug.WriteLine($"Valor del sensor: {sensorValue}");

                if (_initialStepCount == -1)
                {
                    _initialStepCount = sensorValue;
                    Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
                    System.Diagnostics.Debug.WriteLine($"Valor inicial establecido: {_initialStepCount}");
                }

                int currentDaySteps = sensorValue - _initialStepCount;
                if (currentDaySteps < 0)
                {
                    _initialStepCount = sensorValue;
                    currentDaySteps = 0;
                    Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
                    System.Diagnostics.Debug.WriteLine("Sensor resetado, reiniciando conteo");
                }

                _stepCount = currentDaySteps;
                Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
                    UpdateStepsRemaining();
                });

                System.Diagnostics.Debug.WriteLine($"Pasos actualizados: {_stepCount}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnSensorChanged: {ex.Message}");
        }
    }
#endif

    private void UpdateStepsRemaining()
    {
        int goal = Preferences.Get("daily_steps_goal_value", 0);

        if (goal > 0)
        {
            int remaining = Math.Max(0, goal - _stepCount);
            StepsRemainingLabel.Text = $"Pasos restantes: {remaining}";

            if (remaining == 0)
            {
                StepsRemainingLabel.TextColor = Colors.Green;
                DisplayAlert("¡Felicidades!", "Has alcanzado tu meta diaria de pasos.", "OK");
            }
            else
            {
                StepsRemainingLabel.TextColor = Colors.DarkRed;
            }
        }
        else
        {
            StepsRemainingLabel.Text = "Pasos restantes: (sin meta)";
        }
    }

    private async void OnResetAgeClicked(object sender, EventArgs e)
    {
        Preferences.Remove("user_age");
        Preferences.Remove("daily_steps_goal_text");
        Preferences.Remove("daily_steps_goal_value");

        StepsGoalLabel.Text = "Meta diaria: cargando...";
        StepsRemainingLabel.Text = "Pasos restantes: --";
        _stepCount = 0;
        StepCounterLabel.Text = "Pasos dados: 0";

        CheckAndAskAgeAsync();
    }

    private async void OnResetStepsClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("Confirmar", "¿Quieres reiniciar el conteo de pasos de hoy?", "Sí", "No");
        if (result)
        {
            var today = DateTime.Today;
            _stepCount = 0;
            _initialStepCount = -1;

            Preferences.Remove($"steps_{today:yyyy-MM-dd}");
            Preferences.Remove($"initial_steps_{today:yyyy-MM-dd}");

            StepCounterLabel.Text = "Pasos dados: 0";
            UpdateStepsRemaining();

#if ANDROID
            // Reiniciar el listener del sensor
            UnregisterSensorListener();
            await Task.Delay(100);
            RegisterSensorListener();
#endif
        }
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Historial", "Funcionalidad de historial no implementada aún.", "OK");
    }

    // Método para debug - puedes agregarlo temporalmente
    private async void OnTestSensorClicked(object sender, EventArgs e)
    {
#if ANDROID
        if (_sensorManager != null && _stepCounter != null)
        {
            bool hasPermission = CheckActivityRecognitionPermissionNative();

            await DisplayAlert("Info del Sensor",
                $"Sensor disponible: {_stepCounter.Name}\n" +
                $"Listener activo: {_isListenerActive}\n" +
                $"Pasos actuales: {_stepCount}\n" +
                $"Valor inicial: {_initialStepCount}\n" +
                $"Permiso concedido: {hasPermission}",
                "OK");
        }
        else
        {
            await DisplayAlert("Error", "Sensor no inicializado", "OK");
        }
#else
        await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
    }
}