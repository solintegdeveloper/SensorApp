#if ANDROID
using Android.Hardware;
using Android.Runtime;
using System;

namespace SensorApp.Platforms.Android
{
    public class SensorListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<SensorEvent> OnSensorValueChanged;

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            // Puedes manejar cambios de precisión si deseas
        }

        public void OnSensorChanged(SensorEvent e)
        {
            OnSensorValueChanged?.Invoke(e);
        }
    }
}
#endif
