using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Entities;

public class Settings
{
#pragma warning disable IDE1006 // Naming Styles
    public ObjectId _id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    public string Key { get; set; }
    public object Value { get; set; }
    public string ParentCategoryId { get; set; }
    public string Description { get; set; }
}
