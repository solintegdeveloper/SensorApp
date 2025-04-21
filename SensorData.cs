using SQLite;

public class SensorData
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SensorType { get; set; }
    public float Value1 { get; set; }
    public float Value2 { get; set; }
    public float Value3 { get; set; }
    public DateTime Timestamp { get; set; }
}

