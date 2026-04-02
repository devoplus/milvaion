using Microsoft.Extensions.Configuration;
using Suvari.ScheduledTasks.Core;
using Suvari.ScheduledTasks.Core.Utilities;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Suvari.ScheduledTasks.Data.MongoDb;

/// <summary>
/// Statik proje ayarlarına erişmek için kullanılır.
/// Docker ortamında IConfiguration üzerinden, Windows ortamında şifreli dosyadan okur.
/// </summary>
public sealed partial class SettingsExtensions
{
    private static readonly byte[] _initVectorBytes = Encoding.ASCII.GetBytes("tu89geji340t89u2");
    private const int _keysize = 256;

    /// <summary>
    /// Erişilebilecek Default Instance. Initialize() çağrılmadan kullanılmamalıdır.
    /// </summary>
    public static SettingsInstance Default { get; private set; } = new SettingsInstance();

    /// <summary>
    /// DI setup sırasında çağrılır. Dosya mevcutsa file-based, değilse IConfiguration kullanır.
    /// </summary>
    public static void Initialize(IConfiguration configuration, bool useFileBasedSettings)
    {
        if (useFileBasedSettings && IsAvailable())
        {
            Default = new SettingsInstance
            {
                MongoConnectionString = GetProperty("MongoConnectionString"),
                SettingsDbName        = GetProperty("SettingsDbName"),
                LogQueueDbName        = GetProperty("LogQueueDbName"),
                LogDbName             = GetProperty("LogDbName"),
            };
        }
        else
        {
            Default = new SettingsInstance
            {
                MongoConnectionString = configuration["MongoDB:ConnectionString"],
                SettingsDbName        = configuration["MongoDB:SettingsDbName"] ?? "Settings",
                LogQueueDbName        = configuration["MongoDB:LogQueueDbName"] ?? "LogQueue",
                LogDbName             = configuration["MongoDB:LogDbName"]      ?? "Logs",
            };
        }

        // Hardcoded servis URL'leri (her iki modda da aynı)
        Default.Suvari_SSO_QDMS_GWLoginService_GWLogin        = "https://qdms.suvari.com.tr/QDMSNET/BSAT/GWLogin.asmx";
        Default.Suvari_Bimser_QDMS_AgentWebService_Agent       = "https://qdms.suvari.com.tr/QDMSNET/QDMSAgent/Agent.asmx";
        Default.Suvari_Bimser_QDMS_DocumentWebService_DWS      = "https://qdms.suvari.com.tr/QDMSNET/DocumentsWS/DWS.asmx";
        Default.Suvari_SSO_eBAWSAPI_eBAWSAPI                   = "https://eba.suvari.com.tr/eba.net/ws/eBAWSAPI.asmx";
        Default.Suvari_eFinansEFatura_connectorService         = "https://connectortest.efinans.com.tr:443/connector/ws/connectorService";
        Default.Suvari_eFinansLogin_userService                = "https://connectortest.efinans.com.tr:443/connector/ws/userService";
        Default.Suvari_DGNAuthenticationWS_AuthenticationWS    = "https://efaturatest.doganedonusum.com/AuthenticationWS";
        Default.Suvari_DGNEInvoiceWS_EFaturaOIB               = "https://efaturatest.doganedonusum.com:443/EFaturaOIB";
        Default.Suvari_TicimaxUyeServis_UyeServis              = "https://www.suvari.com.tr/servis/UyeServis.svc";
        Default.Suvari_SiparisServis_SiparisServis             = "https://www.suvari.com.tr/Servis/SiparisServis.svc";
    }

    public static string TokenFile => Globals.CurrentBrand switch
    {
        Brand.Suvari      => @"C:\Windows\Suvari\token.sek",
        Brand.BackAndBond => @"C:\Windows\BackAndBond\token.sek",
        _                 => null
    };

    public static string SettingFile => Globals.CurrentBrand switch
    {
        Brand.Suvari      => @"C:\Windows\Suvari\settings.json",
        Brand.BackAndBond => @"C:\Windows\BackAndBond\settings.json",
        _                 => null
    };

    public static bool IsAvailable()
        => !string.IsNullOrEmpty(TokenFile)
        && !string.IsNullOrEmpty(SettingFile)
        && File.Exists(TokenFile)
        && File.Exists(SettingFile);

    private static string GetProperty(string name)
    {
        string encryptKey = File.ReadAllText(TokenFile);
        SettingFile setting = JsonConvert.DeserializeObject<SettingFile>(File.ReadAllText(SettingFile));
        var settingItem = setting.Settings.FirstOrDefault(t => t.Name == Encryption.Base64Encode(name));

        byte[] cipherTextBytes = Convert.FromBase64String(settingItem.Value);
        using var password = new PasswordDeriveBytes(encryptKey, null);
        byte[] keyBytes = password.GetBytes(_keysize / 8);

        using var aes = Aes.Create();
        aes.Padding = PaddingMode.Zeros;
        aes.Mode    = CipherMode.CBC;

        using var decryptor    = aes.CreateDecryptor(keyBytes, _initVectorBytes);
        using var memoryStream = new MemoryStream(cipherTextBytes);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

        byte[] plainTextBytes      = new byte[cipherTextBytes.Length];
        int    decryptedByteCount  = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
        return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount).Replace("\0", string.Empty);
    }
}

/// <summary>
/// Tanımlanmış setting değerleri
/// </summary>
public class SettingsInstance
{
    /// <summary>
    /// MongoDB erişimi için kullanılan Connection String değeri
    /// </summary>
    public string MongoConnectionString { get; set; }
    /// <summary>
    /// MongoDB'de settinglerin saklandığı DB'nin adı
    /// </summary>
    public string SettingsDbName { get; set; }
    /// <summary>
    /// MongoDB'de log kuyruğunun saklandığı DB'nin adı
    /// </summary>
    public string LogQueueDbName { get; set; }
    /// <summary>
    /// MongoDB'de logların saklandığı DB'nin adı
    /// </summary>
    public string LogDbName { get; set; }
    /// <summary>
    /// SSO Projesinde kullanılan QDMS'in GWLogin web servis url'i
    /// </summary>
    public string Suvari_SSO_QDMS_GWLoginService_GWLogin { get; set; }
    /// <summary>
    /// Bimser entegrasyonunda kullanılan QDMS'in Agent web servis url'i
    /// </summary>
    public string Suvari_Bimser_QDMS_AgentWebService_Agent { get; set; }
    /// <summary>
    /// Bimser entegrasyonunda kullanılan Document web servis url'i
    /// </summary>
    public string Suvari_Bimser_QDMS_DocumentWebService_DWS { get; set; }
    /// <summary>
    /// Bimser eBA entegrasyonunda kullanılan web servis url'i
    /// </summary>
    public string Suvari_SSO_eBAWSAPI_eBAWSAPI { get; set; }
    /// <summary>
    /// eFinans entegrasyonunda e-fatura için kullanılan web service url'i
    /// </summary>
    public string Suvari_eFinansEFatura_connectorService { get; set; }
    /// <summary>
    /// eFinans entegrasyonunda service authentication için kullanılan web service url'i
    /// </summary>
    public string Suvari_eFinansLogin_userService { get; set; }
    public string Suvari_DGNAuthenticationWS_AuthenticationWS { get; set; }
    public string Suvari_DGNEInvoiceWS_EFaturaOIB { get; set; }
    public string Suvari_TicimaxUyeServis_UyeServis { get; set; }
    public string Suvari_SiparisServis_SiparisServis { get; set; }
}

/// <summary>
/// JSON formatındaki setting dosyasının modeli
/// </summary>
public class SettingFile
{
    /// <summary>
    /// Dosya versiyonu
    /// </summary>
    public string Version { get; set; }
    /// <summary>
    /// Dev - Prod environment bilgisi
    /// </summary>
    public string Environment { get; set; }
    /// <summary>
    /// Tanımlı settinglerin listesi
    /// </summary>
    public Setting[] Settings { get; set; }
}

/// <summary>
/// JSON formatındaki setting dosyasında saklanan settinglerin modeli
/// </summary>
public class Setting
{
    /// <summary>
    /// Setting adı
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Setting değeri
    /// </summary>
    public string Value { get; set; }
}