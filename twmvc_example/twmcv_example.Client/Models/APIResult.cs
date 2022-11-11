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

public record APIResultForList
{
  public OutputList outPut { get; set; }

  public record OutputList
  {
    public int ErrorCode { get; set; }
    public dynamic Result { get; set; }
    public DateTime Timestamp { get; set; }
  }
}
