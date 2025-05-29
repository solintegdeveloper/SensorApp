using SQLite;

namespace SensorApp;

public class DailySteps
{
    [PrimaryKey]
    public string Date { get; set; } // formato "yyyy-MM-dd"
    public int StepCount { get; set; }
}


//public class DailySteps
//{
//    [PrimaryKey, AutoIncrement]
//    public int Id { get; set; }
//    public string Date { get; set; }  // formato "yyyy-MM-dd"
//    public int StepCount { get; set; }
//}
