using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Core;

public class Globals
{
    public static Brand CurrentBrand = Brand.Suvari;
}

public enum Brand
{
    Suvari = 0,
    BackAndBond = 1
}