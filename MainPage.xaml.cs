using Microsoft.Maui.Controls;
#if ANDROID
using Android.Hardware;
using Android.Content;
using SensorApp.Platforms.Android;
#endif

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

    public MainPage()
    {
        InitializeComponent();

#if ANDROID
        _sensorManager = (SensorManager)Android.App.Application.Context.GetSystemService(Context.SensorService);
        _accelerometer = _sensorManager.GetDefaultSensor(SensorType.Accelerometer);
        _gyroscope = _sensorManager.GetDefaultSensor(SensorType.Gyroscope);
        _light = _sensorManager.GetDefaultSensor(SensorType.Light);

        _listener = new SensorListener();
        _listener.OnSensorValueChanged += OnSensorChanged;
#endif
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

        Device.BeginInvokeOnMainThread(() =>
        {
            switch (e.Sensor.Type)
            {
                case SensorType.Accelerometer:
                    AccelerometerLabel.Text = $"X: {e.Values[0]:0.00}, Y: {e.Values[1]:0.00}, Z: {e.Values[2]:0.00}";
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

}
