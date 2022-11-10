namespace twmcv_example.Client.Models;

public class APIResult
{
  public Output outPut { get; set; }
}

public record Output
{
  public int ErrorCode { get; set; }
  public Dictionary<string, string> Result { get; set; }
  public DateTime Timestamp { get; set; }
}
