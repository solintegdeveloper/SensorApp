using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Collections.Generic;

#if ANDROID
using Android;
using AndroidX.Core.Content;
using Android.Content.PM;
using Android.Hardware;
using Android.Content;
using AndroidX.Core.App;
using Android.OS;
#endif

namespace SensorApp;

public partial class MainPage : ContentPage
{
    private DateTime _lastSensorUpdate = DateTime.MinValue;
    private DateTime _lastStepTime = DateTime.MinValue;

#if ANDROID
    SensorManager _sensorManager;
    Sensor _stepCounter;
    Sensor _stepDetector;
    SensorListener _listener;
    private bool _isListenerActive = false;
    private const int ACTIVITY_RECOGNITION_REQUEST_CODE = 1001;
    private bool _useStepDetector = false;
    
#endif

    private int _stepCount = 0;
    private int _initialStepCount = -1;
    private bool _isInitialized = false;

    public MainPage()
    {
        InitializeComponent();
        System.Diagnostics.Debug.WriteLine("MainPage constructor iniciado");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("OnAppearing llamado");

        if (!_isInitialized)
        {
            _isInitialized = true;
            await InitializePageAsync();
        }

#if ANDROID
        // Reinicializar sensor cuando la app vuelve al frente
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            if (!_isListenerActive)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RegisterSensorListener();
                });
            }
        });
#endif
    }

    protected override void OnDisappearing()
    {
        try
        {
            base.OnDisappearing();
            System.Diagnostics.Debug.WriteLine("OnDisappearing llamado");

            // Guardar estado actual
            var today = DateTime.Today;
            Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);
            if (_initialStepCount != -1)
            {
                Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnDisappearing: {ex.Message}");
        }
    }

    private async Task InitializePageAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Iniciando inicialización de la página");

            // Mostrar un mensaje de carga mientras se inicializa
            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = "Inicializando...";
            }

            await CheckAndAskAgeAsync();

#if ANDROID
            await Task.Run(() => InitializeAndroidSensors());
#endif

            System.Diagnostics.Debug.WriteLine("Inicialización de la página completada");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en InitializePageAsync: {ex.Message}");

            // Mostrar error al usuario
            await DisplayAlert("Error",
                $"Error inicializando la aplicación: {ex.Message}", "OK");
        }
    }

#if ANDROID
    private void InitializeAndroidSensors()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Inicializando sensores Android");

            var context = Platform.CurrentActivity?.BaseContext ?? Android.App.Application.Context;
            _sensorManager = (SensorManager)context.GetSystemService(Context.SensorService);

            if (_sensorManager == null)
            {
                System.Diagnostics.Debug.WriteLine("No se pudo obtener SensorManager");
                return;
            }

            _stepCounter = _sensorManager.GetDefaultSensor(SensorType.StepCounter);
            _stepDetector = _sensorManager.GetDefaultSensor(SensorType.StepDetector);

            if (_stepCounter != null)
            {
                System.Diagnostics.Debug.WriteLine($"StepCounter disponible: {_stepCounter.Name}");
                _useStepDetector = false;
            }
            else if (_stepDetector != null)
            {
                System.Diagnostics.Debug.WriteLine($"Solo StepDetector disponible: {_stepDetector.Name}");
                _useStepDetector = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Ningún sensor de pasos disponible");
                CheckSensorAvailability();
                return;
            }

            _listener = new SensorListener();
            _listener.SensorChanged += OnSensorChanged;

            DiagnoseSensors();

            // Inicializar el sensor de forma asíncrona
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Esperar más tiempo
                await InitializeSensorAsync();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error inicializando sensores Android: {ex.Message}");
        }
    }

    private void DiagnoseSensors()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== DIAGNÓSTICO DE SENSORES ===");

            var stepCounters = _sensorManager.GetSensorList(SensorType.StepCounter);
            var stepDetectors = _sensorManager.GetSensorList(SensorType.StepDetector);

            System.Diagnostics.Debug.WriteLine($"StepCounters encontrados: {stepCounters.Count}");
            foreach (var sensor in stepCounters)
            {
                System.Diagnostics.Debug.WriteLine($"  - {sensor.Name} ({sensor.Vendor}) - Potencia: {sensor.Power}mA");
            }

            System.Diagnostics.Debug.WriteLine($"StepDetectors encontrados: {stepDetectors.Count}");
            foreach (var sensor in stepDetectors)
            {
                System.Diagnostics.Debug.WriteLine($"  - {sensor.Name} ({sensor.Vendor}) - Potencia: {sensor.Power}mA");
            }

            System.Diagnostics.Debug.WriteLine($"Versión Android: API {(int)Build.VERSION.SdkInt}");
            System.Diagnostics.Debug.WriteLine($"Requiere ACTIVITY_RECOGNITION: {Build.VERSION.SdkInt >= BuildVersionCodes.Q}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en diagnóstico: {ex.Message}");
        }
    }

    private async Task InitializeSensorAsync()
    {
        try
        {
            bool hasPermission = await CheckAndRequestActivityRecognitionPermission();
            System.Diagnostics.Debug.WriteLine($"Permiso obtenido: {hasPermission}");

            if (hasPermission)
            {
                await Task.Delay(500);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RegisterSensorListener();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Permiso denegado para sensores");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Permisos requeridos",
                        "La aplicación necesita permiso para detectar actividad física para contar pasos. " +
                        "Por favor, activa el permiso en la configuración de la aplicación.", "OK");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en InitializeSensorAsync: {ex.Message}");
        }
    }

    private async Task<bool> CheckAndRequestActivityRecognitionPermission()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var currentPermission = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                if (currentPermission == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Permiso ACTIVITY_RECOGNITION ya concedido");
                    return true;
                }

                if (Platform.CurrentActivity is AndroidX.AppCompat.App.AppCompatActivity activity)
                {
                    System.Diagnostics.Debug.WriteLine("Solicitando permiso ACTIVITY_RECOGNITION...");

                    ActivityCompat.RequestPermissions(activity,
                        new string[] { Android.Manifest.Permission.ActivityRecognition },
                        ACTIVITY_RECOGNITION_REQUEST_CODE);

                    for (int i = 0; i < 10; i++)
                    {
                        await Task.Delay(500);
                        var newStatus = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                        if (newStatus == Permission.Granted)
                        {
                            System.Diagnostics.Debug.WriteLine($"Permiso concedido después de {i * 500}ms");
                            return true;
                        }
                    }

                    var finalStatus = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                    bool granted = finalStatus == Permission.Granted;
                    System.Diagnostics.Debug.WriteLine($"Estado final del permiso: {granted}");
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
                System.Diagnostics.Debug.WriteLine("Android < 10, no se requiere ACTIVITY_RECOGNITION");
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando permisos: {ex.Message}");
            return false;
        }
    }

    // ====== MÉTODO MEJORADO PARA REGISTRO DE SENSOR ======
    private void RegisterSensorListener()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== INICIANDO REGISTRO DE SENSOR MEJORADO ===");

            if (_isListenerActive)
            {
                System.Diagnostics.Debug.WriteLine("Listener ya está activo, desregistrando primero");
                UnregisterSensorListener();
                // REMOVIDO: await Task.Delay(1000); 
                // CAMBIADO POR:
                Task.Delay(1000).Wait(); // Esperar sincrónicamente
            }

            Sensor sensorToUse = _useStepDetector ? _stepDetector : _stepCounter;

            if (sensorToUse != null && _listener != null)
            {
                System.Diagnostics.Debug.WriteLine($"Registrando sensor: {sensorToUse.Name}");
                System.Diagnostics.Debug.WriteLine($"Tipo de sensor: {sensorToUse.Type}");
                System.Diagnostics.Debug.WriteLine($"Usando StepDetector: {_useStepDetector}");

                // CAMBIO IMPORTANTE: Usar diferentes delays según el sensor
                SensorDelay delay;
                if (_useStepDetector)
                {
                    delay = SensorDelay.Fastest; // Más sensible para StepDetector
                }
                else
                {
                    delay = SensorDelay.Normal; // Normal para StepCounter
                }

                bool registered = _sensorManager.RegisterListener(_listener, sensorToUse, delay);

                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("Falló el registro, intentando con delay UI");
                    delay = SensorDelay.Ui;
                    registered = _sensorManager.RegisterListener(_listener, sensorToUse, delay);
                }

                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("Falló el registro con UI, intentando con Game");
                    delay = SensorDelay.Game;
                    registered = _sensorManager.RegisterListener(_listener, sensorToUse, delay);
                }

                _isListenerActive = registered;
                System.Diagnostics.Debug.WriteLine($"*** RESULTADO REGISTRO: {registered} con delay {delay} ***");

                if (registered)
                {
                    System.Diagnostics.Debug.WriteLine("*** SENSOR REGISTRADO EXITOSAMENTE ***");
                    _lastSensorUpdate = DateTime.Now;

                    if (_useStepDetector && _initialStepCount == -1)
                    {
                        _initialStepCount = 0;
                        var today = DateTime.Today;
                        Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
                    }

                    // NUEVO: Forzar una lectura inicial (SIN AWAIT)
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        System.Diagnostics.Debug.WriteLine("Esperando eventos del sensor...");
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("*** FALLÓ EL REGISTRO DEL LISTENER COMPLETAMENTE ***");
                    TryAlternativeSensorRegistration();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No hay sensor disponible para registrar");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"*** ERROR REGISTRANDO LISTENER: {ex.Message} ***");
        }
    }


    private void TryAlternativeSensorRegistration()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Intentando registro alternativo...");

            if (!_useStepDetector && _stepDetector != null)
            {
                System.Diagnostics.Debug.WriteLine("Cambiando a StepDetector");
                _useStepDetector = true;
                bool registered = _sensorManager.RegisterListener(_listener, _stepDetector, SensorDelay.Ui);
                _isListenerActive = registered;
                System.Diagnostics.Debug.WriteLine($"StepDetector registrado: {registered}");

                if (registered)
                {
                    _initialStepCount = 0;
                    var today = DateTime.Today;
                    Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
                }
            }
            else if (_useStepDetector && _stepCounter != null)
            {
                System.Diagnostics.Debug.WriteLine("Cambiando a StepCounter");
                _useStepDetector = false;
                bool registered = _sensorManager.RegisterListener(_listener, _stepCounter, SensorDelay.Normal);
                _isListenerActive = registered;
                System.Diagnostics.Debug.WriteLine($"StepCounter registrado: {registered}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en registro alternativo: {ex.Message}");
        }
    }

    private void UnregisterSensorListener()
    {
        try
        {
            if (_listener != null && _isListenerActive)
            {
                _sensorManager?.UnregisterListener(_listener);
                _isListenerActive = false;
                System.Diagnostics.Debug.WriteLine("Listener desregistrado");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error desregistrando listener: {ex.Message}");
        }
    }

    // Clase para el listener del sensor
    private class SensorListener : Java.Lang.Object, ISensorEventListener
    {
        public event Action<SensorEvent> SensorChanged;

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            System.Diagnostics.Debug.WriteLine($"Precisión del sensor cambiada: {accuracy}");
        }

        public void OnSensorChanged(SensorEvent e)
        {
            try
            {
                SensorChanged?.Invoke(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en OnSensorChanged: {ex.Message}");
            }
        }
    }

    // ====== MÉTODO MEJORADO PARA MANEJAR EVENTOS DE SENSORES ======
    private void OnSensorChanged(SensorEvent e)
    {
        try
        {
            var now = DateTime.Now;
            var timeSinceLastUpdate = now - _lastSensorUpdate;

            System.Diagnostics.Debug.WriteLine($"*** EVENTO SENSOR RECIBIDO ({now:HH:mm:ss.fff}) ***");
            System.Diagnostics.Debug.WriteLine($"Tiempo desde última actualización: {timeSinceLastUpdate.TotalMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"Tipo de sensor: {e.Sensor.Type}");
            System.Diagnostics.Debug.WriteLine($"Nombre del sensor: {e.Sensor.Name}");

            _lastSensorUpdate = now;

            if (e.Values != null && e.Values.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Valor del sensor: {e.Values[0]}");

                // CAMBIO IMPORTANTE: Procesar CUALQUIER evento de sensor
                if (e.Sensor.Type == SensorType.StepCounter)
                {
                    System.Diagnostics.Debug.WriteLine(">>> Procesando como StepCounter");
                    ProcessStepCounterEvent(e);
                }
                else if (e.Sensor.Type == SensorType.StepDetector)
                {
                    System.Diagnostics.Debug.WriteLine(">>> Procesando como StepDetector");
                    ProcessStepDetectorEvent(e);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($">>> Sensor desconocido: {e.Sensor.Type}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("*** WARNING: Evento sin valores ***");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"*** ERROR en OnSensorChanged: {ex.Message} ***");
        }
    }


    // ====== MÉTODO MEJORADO PARA PROCESAR EVENTOS DE STEP COUNTER ======
    private void ProcessStepCounterEvent(SensorEvent e)
    {
        try
        {
            int sensorValue = (int)e.Values[0];
            var today = DateTime.Today;

            System.Diagnostics.Debug.WriteLine($"=== PROCESANDO STEP COUNTER ===");
            System.Diagnostics.Debug.WriteLine($"Valor del sensor: {sensorValue}");
            System.Diagnostics.Debug.WriteLine($"Valor inicial actual: {_initialStepCount}");

            if (_initialStepCount == -1)
            {
                _initialStepCount = sensorValue;
                Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
                System.Diagnostics.Debug.WriteLine($"Valor inicial establecido: {_initialStepCount}");
                return;
            }

            int rawDaySteps = sensorValue - _initialStepCount;
            System.Diagnostics.Debug.WriteLine($"Pasos brutos: {sensorValue} - {_initialStepCount} = {rawDaySteps}");

            // Manejar reset del sensor
            if (rawDaySteps < 0)
            {
                System.Diagnostics.Debug.WriteLine("Reset del sensor detectado, reajustando...");
                _initialStepCount = sensorValue - _stepCount;
                rawDaySteps = _stepCount;
                Preferences.Set($"initial_steps_{today:yyyy-MM-dd}", _initialStepCount);
            }

            // VALIDACIÓN MÁS PERMISIVA PARA STEP COUNTER
            int validatedSteps = ValidateStepCountForStepCounter(rawDaySteps);

            // Actualizar CUALQUIER cambio positivo (incluso +1)
            if (validatedSteps > _stepCount)
            {
                int previousSteps = _stepCount;
                _stepCount = validatedSteps;
                Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

                System.Diagnostics.Debug.WriteLine($"*** PASOS ACTUALIZADOS: {previousSteps} → {_stepCount} ***");
                UpdateUI();
            }
            else if (validatedSteps != _stepCount)
            {
                System.Diagnostics.Debug.WriteLine($"Cambio ignorado: {_stepCount} → {validatedSteps}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error procesando StepCounter: {ex.Message}");
        }
    }

    // ====== NUEVA FUNCIÓN DE VALIDACIÓN ESPECÍFICA PARA STEP COUNTER ======
    private int ValidateStepCountForStepCounter(int newCount)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== VALIDANDO STEP COUNTER ===");
            System.Diagnostics.Debug.WriteLine($"Anterior: {_stepCount}, Nuevo: {newCount}, Diferencia: {newCount - _stepCount}");

            // Para StepCounter, ser mucho más permisivo
            var increment = newCount - _stepCount;

            // Solo rechazar si hay un reset obvio
            if (newCount < _stepCount && _stepCount > 20)
            {
                System.Diagnostics.Debug.WriteLine($"Reset detectado, manteniendo: {_stepCount}");
                return _stepCount;
            }

            // Permitir incrementos más grandes para StepCounter
            if (increment > 50) // Muy generoso
            {
                System.Diagnostics.Debug.WriteLine($"Incremento muy grande: {increment}, limitando a 10");
                return _stepCount + 10;
            }

            // Aceptar cualquier incremento positivo
            if (increment >= 0)
            {
                System.Diagnostics.Debug.WriteLine($"Incremento aceptado: {increment}");
                return newCount;
            }

            // Para decrementos pequeños, mantener el valor actual
            if (increment > -5)
            {
                System.Diagnostics.Debug.WriteLine($"Decremento pequeño ignorado: {increment}");
                return _stepCount;
            }

            System.Diagnostics.Debug.WriteLine($"Usando nuevo valor: {newCount}");
            return newCount;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error validando StepCounter: {ex.Message}");
            return _stepCount;
        }
    }


    // Método para verificar estado de los Labels
    private async void OnVerifyLabelsClicked(object sender, EventArgs e)
    {
        try
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== ESTADO DE CONTROLES ===\n");

            status.AppendLine($"StepCounterLabel: {(StepCounterLabel != null ? "✓ Conectado" : "✗ NULL")}");
            if (StepCounterLabel != null)
            {
                status.AppendLine($"  Texto: '{StepCounterLabel.Text}'");
            }

            status.AppendLine($"StepsGoalLabel: {(StepsGoalLabel != null ? "✓ Conectado" : "✗ NULL")}");
            status.AppendLine($"StepsRemainingLabel: {(StepsRemainingLabel != null ? "✓ Conectado" : "✗ NULL")}");

            status.AppendLine($"\nPasos en memoria: {_stepCount}");
            status.AppendLine($"Valor inicial: {_initialStepCount}");

            await DisplayAlert("Estado de Controles", status.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnVerifyLabelsClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al verificar controles", "OK");
        }
    }

    // Método para probar actualización del UI
    private async void OnTestUIUpdateClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("*** PROBANDO ACTUALIZACIÓN DE UI ***");

            int oldSteps = _stepCount;
            _stepCount += 3;

            var today = DateTime.Today;
            Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

            // Actualizar UI directamente
            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = $"Pasos dados: {FormatStepCount(_stepCount)}";
            }
            UpdateStepsRemaining();

            await DisplayAlert("Test UI",
                $"Pasos incrementados en 3\n" +
                $"Anterior: {oldSteps}\n" +
                $"Actual: {_stepCount}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnTestUIUpdateClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en test", "OK");
        }
    }

    // Método para monitorear el sensor
    private async void OnMonitorSensorClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== MONITOR DEL SENSOR ===\n");

            status.AppendLine($"Sensor activo: {(_isListenerActive ? "✓ Sí" : "✗ No")}");
            status.AppendLine($"Tipo de sensor: {(_useStepDetector ? "Step Detector" : "Step Counter")}");

            var timeSinceUpdate = DateTime.Now - _lastSensorUpdate;
            status.AppendLine($"Última actualización: {timeSinceUpdate.TotalSeconds:F1}s atrás");

            status.AppendLine($"Pasos actuales: {_stepCount}");
            status.AppendLine($"Valor inicial: {_initialStepCount}");

            if (timeSinceUpdate.TotalMinutes > 5)
            {
                status.AppendLine("\n⚠️ El sensor parece inactivo");
                status.AppendLine("Intenta caminar un poco y vuelve a verificar");
            }
            else
            {
                status.AppendLine("\n✓ El sensor está funcionando");
            }

            await DisplayAlert("Monitor Sensor", status.ToString(), "OK");
#else
            await DisplayAlert("Info", "Solo disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnMonitorSensorClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en monitor", "OK");
        }
    }

    // Método simple para refrescar UI manualmente
    private async void OnForceRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("*** REFRESCANDO UI MANUALMENTE ***");

            // Recargar datos guardados
            LoadSavedSteps();

            // Actualizar UI
            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = $"Pasos dados: {FormatStepCount(_stepCount)}";
            }
            UpdateStepsRemaining();

            await DisplayAlert("UI Actualizado",
                $"Pasos: {FormatStepCount(_stepCount)}\n" +
                $"Inicial: {_initialStepCount}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnForceRefreshClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al refrescar", "OK");
        }
    }

    // ====== MÉTODO MEJORADO PARA PROCESAR EVENTOS DE STEP DETECTOR ======
    private void ProcessStepDetectorEvent(SensorEvent e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== PROCESANDO STEP DETECTOR ===");

            // Filtro de tiempo para evitar eventos duplicados
            var now = DateTime.Now;
            var timeSinceLastStep = now - _lastStepTime;

            // Ignorar eventos muy frecuentes (menos de 200ms entre pasos)
            if (timeSinceLastStep.TotalMilliseconds < 200)
            {
                System.Diagnostics.Debug.WriteLine($"Evento ignorado - muy frecuente: {timeSinceLastStep.TotalMilliseconds}ms");
                return;
            }

            _lastStepTime = now;
            System.Diagnostics.Debug.WriteLine($"Paso detectado - tiempo desde último: {timeSinceLastStep.TotalMilliseconds}ms");

            var today = DateTime.Today;
            int previousSteps = _stepCount;
            _stepCount++;
            Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

            System.Diagnostics.Debug.WriteLine($"*** PASO DETECTADO: {previousSteps} → {_stepCount} ***");
            UpdateUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error procesando StepDetector: {ex.Message}");
        }
    }


    // ====== MÉTODO ACTUALIZADO PARA UpdateUI (SIN ERRORES) ======
    private void UpdateUI()
    {
        try
        {
            // Asegurar que se ejecute en el hilo principal
            if (MainThread.IsMainThread)
            {
                UpdateUIInternal();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateUIInternal();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en UpdateUI: {ex.Message}");
        }
    }

    private void UpdateUIInternal()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"*** ACTUALIZANDO UI - Pasos: {_stepCount} ***");

            // Actualizar label de pasos
            if (StepCounterLabel != null)
            {
                var formattedSteps = FormatStepCount(_stepCount);
                StepCounterLabel.Text = $"Pasos dados: {formattedSteps}";
                System.Diagnostics.Debug.WriteLine($"Label actualizado: {StepCounterLabel.Text}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("*** WARNING: StepCounterLabel es NULL ***");
            }

            // Actualizar pasos restantes
            UpdateStepsRemaining();

            System.Diagnostics.Debug.WriteLine($"*** UI ACTUALIZADA CORRECTAMENTE ***");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en UpdateUIInternal: {ex.Message}");
        }
    }

    
    // Método para verificar si los sensores están disponibles
    private void CheckSensorAvailability()
    {
        try
        {
            if (_sensorManager == null)
            {
                System.Diagnostics.Debug.WriteLine("SensorManager no disponible");
                return;
            }

            var stepCounters = _sensorManager.GetSensorList(SensorType.StepCounter);
            var stepDetectors = _sensorManager.GetSensorList(SensorType.StepDetector);

            System.Diagnostics.Debug.WriteLine($"Sensores StepCounter disponibles: {stepCounters?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Sensores StepDetector disponibles: {stepDetectors?.Count ?? 0}");

            if (stepCounters?.Count == 0 && stepDetectors?.Count == 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Sensor no disponible",
                        "Tu dispositivo no tiene sensores de pasos compatibles.", "OK");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando disponibilidad de sensores: {ex.Message}");
        }
    }

    // Método para reiniciar el sensor cuando sea necesario
    private async Task RestartSensorAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Reiniciando sensor...");

            UnregisterSensorListener();
            await Task.Delay(1000); // Esperar un segundo
            RegisterSensorListener();

            System.Diagnostics.Debug.WriteLine("Sensor reiniciado");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reiniciando sensor: {ex.Message}");
        }
    }

    // Método para validar y corregir lecturas del sensor
    private int ValidateStepCount(int newCount)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== VALIDANDO CONTEO ===");
            System.Diagnostics.Debug.WriteLine($"Anterior: {_stepCount}, Nuevo: {newCount}, Diferencia: {newCount - _stepCount}");

            // Si el nuevo conteo es menor que el actual, podría ser un reset del sensor
            if (newCount < _stepCount && _stepCount > 50) // Reducido de 100 a 50
            {
                System.Diagnostics.Debug.WriteLine($"Reset del sensor detectado. Manteniendo: {_stepCount}");
                return _stepCount;
            }

            // Si el incremento es muy grande, limitarlo
            var increment = newCount - _stepCount;
            if (increment > 20) // Reducido de 100 a 20 para ser más permisivo
            {
                System.Diagnostics.Debug.WriteLine($"Incremento muy grande: {increment}, limitando a 5");
                return _stepCount + 5; // Incrementar gradualmente
            }

            // Si el incremento es negativo pero pequeño, ignorar
            if (increment < 0 && Math.Abs(increment) < 10)
            {
                System.Diagnostics.Debug.WriteLine($"Fluctuación menor ignorada: {increment}");
                return _stepCount; // Mantener el valor actual
            }

            System.Diagnostics.Debug.WriteLine($"Conteo válido: {newCount}");
            return newCount;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error validando conteo: {ex.Message}");
            return _stepCount;
        }
    }

#endif

    private async Task CheckAndAskAgeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Verificando edad del usuario");

            if (!Preferences.ContainsKey("user_age"))
            {
                System.Diagnostics.Debug.WriteLine("Edad no encontrada, solicitando al usuario");

                string ageInput = await DisplayPromptAsync("Edad", "Por favor, ingresa tu edad:");

                if (string.IsNullOrEmpty(ageInput))
                {
                    System.Diagnostics.Debug.WriteLine("Usuario canceló el diálogo de edad");
                    SetDefaultGoal();
                    return;
                }

                if (int.TryParse(ageInput, out int age) && age > 0 && age < 150)
                {
                    Preferences.Set("user_age", age);
                    SetGoalForAge(age);
                }
                else
                {
                    await DisplayAlert("Error", "Edad no válida. Se usará una meta por defecto.", "OK");
                    SetDefaultGoal();
                }
            }
            else
            {
                string savedGoalText = Preferences.Get("daily_steps_goal_text", "Meta no establecida");
                if (StepsGoalLabel != null)
                {
                    StepsGoalLabel.Text = $"Meta diaria: {savedGoalText}";
                }
            }

            LoadSavedSteps();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en CheckAndAskAgeAsync: {ex.Message}");
            SetDefaultGoal();
            LoadSavedSteps();
        }
    }

    private void SetGoalForAge(int age)
    {
        try
        {
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
                goal = 8000;
                meta = "8,000 pasos (por defecto)";
            }

            Preferences.Set("daily_steps_goal_text", meta);
            Preferences.Set("daily_steps_goal_value", goal);

            if (StepsGoalLabel != null)
            {
                StepsGoalLabel.Text = $"Meta diaria: {meta}";
            }

            System.Diagnostics.Debug.WriteLine($"Meta establecida: {meta}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error estableciendo meta por edad: {ex.Message}");
            SetDefaultGoal();
        }
    }

    private void SetDefaultGoal()
    {
        try
        {
            string meta = "8,000 pasos (por defecto)";
            int goal = 8000;

            Preferences.Set("daily_steps_goal_text", meta);
            Preferences.Set("daily_steps_goal_value", goal);

            if (StepsGoalLabel != null)
            {
                StepsGoalLabel.Text = $"Meta diaria: {meta}";
            }

            System.Diagnostics.Debug.WriteLine("Meta por defecto establecida");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error estableciendo meta por defecto: {ex.Message}");
        }
    }

    private void LoadSavedSteps()
    {
        try
        {
            var today = DateTime.Today;
            _stepCount = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
            _initialStepCount = Preferences.Get($"initial_steps_{today:yyyy-MM-dd}", -1);

            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = $"Pasos dados: {FormatStepCount(_stepCount)}";
            }

            UpdateStepsRemaining();
            ValidateDataIntegrity();

            System.Diagnostics.Debug.WriteLine($"Pasos cargados: {_stepCount}, Inicial: {_initialStepCount}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando pasos guardados: {ex.Message}");

            _stepCount = 0;
            _initialStepCount = -1;

            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = "Pasos dados: 0";
            }
        }
    }

    private void UpdateStepsRemaining()
    {
        try
        {
            if (StepsRemainingLabel == null) return;

            int goal = Preferences.Get("daily_steps_goal_value", 8000);
            int remaining = Math.Max(0, goal - _stepCount);

            StepsRemainingLabel.Text = $"Pasos restantes: {remaining:N0}";

            if (remaining == 0)
            {
                StepsRemainingLabel.TextColor = Colors.Green;
            }
            else
            {
                StepsRemainingLabel.TextColor = Colors.DarkRed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error actualizando pasos restantes: {ex.Message}");
        }
    }

    // Método para limpiar datos antiguos (llamar periódicamente)
    private void CleanOldData()
    {
        try
        {
            var today = DateTime.Today;
            var keysToRemove = new List<string>();

            // Buscar claves de días anteriores para eliminar datos antiguos
            for (int i = 1; i <= 30; i++) // Limpiar datos de los últimos 30 días excepto hoy
            {
                var oldDate = today.AddDays(-i);
                var stepKey = $"steps_{oldDate:yyyy-MM-dd}";
                var initialKey = $"initial_steps_{oldDate:yyyy-MM-dd}";

                if (Preferences.ContainsKey(stepKey))
                {
                    keysToRemove.Add(stepKey);
                }
                if (Preferences.ContainsKey(initialKey))
                {
                    keysToRemove.Add(initialKey);
                }
            }

            foreach (var key in keysToRemove)
            {
                Preferences.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Limpiados {keysToRemove.Count} registros antiguos");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error limpiando datos antiguos: {ex.Message}");
        }
    }

    // Método para obtener estadísticas del día
    private void GetDailyStats()
    {
        try
        {
            var today = DateTime.Today;
            int todaySteps = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
            int goal = Preferences.Get("daily_steps_goal_value", 8000);

            double progressPercentage = goal > 0 ? (double)todaySteps / goal * 100 : 0;

            System.Diagnostics.Debug.WriteLine($"=== ESTADÍSTICAS DIARIAS ===");
            System.Diagnostics.Debug.WriteLine($"Pasos hoy: {todaySteps}");
            System.Diagnostics.Debug.WriteLine($"Meta: {goal}");
            System.Diagnostics.Debug.WriteLine($"Progreso: {progressPercentage:F1}%");
            System.Diagnostics.Debug.WriteLine($"Restantes: {Math.Max(0, goal - todaySteps)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error obteniendo estadísticas: {ex.Message}");
        }
    }

    // Método para formatear números grandes
    private string FormatStepCount(int steps)
    {
        try
        {
            if (steps >= 1000000)
            {
                return $"{steps / 1000000.0:F1}M";
            }
            else if (steps >= 1000)
            {
                return $"{steps / 1000.0:F1}K";
            }
            else
            {
                return steps.ToString("N0");
            }
        }
        catch
        {
            return steps.ToString();
        }
    }

    // Método para calcular calorías estimadas (aproximado)
    private int CalculateEstimatedCalories(int steps)
    {
        try
        {
            // Fórmula aproximada: 0.04 calorías por paso para una persona promedio
            return (int)(steps * 0.04);
        }
        catch
        {
            return 0;
        }
    }

    // Método para calcular distancia estimada (aproximado)
    private double CalculateEstimatedDistance(int steps)
    {
        try
        {
            // Fórmula aproximada: 0.762 metros por paso para una persona promedio
            return steps * 0.762 / 1000; // Convertir a kilómetros
        }
        catch
        {
            return 0;
        }
    }

    // Método para validar la integridad de los datos
    private void ValidateDataIntegrity()
    {
        try
        {
            var today = DateTime.Today;
            int savedSteps = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
            int savedInitial = Preferences.Get($"initial_steps_{today:yyyy-MM-dd}", -1);

            System.Diagnostics.Debug.WriteLine($"=== VALIDACIÓN DE DATOS ===");
            System.Diagnostics.Debug.WriteLine($"Pasos guardados: {savedSteps}");
            System.Diagnostics.Debug.WriteLine($"Valor inicial guardado: {savedInitial}");
            System.Diagnostics.Debug.WriteLine($"Pasos en memoria: {_stepCount}");
            System.Diagnostics.Debug.WriteLine($"Inicial en memoria: {_initialStepCount}");

            // Sincronizar si hay diferencias
            if (savedSteps != _stepCount)
            {
                System.Diagnostics.Debug.WriteLine("Sincronizando pasos desde almacenamiento");
                _stepCount = savedSteps;
            }

            if (savedInitial != _initialStepCount && savedInitial != -1)
            {
                System.Diagnostics.Debug.WriteLine("Sincronizando valor inicial desde almacenamiento");
                _initialStepCount = savedInitial;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error validando integridad de datos: {ex.Message}");
        }
    }

    private bool CheckActivityRecognitionPermissionNative()
    {
        try
        {
#if ANDROID
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var permission = ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.ActivityRecognition);
                bool granted = permission == Permission.Granted;
                return granted;
            }

            return true;
#else
            return false;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando permiso nativo: {ex.Message}");
            return false;
        }
    }

    // Event Handlers
    private async void OnResetAgeClicked(object sender, EventArgs e)
    {
        try
        {
            Preferences.Remove("user_age");
            Preferences.Remove("daily_steps_goal_text");
            Preferences.Remove("daily_steps_goal_value");

            if (StepsGoalLabel != null)
            {
                StepsGoalLabel.Text = "Meta diaria: cargando...";
            }

            if (StepsRemainingLabel != null)
            {
                StepsRemainingLabel.Text = "Pasos restantes: --";
            }

            _stepCount = 0;

            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = "Pasos dados: 0";
            }

            await CheckAndAskAgeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnResetAgeClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al resetear la edad", "OK");
        }
    }

    private async void OnResetStepsClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("Confirmar", "¿Quieres reiniciar el conteo de pasos de hoy?", "Sí", "No");
            if (result)
            {
                var today = DateTime.Today;
                _stepCount = 0;
                _initialStepCount = -1;

                Preferences.Remove($"steps_{today:yyyy-MM-dd}");
                Preferences.Remove($"initial_steps_{today:yyyy-MM-dd}");

                if (StepCounterLabel != null)
                {
                    StepCounterLabel.Text = "Pasos dados: 0";
                }

                UpdateStepsRemaining();

#if ANDROID
                await RestartSensorAsync();
#endif
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnResetStepsClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al resetear los pasos", "OK");
        }
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        try
        {
            var history = new System.Text.StringBuilder();
            history.AppendLine("=== HISTORIAL DE PASOS (ÚLTIMOS 7 DÍAS) ===\n");

            var today = DateTime.Today;
            int totalWeekSteps = 0;

            for (int i = 0; i < 7; i++)
            {
                var date = today.AddDays(-i);
                var steps = Preferences.Get($"steps_{date:yyyy-MM-dd}", 0);
                totalWeekSteps += steps;

                string dayName = date.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
                history.AppendLine($"{dayName} ({date:dd/MM}): {FormatStepCount(steps)} pasos");
            }

            history.AppendLine($"\nTotal semanal: {FormatStepCount(totalWeekSteps)} pasos");
            history.AppendLine($"Promedio diario: {FormatStepCount(totalWeekSteps / 7)} pasos");

            // Calcular estadísticas adicionales
            var calories = CalculateEstimatedCalories(totalWeekSteps);
            var distance = CalculateEstimatedDistance(totalWeekSteps);

            history.AppendLine($"\nCalorías estimadas: {calories} cal");
            history.AppendLine($"Distancia estimada: {distance:F2} km");

            await DisplayAlert("Historial de Pasos", history.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnViewHistoryClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al mostrar el historial", "OK");
        }
    }

    private async void OnTestSensorClicked(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== ESTADO DETALLADO DEL SENSOR ===\n");

            if (_sensorManager != null)
            {
                status.AppendLine($"SensorManager: ✓ OK");
                status.AppendLine($"StepCounter disponible: {(_stepCounter != null ? "✓ Sí" : "✗ No")}");
                status.AppendLine($"StepDetector disponible: {(_stepDetector != null ? "✓ Sí" : "✗ No")}");
                status.AppendLine($"Usando StepDetector: {(_useStepDetector ? "Sí" : "No")}");
                status.AppendLine($"Listener activo: {(_isListenerActive ? "✓ Sí" : "✗ No")}");

                var currentSensor = _useStepDetector ? _stepDetector : _stepCounter;
                if (currentSensor != null)
                {
                    status.AppendLine($"\nSensor actual: {currentSensor.Name}");
                    status.AppendLine($"Fabricante: {currentSensor.Vendor}");
                    status.AppendLine($"Consumo: {currentSensor.Power} mA");
                    status.AppendLine($"Resolución: {currentSensor.Resolution}");
                }

                status.AppendLine($"\nPermiso ACTIVITY_RECOGNITION: {(CheckActivityRecognitionPermissionNative() ? "✓ Concedido" : "✗ Denegado")}");
                status.AppendLine($"Versión Android: API {(int)Build.VERSION.SdkInt}");

                status.AppendLine($"\n--- DATOS ACTUALES ---");
                status.AppendLine($"Pasos hoy: {_stepCount}");
                status.AppendLine($"Valor inicial: {_initialStepCount}");
                status.AppendLine($"Última actualización: {_lastSensorUpdate:HH:mm:ss}");

                var timeSinceUpdate = DateTime.Now - _lastSensorUpdate;
                status.AppendLine($"Tiempo sin actualizar: {timeSinceUpdate.TotalSeconds:F1}s");

                // Obtener estadísticas
                GetDailyStats();
            }
            else
            {
                status.AppendLine("SensorManager: ✗ ERROR");
            }

            await DisplayAlert("Estado del Sensor", status.ToString(), "OK");
            System.Diagnostics.Debug.WriteLine(status.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnTestSensorClicked: {ex.Message}");
            await DisplayAlert("Error", $"Error obteniendo estado del sensor: {ex.Message}", "OK");
        }
#else
        await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
    }

    private async void OnSimulateStepsClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("*** SIMULANDO PASOS ***");

            _stepCount += 10;
            var today = DateTime.Today;
            Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = $"Pasos dados: {FormatStepCount(_stepCount)}";
            }
            UpdateStepsRemaining();

            System.Diagnostics.Debug.WriteLine($"Pasos simulados. Total: {_stepCount}");
            await DisplayAlert("Simulación", $"Se agregaron 10 pasos. Total: {FormatStepCount(_stepCount)}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error simulando pasos: {ex.Message}");
            await DisplayAlert("Error", "Error al simular pasos", "OK");
        }
    }

    private async void OnCleanDataClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("Confirmar",
                "¿Quieres limpiar los datos antiguos? Esto eliminará el historial de pasos de más de 30 días.",
                "Sí", "No");

            if (result)
            {
                CleanOldData();
                await DisplayAlert("Completado", "Datos antiguos eliminados correctamente.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnCleanDataClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al limpiar datos", "OK");
        }
    }

    private async void OnShowStatsClicked(object sender, EventArgs e)
    {
        try
        {
            var today = DateTime.Today;
            int todaySteps = Preferences.Get($"steps_{today:yyyy-MM-dd}", 0);
            int goal = Preferences.Get("daily_steps_goal_value", 8000);

            double progressPercentage = goal > 0 ? (double)todaySteps / goal * 100 : 0;
            var calories = CalculateEstimatedCalories(todaySteps);
            var distance = CalculateEstimatedDistance(todaySteps);

            var stats = new System.Text.StringBuilder();
            stats.AppendLine("=== ESTADÍSTICAS DE HOY ===\n");
            stats.AppendLine($"📊 Pasos realizados: {FormatStepCount(todaySteps)}");
            stats.AppendLine($"🎯 Meta diaria: {FormatStepCount(goal)}");
            stats.AppendLine($"📈 Progreso: {progressPercentage:F1}%");
            stats.AppendLine($"🔥 Calorías estimadas: {calories} cal");
            stats.AppendLine($"📏 Distancia estimada: {distance:F2} km");

            if (progressPercentage >= 100)
            {
                stats.AppendLine("\n🎉 ¡Felicidades! Has alcanzado tu meta diaria.");
            }
            else
            {
                int remaining = goal - todaySteps;
                stats.AppendLine($"\n⏳ Te faltan {FormatStepCount(remaining)} pasos para alcanzar tu meta.");
            }

            await DisplayAlert("Estadísticas", stats.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnShowStatsClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al mostrar estadísticas", "OK");
        }
    }

    private async void OnRefreshSensorClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await DisplayAlert("Información", "Reiniciando sensor...", "OK");
            await RestartSensorAsync();
            await DisplayAlert("Completado", "Sensor reiniciado correctamente.", "OK");
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnRefreshSensorClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al reiniciar el sensor", "OK");
        }
    }

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        try
        {
            var about = new System.Text.StringBuilder();
            about.AppendLine("=== ACERCA DE SENSOR APP ===\n");
            about.AppendLine("Aplicación para contar pasos diarios");
            about.AppendLine("Versión: 1.0.0");
            about.AppendLine("Plataforma: .NET MAUI");
            about.AppendLine("\n--- CARACTERÍSTICAS ---");
            about.AppendLine("• Conteo automático de pasos");
            about.AppendLine("• Metas personalizadas por edad");
            about.AppendLine("• Historial de 7 días");
            about.AppendLine("• Estadísticas detalladas");
            about.AppendLine("• Estimación de calorías y distancia");
            about.AppendLine("• Funciona en segundo plano");
            about.AppendLine("\n--- SENSORES COMPATIBLES ---");
            about.AppendLine("• Step Counter (Android)");
            about.AppendLine("• Step Detector (Android)");
            about.AppendLine("\nDesarrollado con ❤️ usando .NET MAUI");

            await DisplayAlert("Acerca de", about.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnAboutClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al mostrar información", "OK");
        }
    }

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        try
        {
            var export = new System.Text.StringBuilder();
            export.AppendLine("HISTORIAL COMPLETO DE PASOS");
            export.AppendLine("============================\n");

            var today = DateTime.Today;

            // Exportar últimos 30 días
            for (int i = 0; i < 30; i++)
            {
                var date = today.AddDays(-i);
                var steps = Preferences.Get($"steps_{date:yyyy-MM-dd}", 0);

                if (steps > 0)
                {
                    export.AppendLine($"{date:yyyy-MM-dd}: {steps} pasos");
                }
            }

            await DisplayAlert("Exportar Datos", export.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnExportDataClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al exportar datos", "OK");
        }
    }

    private async void OnTestPermissionsClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var hasPermission = CheckActivityRecognitionPermissionNative();

            if (hasPermission)
            {
                await DisplayAlert("Permisos", "✓ Todos los permisos están concedidos", "OK");
            }
            else
            {
                var result = await DisplayAlert("Permisos",
                    "⚠️ Faltan permisos necesarios.\n¿Quieres intentar solicitarlos de nuevo?",
                    "Sí", "No");

                if (result)
                {
                    await CheckAndRequestActivityRecognitionPermission();
                }
            }
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnTestPermissionsClicked: {ex.Message}");
            await DisplayAlert("Error", "Error al verificar permisos", "OK");
        }
    }

    // Método faltante para el evento OnCheckSensorStatusClicked
    private async void OnCheckSensorStatusClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== ESTADO GENERAL DEL SENSOR ===\n");

            if (_sensorManager != null)
            {
                status.AppendLine($"✓ SensorManager inicializado correctamente");
                status.AppendLine($"StepCounter: {(_stepCounter != null ? "✓ Disponible" : "✗ No disponible")}");
                status.AppendLine($"StepDetector: {(_stepDetector != null ? "✓ Disponible" : "✗ No disponible")}");
                status.AppendLine($"Sensor activo: {(_isListenerActive ? "✓ Sí" : "✗ No")}");

                if (_isListenerActive)
                {
                    var sensorType = _useStepDetector ? "Step Detector" : "Step Counter";
                    status.AppendLine($"Tipo de sensor en uso: {sensorType}");

                    var timeSinceUpdate = DateTime.Now - _lastSensorUpdate;
                    if (timeSinceUpdate.TotalMinutes < 5)
                    {
                        status.AppendLine($"✓ Sensor funcionando (última actualización: {timeSinceUpdate.TotalSeconds:F0}s)");
                    }
                    else
                    {
                        status.AppendLine($"⚠️ Sensor inactivo (última actualización: {timeSinceUpdate.TotalMinutes:F0}m)");
                    }
                }

                status.AppendLine($"Permisos: {(CheckActivityRecognitionPermissionNative() ? "✓ Concedidos" : "✗ Faltantes")}");
                status.AppendLine($"\nPasos detectados hoy: {_stepCount}");

                var goal = Preferences.Get("daily_steps_goal_value", 8000);
                var progress = goal > 0 ? (double)_stepCount / goal * 100 : 0;
                status.AppendLine($"Progreso hacia la meta: {progress:F1}%");
            }
            else
            {
                status.AppendLine("✗ Error: SensorManager no disponible");
                status.AppendLine("El dispositivo podría no tener sensores compatibles");
            }

            await DisplayAlert("Estado del Sensor", status.ToString(), "OK");
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnCheckSensorStatusClicked: {ex.Message}");
            await DisplayAlert("Error", $"Error verificando estado del sensor: {ex.Message}", "OK");
        }
    }

    // Método para probar pasos reales - CORREGIDO
    private async void OnTestRealStepsClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await DisplayAlert("Test Pasos Reales",
                "Camina 10-15 pasos de forma normal y luego presiona OK", "OK");

            var initialSteps = _stepCount;
            var initialTime = _lastSensorUpdate;

            await DisplayAlert("Esperando...",
                "Ahora camina 10-15 pasos y espera 5 segundos", "OK");

            await Task.Delay(5000); // Esperar 5 segundos

            var finalSteps = _stepCount;
            var finalTime = _lastSensorUpdate;
            var stepsDetected = finalSteps - initialSteps;
            var timeDiff = finalTime - initialTime;

            var result = new System.Text.StringBuilder();
            result.AppendLine("=== RESULTADO DEL TEST ===\n");
            result.AppendLine($"Pasos detectados: {stepsDetected}");
            result.AppendLine($"Tiempo transcurrido: {timeDiff.TotalSeconds:F1}s");

            if (stepsDetected > 0)
            {
                result.AppendLine("\n✅ ¡El sensor está funcionando!");
            }
            else if (timeDiff.TotalSeconds > 0)
            {
                result.AppendLine("\n⚠️ El sensor recibe eventos pero no detecta pasos");
                result.AppendLine("Intenta caminar más rápido o fuerte");
            }
            else
            {
                result.AppendLine("\n❌ El sensor no está recibiendo eventos");
                result.AppendLine("Necesita reiniciarse");
            }

            await DisplayAlert("Resultado Test", result.ToString(), "OK");
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnTestRealStepsClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en test de pasos reales", "OK");
        }
    }

    // Método para reinicio completo - CORREGIDO
    private async void OnFullSensorRestartClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await DisplayAlert("Reiniciando", "Reiniciando sensor completamente...", "OK");

            System.Diagnostics.Debug.WriteLine("*** REINICIO COMPLETO DEL SENSOR ***");

            // Paso 1: Desregistrar completamente
            UnregisterSensorListener();
            await Task.Delay(2000);

            // Paso 2: Limpiar referencias
            _listener = null;
            _isListenerActive = false;
            await Task.Delay(1000);

            // Paso 3: Recrear listener
            _listener = new SensorListener();
            _listener.SensorChanged += OnSensorChanged;
            await Task.Delay(1000);

            // Paso 4: Intentar con sensor alternativo primero
            if (!_useStepDetector && _stepDetector != null)
            {
                System.Diagnostics.Debug.WriteLine("Intentando primero con StepDetector");
                _useStepDetector = true;
            }
            else if (_useStepDetector && _stepCounter != null)
            {
                System.Diagnostics.Debug.WriteLine("Intentando primero con StepCounter");
                _useStepDetector = false;
            }

            // Paso 5: Registrar de nuevo
            RegisterSensorListener();

            await Task.Delay(1000);

            var status = _isListenerActive ? "✅ Exitoso" : "❌ Falló";
            await DisplayAlert("Reinicio Completado",
                $"Estado: {status}\n" +
                $"Sensor: {(_useStepDetector ? "StepDetector" : "StepCounter")}", "OK");
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnFullSensorRestartClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en reinicio completo", "OK");
        }
    }


    // Método para cambiar sensor - CORREGIDO
    private async void OnSwitchSensorTypeClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var currentType = _useStepDetector ? "StepDetector" : "StepCounter";
            var newType = _useStepDetector ? "StepCounter" : "StepDetector";

            var result = await DisplayAlert("Cambiar Sensor",
                $"Actual: {currentType}\n¿Cambiar a {newType}?", "Sí", "No");

            if (result)
            {
                UnregisterSensorListener();
                await Task.Delay(1000);

                _useStepDetector = !_useStepDetector;

                if (_useStepDetector && _stepDetector == null)
                {
                    await DisplayAlert("Error", "StepDetector no disponible", "OK");
                    _useStepDetector = false;
                    return;
                }

                if (!_useStepDetector && _stepCounter == null)
                {
                    await DisplayAlert("Error", "StepCounter no disponible", "OK");
                    _useStepDetector = true;
                    return;
                }

                RegisterSensorListener();

                await DisplayAlert("Cambiado",
                    $"Ahora usando: {(_useStepDetector ? "StepDetector" : "StepCounter")}", "OK");
            }
#else
            await DisplayAlert("Info", "Esta funcionalidad solo está disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnSwitchSensorTypeClicked: {ex.Message}");
            await DisplayAlert("Error", "Error cambiando sensor", "OK");
        }
    }

    // Método simple para probar actualización - SIN ANDROID
    private async void OnSimpleTestClicked(object sender, EventArgs e)
    {
        try
        {
            var oldSteps = _stepCount;
            _stepCount += 5;

            var today = DateTime.Today;
            Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

            if (StepCounterLabel != null)
            {
                StepCounterLabel.Text = $"Pasos dados: {FormatStepCount(_stepCount)}";
            }
            UpdateStepsRemaining();

            await DisplayAlert("Test Simple",
                $"Pasos: {oldSteps} → {_stepCount}\n" +
                $"Última actualización: {_lastSensorUpdate:HH:mm:ss}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnSimpleTestClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en test simple", "OK");
        }
    }

    // ====== MÉTODO PARA CALIBRAR EL SENSOR ======
    private async void OnCalibrateSensorClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var result = await DisplayAlert("Calibrar Sensor",
                "¿Quieres recalibrar el sensor?\n" +
                "Esto restablecerá el punto de referencia para hoy.", "Sí", "No");

            if (result)
            {
                var today = DateTime.Today;

                // Opción 1: Reset completo
                var option = await DisplayAlert("Tipo de Calibración",
                    "¿Cómo quieres calibrar?", "Reset Completo", "Ajuste Fino");

                if (option) // Reset completo
                {
                    _stepCount = 0;
                    _initialStepCount = -1;
                    Preferences.Remove($"steps_{today:yyyy-MM-dd}");
                    Preferences.Remove($"initial_steps_{today:yyyy-MM-dd}");

                    await DisplayAlert("Reset Completo",
                        "Contador reiniciado a 0.\nEmpezará a contar desde ahora.", "OK");
                }
                else // Ajuste fino
                {
                    string input = await DisplayPromptAsync("Ajuste Fino",
                        $"Pasos actuales: {_stepCount}\n¿Cuántos pasos has dado realmente?");

                    if (int.TryParse(input, out int realSteps) && realSteps >= 0)
                    {
                        int adjustment = realSteps - _stepCount;
                        _stepCount = realSteps;
                        Preferences.Set($"steps_{today:yyyy-MM-dd}", _stepCount);

                        await DisplayAlert("Ajuste Aplicado",
                            $"Pasos ajustados: {adjustment:+#;-#;0}\n" +
                            $"Nuevo total: {_stepCount}", "OK");
                    }
                }

                UpdateUI();
            }
#else
        await DisplayAlert("Info", "Solo disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnCalibrateSensorClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en calibración", "OK");
        }
    }

    // ====== MÉTODO PARA MONITOREAR PRECISIÓN ======
    private async void OnMonitorAccuracyClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await DisplayAlert("Monitor de Precisión",
                "Vamos a monitorear la precisión del sensor.\n" +
                "Camina exactamente 20 pasos contándolos manualmente.", "Empezar");

            var initialSteps = _stepCount;
            var startTime = DateTime.Now;

            await DisplayAlert("¡Ahora!",
                "Camina exactamente 20 pasos.\n" +
                "Cuenta en voz alta: 1, 2, 3...\n" +
                "Camina de forma normal y constante.", "Terminé");

            var finalSteps = _stepCount;
            var endTime = DateTime.Now;
            var detectedSteps = finalSteps - initialSteps;
            var duration = endTime - startTime;

            // CÁLCULO CORREGIDO DE PRECISIÓN
            var accuracy = detectedSteps > 0 ? ((double)detectedSteps / 20.0) * 100 : 0;
            var error = Math.Abs(detectedSteps - 20);
            var errorPercentage = (error / 20.0) * 100;

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== REPORTE DE PRECISIÓN ===\n");
            report.AppendLine($"Pasos reales: 20");
            report.AppendLine($"Pasos detectados: {detectedSteps}");
            report.AppendLine($"Precisión: {accuracy:F1}%");
            report.AppendLine($"Error: {error} pasos ({errorPercentage:F1}%)");
            report.AppendLine($"Tiempo: {duration.TotalSeconds:F1}s");

            // EVALUACIÓN CORREGIDA
            if (detectedSteps == 20)
            {
                report.AppendLine("\n🎯 Precisión perfecta!");
            }
            else if (accuracy >= 85 && accuracy <= 115) // 17-23 pasos detectados
            {
                report.AppendLine("\n✅ Buena precisión");
            }
            else if (accuracy >= 70 && accuracy <= 130) // 14-26 pasos detectados
            {
                report.AppendLine("\n⚠️ Precisión aceptable");
                if (detectedSteps < 20)
                {
                    report.AppendLine("El sensor subcuenta pasos");
                    report.AppendLine("Intenta: caminar más fuerte, más regular, o cambiar sensibilidad");
                }
                else
                {
                    report.AppendLine("El sensor sobrecuenta pasos");
                    report.AppendLine("Intenta: caminar más suave o cambiar sensibilidad");
                }
            }
            else
            {
                report.AppendLine("\n❌ Baja precisión");
                if (detectedSteps < 10)
                {
                    report.AppendLine("El sensor está muy poco sensible");
                    report.AppendLine("Recomendación: Cambiar a StepDetector");
                }
                else
                {
                    report.AppendLine("El sensor está muy sensible");
                    report.AppendLine("Recomendación: Ajustar filtros o cambiar sensor");
                }
            }

            await DisplayAlert("Reporte de Precisión", report.ToString(), "OK");
#else
        await DisplayAlert("Info", "Solo disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnMonitorAccuracyClicked: {ex.Message}");
            await DisplayAlert("Error", "Error en monitor de precisión", "OK");
        }
    }

    // ====== MÉTODO PARA MEJORAR SENSIBILIDAD ======
    private async void OnAdjustSensitivityClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            var options = await DisplayActionSheet("Ajustar Sensibilidad",
                "Cancelar", null,
                "Más Sensible (detecta más pasos)",
                "Menos Sensible (detecta menos pasos)",
                "Reiniciar Sensor con Mayor Sensibilidad");

            switch (options)
            {
                case "Más Sensible (detecta más pasos)":
                    // Cambiar a StepDetector que es más sensible
                    if (_stepDetector != null)
                    {
                        UnregisterSensorListener();
                        await Task.Delay(1000);
                        _useStepDetector = true;
                        RegisterSensorListener();
                        await DisplayAlert("Sensibilidad", "Cambiado a StepDetector (más sensible)", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "StepDetector no disponible", "OK");
                    }
                    break;

                case "Menos Sensible (detecta menos pasos)":
                    // Mantener StepCounter pero con filtros más estrictos
                    if (_stepCounter != null)
                    {
                        UnregisterSensorListener();
                        await Task.Delay(1000);
                        _useStepDetector = false;
                        RegisterSensorListener();
                        await DisplayAlert("Sensibilidad", "Mantenido en StepCounter (menos sensible)", "OK");
                    }
                    break;

                case "Reiniciar Sensor con Mayor Sensibilidad":
                    // Reiniciar con delay más rápido
                    await RestartSensorWithHighSensitivity();
                    break;
            }
#else
        await DisplayAlert("Info", "Solo disponible en Android", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnAdjustSensitivityClicked: {ex.Message}");
            await DisplayAlert("Error", "Error ajustando sensibilidad", "OK");
        }
    }

    // ====== MÉTODO PARA REINICIAR CON ALTA SENSIBILIDAD ======
    private async Task RestartSensorWithHighSensitivity()
    {
        try
        {
#if ANDROID
            System.Diagnostics.Debug.WriteLine("*** REINICIANDO CON ALTA SENSIBILIDAD ***");

            UnregisterSensorListener();
            await Task.Delay(2000);

            // Recrear listener
            _listener = new SensorListener();
            _listener.SensorChanged += OnSensorChanged;

            // Usar el sensor más sensible disponible
            Sensor sensorToUse = _stepDetector ?? _stepCounter;
            _useStepDetector = _stepDetector != null;

            if (sensorToUse != null && _listener != null)
            {
                // Usar el delay más rápido posible
                SensorDelay delay = SensorDelay.Fastest;
                bool registered = _sensorManager.RegisterListener(_listener, sensorToUse, delay);

                if (!registered)
                {
                    delay = SensorDelay.Game;
                    registered = _sensorManager.RegisterListener(_listener, sensorToUse, delay);
                }

                _isListenerActive = registered;

                var sensorName = _useStepDetector ? "StepDetector" : "StepCounter";
                await DisplayAlert("Reinicio Completo",
                    $"Sensor: {sensorName}\n" +
                    $"Delay: {delay}\n" +
                    $"Estado: {(registered ? "✅ Activo" : "❌ Error")}", "OK");
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en RestartSensorWithHighSensitivity: {ex.Message}");
        }
    }
}