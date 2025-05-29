using SensorApp;
using SQLite;
using System.IO;

public class SensorDatabase
{
    private readonly SQLiteAsyncConnection _database;

    public SensorDatabase()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "sensors.db3");
        _database = new SQLiteAsyncConnection(dbPath);
        _database.CreateTableAsync<SensorData>().Wait();
        _database.CreateTableAsync<DailySteps>().Wait();

    }

    public Task<int> ClearSensorDataAsync()
    {
        return _database.DeleteAllAsync<SensorData>();
    }

    public async Task<int> SaveSensorDataAsync(SensorData data)
    {
        //return _database.InsertAsync(data);
        int result = await _database.InsertAsync(data);

        // SOLO PARA DEBUG — lo puedes borrar luego
        //if (result > 0)
        //    await Application.Current.MainPage.DisplayAlert("Guardado", $"{data.SensorType} insertado", "OK");

        return result;

    }

    public Task<List<SensorData>> GetAllSensorDataAsync()
    {
        return _database.Table<SensorData>().ToListAsync();
    }

    //public async Task<int> InsertSensorDataAsync(SensorData data)
    //{
    //    int result = await _database.InsertAsync(data);

    //    // SOLO PARA DEBUG — lo puedes borrar luego
    //    if (result > 0)
    //        await Application.Current.MainPage.DisplayAlert("Guardado", $"{data.SensorType} insertado", "OK");

    //    return result;
    //}

    public async Task<DailySteps> GetTodayStepsAsync()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        return await _database.Table<DailySteps>().Where(d => d.Date == today).FirstOrDefaultAsync();
    }

    public async Task AddStepForTodayAsync()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        var record = await GetTodayStepsAsync();

        if (record == null)
        {
            record = new DailySteps { Date = today, StepCount = 1 };
            await _database.InsertAsync(record);
        }
        else
        {
            record.StepCount += 1;
            await _database.UpdateAsync(record);
        }
    }

    //public async Task<List<DailySteps>> GetAllDailyStepsAsync()
    //{
    //    return await _database.Table<DailySteps>().OrderByDescending(d => d.Date).ToListAsync();
    //}
    public async Task<List<DailySteps>> GetAllDailyStepsAsync()
    {
        // Asegúrate que la tabla no tenga registros repetidos para la misma fecha
        // Si tienes varios registros para la misma fecha, aquí puedes hacer un GROUP BY o agrupar en C#

        var allSteps = await _database.Table<DailySteps>().OrderByDescending(d => d.Date).ToListAsync();

        // Si hay duplicados en la BD y quieres solo uno por fecha:
        var groupedSteps = allSteps
            .GroupBy(s => s.Date)
            .Select(g => g.First())  // O algún criterio para elegir un solo registro por fecha
            .ToList();

        return groupedSteps;
    }

    public async Task DeleteAllDailyStepsAsync()
    {
        await _database.DeleteAllAsync<DailySteps>();
    }

}
