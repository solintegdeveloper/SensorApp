using Microsoft.Maui.Controls;
#if ANDROID
using Android.Hardware;
using Android.Content;
using SensorApp.Platforms.Android;
#endif
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Linq;

namespace SensorApp;

public partial class MainPage : ContentPage
{
#if ANDROID
    SensorManager _sensorManager;
    Sensor _accelerometer;
    Sensor _gyroscope;
    Sensor _light;
    Sensor _stepCounter; // Nuevo sensor StepCounter
    Sensor _stepDetector; // Opcional: para detectar pasos individuales
    SensorListener _listener;
#endif

    private SensorDatabase _sensorDatabase = new SensorDatabase();

    private int _stepCount = 0;
    private int _initialStepCount = 0; // Para almacenar el conteo inicial del sensor
    private bool _isFirstStepReading = true; // Para identificar la primera lectura

    // Variables para detección de pasos con acelerómetro (respaldo)
    private float _lastAccelerationBackup = 9.8f;
    private long _lastStepTimeBackup = 0;

    public MainPage()
    {
        InitializeComponent();
        CheckAndAskAgeAsync();

#if ANDROID
        _sensorManager = (SensorManager)Android.App.Application.Context.GetSystemService(Context.SensorService);
        _accelerometer = _sensorManager.GetDefaultSensor(SensorType.Accelerometer);
        _gyroscope = _sensorManager.GetDefaultSensor(SensorType.Gyroscope);
        _light = _sensorManager.GetDefaultSensor(SensorType.Light);
        
        // Inicializar sensores de pasos
        _stepCounter = _sensorManager.GetDefaultSensor(SensorType.StepCounter);
        _stepDetector = _sensorManager.GetDefaultSensor(SensorType.StepDetector);

        _listener = new SensorListener();
        _listener.OnSensorValueChanged += OnSensorChanged;

        // Verificar disponibilidad de sensores de pasos
        CheckStepSensorAvailability();
#endif
    }

#if ANDROID
    private void CheckStepSensorAvailability()
    {
        if (_stepCounter == null && _stepDetector == null)
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Advertencia", 
                    "Tu dispositivo no tiene sensores de pasos disponibles. " +
                    "Se usará el acelerómetro como respaldo, pero puede ser menos preciso.", 
                    "OK");
            });
        }
        else if (_stepCounter == null)
        {
            System.Diagnostics.Debug.WriteLine("StepCounter no disponible, usando StepDetector");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("StepCounter disponible");
        }
    }
#endif

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

        // Cargar pasos guardados del día actual
        await LoadTodaySteps();
    }

    private async Task LoadTodaySteps()
    {
        // Cargar los pasos guardados del día actual
        var today = DateTime.Today;
        var savedSteps = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
        var savedInitialCount = Preferences.Get($"initial_steps_{today:yyyy-MM-dd}", -1);

        if (savedInitialCount != -1)
        {
            _initialStepCount = savedInitialCount;
            _stepCount = savedSteps;
            _isFirstStepReading = false;

            Device.BeginInvokeOnMainThread(() =>
            {
                StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
                UpdateStepsRemaining();
            });
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        // Registrar sensores con diferentes prioridades
        if (_stepCounter != null)
            _sensorManager.RegisterListener(_listener, _stepCounter, SensorDelay.Ui);
        
        if (_stepDetector != null)
            _sensorManager.RegisterListener(_listener, _stepDetector, SensorDelay.Ui);
            
        // Mantener otros sensores
        if (_accelerometer != null)
            _sensorManager.RegisterListener(_listener, _accelerometer, SensorDelay.Game);
        if (_gyroscope != null)
            _sensorManager.RegisterListener(_listener, _gyroscope, SensorDelay.Ui);
        if (_light != null)
            _sensorManager.RegisterListener(_listener, _light, SensorDelay.Ui);
#endif
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SensorDataPage());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        _sensorManager.UnregisterListener(_listener);
#endif
    }

#if ANDROID
    private async void OnSensorChanged(SensorEvent e)
    {
        var data = new SensorData
        {
            SensorType = e.Sensor.Type.ToString(),
            Value1 = e.Values.Count > 0 ? e.Values[0] : 0,
            Value2 = e.Values.Count > 1 ? e.Values[1] : 0,
            Value3 = e.Values.Count > 2 ? e.Values[2] : 0,
            Timestamp = DateTime.Now
        };

        await _sensorDatabase.SaveSensorDataAsync(data);

        Device.BeginInvokeOnMainThread(() =>
        {
            switch (e.Sensor.Type)
            {
                case SensorType.StepCounter:
                    ProcessStepCounter(e.Values[0]);
                    break;
                    
                case SensorType.StepDetector:
                    ProcessStepDetector();
                    break;
                    
                case SensorType.Accelerometer:
                    AccelerometerLabel.Text = $"X: {e.Values[0]:0.00}, Y: {e.Values[1]:0.00}, Z: {e.Values[2]:0.00}";
                    
                    // Solo usar acelerómetro si no hay sensores de pasos disponibles
                    if (_stepCounter == null && _stepDetector == null)
                    {
                        float x = e.Values[0];
                        float y = e.Values[1];
                        float z = e.Values[2];
                        float acceleration = (float)Math.Sqrt(x * x + y * y + z * z);
                        ProcessAccelerometerStepDetection(acceleration);
                    }
                    break;
                    
                case SensorType.Gyroscope:
                    GyroscopeLabel.Text = $"X: {e.Values[0]:0.00}, Y: {e.Values[1]:0.00}, Z: {e.Values[2]:0.00}";
                    break;
                    
                case SensorType.Light:
                    LightLabel.Text = $"{e.Values[0]:0.00} lux";
                    break;
            }
        });
    }

    private void ProcessStepCounter(float sensorStepCount)
    {
        var today = DateTime.Today;
        
        if (_isFirstStepReading)
        {
            // Primera lectura del día: establecer el conteo inicial
            _initialStepCount = (int)sensorStepCount;
            _isFirstStepReading = false;
            
            // Guardar el conteo inicial del día
            Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
            
            System.Diagnostics.Debug.WriteLine($"Conteo inicial del sensor: {_initialStepCount}");
        }
        
        // Calcular pasos del día actual
        int currentDaySteps = (int)sensorStepCount - _initialStepCount;
        
        // Asegurar que no sea negativo (en caso de reinicio del sensor)
        if (currentDaySteps < 0)
        {
            _initialStepCount = (int)sensorStepCount;
            currentDaySteps = 0;
            Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
        }
        
        _stepCount = currentDaySteps;
        
        // Guardar pasos del día actual
        Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);
        
        StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
        UpdateStepsRemaining();
        
        System.Diagnostics.Debug.WriteLine($"Pasos hoy: {_stepCount} (Sensor: {sensorStepCount}, Inicial: {_initialStepCount})");
    }

    private void ProcessStepDetector()
    {
        var today = DateTime.Today;
        
        // StepDetector se activa una vez por cada paso detectado
        _stepCount++;
        
        // Guardar pasos del día actual
        Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);
        
        StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
        UpdateStepsRemaining();
        
        System.Diagnostics.Debug.WriteLine($"Paso detectado por StepDetector. Total: {_stepCount}");
    }

    // Método de respaldo usando acelerómetro (código original simplificado)
    private void ProcessAccelerometerStepDetection(float currentAcceleration)
    {
        // Implementación simplificada del método original
        // Solo se usa si no hay sensores de pasos disponibles
        const float threshold = 1.5f;
        const int stepDelayMs = 250;
        
        float deviation = Math.Abs(currentAcceleration - 9.8f);
        
        if (deviation > threshold && currentAcceleration < _lastAccelerationBackup)
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            
            if (currentTime - _lastStepTimeBackup > stepDelayMs)
            {
                var today = DateTime.Today;
                _stepCount++;
                _lastStepTimeBackup = currentTime;
                
                Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);
                
                StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
                UpdateStepsRemaining();
                
                System.Diagnostics.Debug.WriteLine($"Paso detectado por acelerómetro (respaldo). Total: {_stepCount}");
            }
        }
        
        _lastAccelerationBackup = currentAcceleration;
    }
#endif

    private void OnResetAgeClicked(object sender, EventArgs e)
    {
        Preferences.Remove("user_age");
        Preferences.Remove("daily_steps_goal_text");
        Preferences.Remove("daily_steps_goal_value");

        StepsGoalLabel.Text = "Meta diaria: cargando...";
        StepsRemainingLabel.Text = "Pasos restantes: --";
        _stepCount = 0;
        StepCounterLabel.Text = "Pasos: 0";

        CheckAndAskAgeAsync();
    }

    private async void OnResetStepsClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("Confirmar",
            "¿Estás seguro de que quieres reiniciar el contador de pasos de hoy?",
            "Sí", "No");

        if (result)
        {
            var today = DateTime.Today;
            _stepCount = 0;
            _isFirstStepReading = true;

            // Limpiar preferencias del día actual
            Preferences.Remove($"steps_{today:yyyy-MM-dd}");
            Preferences.Remove($"initial_steps_{today:yyyy-MM-dd}");

            StepCounterLabel.Text = "Pasos dados: 0";
            UpdateStepsRemaining();

            System.Diagnostics.Debug.WriteLine("Contador de pasos reiniciado");
        }
    }

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
                CheckAndShowGoalReachedMessage(_stepCount, goal);
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

    private void CheckAndShowGoalReachedMessage(int steps, int goal)
    {
        if (steps >= goal)
        {
            DisplayAlert("¡Felicidades!", "Has alcanzado tu meta diaria de pasos.", "OK");
        }
    }
}