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

}
