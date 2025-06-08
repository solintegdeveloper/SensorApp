#if ANDROID
using Android.Hardware;
using Java.Lang;
using System;
using SystemException = System.Exception;

namespace SensorApp
{
    public class SensorListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<SensorEvent> OnSensorValueChanged;

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            System.Diagnostics.Debug.WriteLine($"Precisión del sensor cambió: {sensor.Name} - {accuracy}");
        }

        public void OnSensorChanged(SensorEvent e)
        {
            try
            {
                if (e?.Values != null && e.Values.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Evento del sensor: {e.Sensor.Type} - Valor: {e.Values[0]}");
                    OnSensorValueChanged?.Invoke(e);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Evento del sensor sin valores válidos");
                }
            }
            catch (SystemException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en OnSensorChanged: {ex.Message}");
            }
        }
    }
}
#endif