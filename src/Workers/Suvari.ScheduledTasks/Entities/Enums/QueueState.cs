using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Entities.Enums;

public enum QueueState
{
    Waiting = 0,
    Sending = 1,
    Completed = 2,
    Error = 3
}
