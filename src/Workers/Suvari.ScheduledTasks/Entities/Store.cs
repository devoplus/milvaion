using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Entities;

public partial class Store
{
    public string Kod { get; set; }
    public string DepoKisaKod { get; set; }
    public string Depo { get; set; }
    public string Magaza { get; set; }
    public string MagazaAdi { get; set; }
    public string MagazaTipi { get; set; }
    public string SozlesmeM2 { get; set; }
    public string KullanilanSatisM2 { get; set; }
    public DateTime? AcilisTarihi { get; set; }
    public string Il { get; set; }
    public string Bolge { get; set; }
    public string MagazaAdres { get; set; }
    public string YetkiliKisi { get; set; }
    public string Unvan { get; set; }
    public string MagazaTelNo { get; set; }
    public string MagazaGsmNo { get; set; }
    public string MagazaEmail { get; set; }
    public string KisiselGsmNo { get; set; }
    public DateTime? TadilatTarihi { get; set; }
    public string Enlem { get; set; }
    public string Boylam { get; set; }
    public string TicariUnvan { get; set; }
    public string GBM { get; set; }
    public string GBMAdi { get; set; }
    public string BrandCode { get; set; }
}