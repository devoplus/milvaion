using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Entities;

public partial class Employee
{
    public string PersonalCode { get; set; }
    public decimal CompanyCode { get; set; }
    public string WorkPlaceCode { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public byte Gender { get; set; }
    public string IdentityNum { get; set; }
    public DateTime BirthDate { get; set; }
    public string BirthPlace { get; set; }
    public DateTime JobStartDate { get; set; }
    public DateTime JobEndDate { get; set; }
    public string NebimGsm { get; set; }
    public string IntranetGsm { get; set; }
    public string NebimEmail { get; set; }
    public string IntranetEmail { get; set; }
    public string PositionCode { get; set; }
    public string PositionName { get; set; }
    public string TitleCode { get; set; }
    public string Title { get; set; }
    public string NebimDepartmanCode { get; set; }
    public string NebimDepartmanName { get; set; }
    public string Kirilim1Kod { get; set; }
    public string Kirilim1 { get; set; }
    public string Kirilim2Kod { get; set; }
    public string Kirilim2 { get; set; }
    public string Kirilim3Kod { get; set; }
    public string Kirilim3 { get; set; }
    public string PersonalTypeCode { get; set; }
    public string PersonalTypeName { get; set; }
    public string Askerlik { get; set; }
    public string Egitim { get; set; }
    public bool MedeniHal { get; set; }
    public string NebimDahili { get; set; }
    public string CardNumber { get; set; }
    public string BrandCode { get; set; }
}

