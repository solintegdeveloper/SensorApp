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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        _sensorManager.UnregisterListener(_listener);
#endif
    }

#if ANDROID
    private void OnSensorChanged(SensorEvent e)
    {
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
