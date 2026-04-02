using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Core;

public class BaseResponse<T>
{
    public T Data { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
}
