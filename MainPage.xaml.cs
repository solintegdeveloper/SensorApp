using Microsoft.Maui.Controls;
#if ANDROID
using Android.Hardware;
using Android.Content;
using SensorApp.Platforms.Android;
#endif
using Microsoft.Maui.Storage;

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
    private const float StepThreshold = 1.2f; // Umbral de aceleración para contar un paso
    private long _lastStepTime = 0;
    private const int StepDelayMs = 300; // mínimo 300 ms entre pasos para evitar falsos positivos


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
            string result = await DisplayPromptAsync("Edad", "Por favor, ingresa tu edad:", "Aceptar", "Cancelar", keyboard: Keyboard.Numeric);

            if (int.TryParse(result, out int age))
            {
                Preferences.Set("user_age", age);

                // Calcular meta de pasos
                string meta;
                if (age >= 6 && age <= 17)
                    meta = "12,000 – 15,000 pasos";
                else if (age >= 18 && age <= 64)
                    meta = "7,000 – 10,000 pasos";
                else if (age >= 65)
                    meta = "6,000 – 8,000 pasos";
                else
                    meta = "Edad no válida";

                Preferences.Set("daily_steps_goal", meta);
                StepsGoalLabel.Text = $"Meta diaria: {meta}";

                await DisplayAlert("Meta diaria", $"Tu meta diaria es: {meta}", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Edad inválida. Intenta nuevamente.", "OK");
                CheckAndAskAgeAsync(); // volver a intentar
            }
        }
        else
        {
            string storedMeta = Preferences.Get("daily_steps_goal", "Meta no definida");
            StepsGoalLabel.Text = $"Meta diaria: {storedMeta}";
        }
    }



    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (_accelerometer != null)
            _sensorManager.RegisterListener(_listener, _accelerometer, SensorDelay.Ui);
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
            //Timestamp = DateTime.UtcNow
            Timestamp = DateTime.Now
        };

        await _sensorDatabase.SaveSensorDataAsync(data);

        await _sensorDatabase.AddStepForTodayAsync();

        Device.BeginInvokeOnMainThread(() =>
        {
            switch (e.Sensor.Type)
            {
                case SensorType.Accelerometer:
                    AccelerometerLabel.Text = $"X: {e.Values[0]:0.00}, Y: {e.Values[1]:0.00}, Z: {e.Values[2]:0.00}";

                    float x = e.Values[0];
                    float y = e.Values[1];
                    float z = e.Values[2];

                    float acceleration = (float)Math.Sqrt(x * x + y * y + z * z);

                    long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (acceleration > StepThreshold && currentTime - _lastStepTime > StepDelayMs)
                    {
                        _stepCount++;
                        _lastStepTime = currentTime;
                        StepCounterLabel.Text = $"Pasos dados: {_stepCount}";
                        UpdateStepsRemaining();

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
#endif
    private void OnResetAgeClicked(object sender, EventArgs e)
    {
        Preferences.Remove("user_age");
        Preferences.Remove("daily_steps_goal");

        StepsGoalLabel.Text = "Meta diaria: cargando...";
        CheckAndAskAgeAsync();
    }

    private async void OnViewDailyStepsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DailyStepsPage());
    }

    private void UpdateStepsRemaining()
    {
        string goalText = Preferences.Get("daily_steps_goal", "");
        int maxGoal = 0;

        if (goalText.Contains("–"))
        {
            var parts = goalText.Split('–');
            if (int.TryParse(parts[1].Replace(" pasos", "").Replace(",", "").Trim(), out int parsedGoal))
            {
                maxGoal = parsedGoal;
            }
        }

        if (maxGoal > 0)
        {
            int remaining = Math.Max(0, maxGoal - _stepCount);
            StepsRemainingLabel.Text = $"Pasos restantes: {remaining}";

            if (remaining == 0)
            {
                StepsRemainingLabel.TextColor = Colors.Green;
            }
            else
            {
                StepsRemainingLabel.TextColor = Colors.DarkRed;
            }
        }
        else
        {
            StepsRemainingLabel.Text = "Pasos restantes: meta no definida";
            StepsRemainingLabel.TextColor = Colors.Gray;
        }
    }



}
