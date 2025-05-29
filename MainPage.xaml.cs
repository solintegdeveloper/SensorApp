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
    SensorListener _listener;
#endif

    private SensorDatabase _sensorDatabase = new SensorDatabase();

    private int _stepCount = 0;

    // Parámetros simplificados para detección de pasos
    private const float StepThreshold = 1.5f; // Reducido para mayor sensibilidad
    private long _lastStepTime = 0;
    private const int StepDelayMs = 250; // Tiempo mínimo entre pasos

    // Variables para filtrado básico
    private Queue<float> _accelerationHistory = new Queue<float>();
    private const int HistorySize = 5; // Reducido para respuesta más rápida
    private float _baselineAcceleration = 9.8f;
    private bool _isCalibrating = true;
    private int _calibrationSamples = 0;
    private const int CalibrationSampleCount = 20; // Reducido para calibración más rápida

    // Detección de picos mejorada
    private float _lastAcceleration = 0;
    private bool _isPeakDetected = false;

    public MainPage()
    {
        InitializeComponent();
        CheckAndAskAgeAsync();

#if ANDROID
        _sensorManager = (SensorManager)Android.App.Application.Context.GetSystemService(Context.SensorService);
        _accelerometer = _sensorManager.GetDefaultSensor(SensorType.Accelerometer);
        _gyroscope = _sensorManager.GetDefaultSensor(SensorType.Gyroscope);
        _light = _sensorManager.GetDefaultSensor(SensorType.Light);

        _listener = new SensorListener();
        _listener.OnSensorValueChanged += OnSensorChanged;
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
                await Task.Delay(500); // Breve espera antes de volver a preguntar
                CheckAndAskAgeAsync();
            }
        }
        else
        {
            string savedGoalText = Preferences.Get("daily_steps_goal_text", "Meta no establecida");
            StepsGoalLabel.Text = $"Meta diaria: {savedGoalText}";
        }
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (_accelerometer != null)
            _sensorManager.RegisterListener(_listener, _accelerometer, SensorDelay.Game); // Cambio a Game para mayor frecuencia
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
                case SensorType.Accelerometer:
                    AccelerometerLabel.Text = $"X: {e.Values[0]:0.00}, Y: {e.Values[1]:0.00}, Z: {e.Values[2]:0.00}";

                    float x = e.Values[0];
                    float y = e.Values[1];
                    float z = e.Values[2];

                    // Calcular la magnitud de la aceleración
                    float acceleration = (float)Math.Sqrt(x * x + y * y + z * z);
                    
                    // Procesar la detección de pasos simplificada
                    ProcessStepDetection(acceleration);

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

    private void ProcessStepDetection(float currentAcceleration)
    {
        // Fase de calibración simplificada
        if (_isCalibrating)
        {
            _calibrationSamples++;
            _baselineAcceleration = (_baselineAcceleration * 0.9f) + (currentAcceleration * 0.1f);
            
            if (_calibrationSamples >= CalibrationSampleCount)
            {
                _isCalibrating = false;
                Device.BeginInvokeOnMainThread(() =>
                {
                    // Removido el DisplayAlert para evitar interrupciones
                    System.Diagnostics.Debug.WriteLine("Calibración completada");
                });
            }
            return;
        }

        // Mantener historial simple
        _accelerationHistory.Enqueue(currentAcceleration);
        if (_accelerationHistory.Count > HistorySize)
        {
            _accelerationHistory.Dequeue();
        }

        // Detección de picos simplificada
        if (_accelerationHistory.Count >= 3)
        {
            var recent = _accelerationHistory.ToArray();
            float current = recent[recent.Length - 1];
            float previous = recent[recent.Length - 2];
            float beforePrevious = recent[recent.Length - 3];

            // Detectar pico: el valor anterior es mayor que el actual y el anterior a ese
            bool isPeak = previous > current && previous > beforePrevious;
            
            if (isPeak)
            {
                float deviation = Math.Abs(previous - _baselineAcceleration);
                
                if (deviation > StepThreshold)
                {
                    long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    
                    if (currentTime - _lastStepTime > StepDelayMs)
                    {
                        _stepCount++;
                        _lastStepTime = currentTime;
                        
                        StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
                        UpdateStepsRemaining();
                        
                        // Debug para monitoreo
                        System.Diagnostics.Debug.WriteLine($"Paso detectado #{_stepCount}, Desviación: {deviation:F2}");
                    }
                }
            }
        }

        _lastAcceleration = currentAcceleration;
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

    private async void OnViewDailyStepsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DailyStepsPage());
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