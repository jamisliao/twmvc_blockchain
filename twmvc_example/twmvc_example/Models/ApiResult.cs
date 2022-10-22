using System.Runtime.Serialization;

namespace twmvc_example.Models;

/// <summary>
///     API回傳物件
/// </summary>
[DataContract]
public class ApiResult
{
    /// <summary>
    ///     API回傳物件
    /// </summary>
    /// <param name="resultCode">執行結果代碼</param>
    /// <param name="data">回傳資料</param>
    public ApiResult(ResultCodeEnum resultCode, object data)
    {
        OutPut = new Dictionary<string, object>();
        OutPut.Add("ErrorCode", (int) resultCode);
        OutPut.Add("Result", data);
        OutPut.Add("Timestamp",
                   DateTimeOffset.UtcNow.ToOffset(new TimeSpan(-8, 0, 0))
                                 .ToString("yyyy-MM-ddTHH:mm:ss"));
    }

    public ApiResult(ResultCodeEnum resultCode, object data, string message = "")
    {
        OutPut = new Dictionary<string, object>();
        OutPut.Add("ErrorCode", resultCode);
        OutPut.Add("Result", data);
        OutPut.Add("Timestamp",
                   DateTimeOffset.UtcNow.ToOffset(new TimeSpan(-8, 0, 0))
                                 .ToString("yyyy-MM-ddTHH:mm:ss"));

        if (message != "")
        {
            OutPut.Add("Message", message);
        }
    }

    public Dictionary<string, object> OutPut { get; set; }
}