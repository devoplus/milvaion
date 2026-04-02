using Suvari.ScheduledTasks.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Core.Utilities;

public class BrandHelper
{
    public static bool CheckBrandCorrelation(Employee employee)
    {
        return !(employee.BrandCode == "BB" && Globals.CurrentBrand == Brand.Suvari) || (employee.BrandCode == "SVR" && Globals.CurrentBrand == Brand.BackAndBond);
    }

    public static bool CheckBrandCorrelation(Store store)
    {
        return !(store.BrandCode == "BB" && Globals.CurrentBrand == Brand.Suvari) || (store.BrandCode == "SVR" && Globals.CurrentBrand == Brand.BackAndBond);
    }

    public static string GetCurrentBrandCode
    {
        get
        {
            return Globals.CurrentBrand == Brand.Suvari ? "SVR" : Globals.CurrentBrand == Brand.BackAndBond ? "BB" : null;
        }
    }

    public static string CDNUri
    {
        get
        {
            return Globals.CurrentBrand == Brand.Suvari ? "https://cdn.suvari.com.tr/" : "https://cdn.backandbond.com/";
        }
    }
    public static string NebimCDNUri
    {
        get
        {
            return Globals.CurrentBrand == Brand.Suvari ? "https://cdn.suvari.com.tr/Static/" : "https://cdn.backandbond.com/Static/";
        }
    }
}