using System.ComponentModel;

namespace twmvc_example.Models;

public enum ResultCodeEnum
{
    /// <summary>
    ///     執行成功
    /// </summary>
    [Description("執行成功")]
    Success = 0,
    
    
    /// <summary>
    ///     等待授權中
    /// </summary>
    [Description("等待授權中")]
    StillPending = 11,
}