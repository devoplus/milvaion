using MongoDB.Bson;
using Suvari.ScheduledTasks.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Entities;

public class LogQueue
{
#pragma warning disable IDE1006 // Naming Styles
    public ObjectId _id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    public string Message { get; set; }
    public long Channel { get; set; }
    public bool DisableNotification { get; set; }
    public DateTime Created { get; set; }
    public QueueState State { get; set; }
    public bool IsHighPriority { get; set; }
    public int RetryCount { get; set; }
}
