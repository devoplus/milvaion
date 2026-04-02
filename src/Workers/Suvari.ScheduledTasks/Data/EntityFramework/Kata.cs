using Microsoft.Data.SqlClient;
using SqlKata;
using SqlKata.Compilers;
using Suvari.ScheduledTasks.Core.Utilities;
using System.Data;
using System.Reflection;
using System.Text;

namespace Suvari.ScheduledTasks.Data.EntityFramework;

/// <summary>
/// SQL erişimi için kullanılan Kata kütüphanesine ait helperlar.
/// </summary>
public class Kata
{
    /// <summary>
    /// Erişilecek SQL sunucusuna ait bağlantı parametresi
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Kata Helper
    /// </summary>
    /// <param name="connectionString">Bağlantı parametresi</param>
    public Kata(string connectionString)
    {
        string source = string.Empty;

        try
        {
            if (!string.IsNullOrEmpty(Environment.MachineName))
            {
                source = Environment.MachineName;
            }
            else if (!string.IsNullOrEmpty(System.Net.Dns.GetHostName()))
            {
                source = System.Net.Dns.GetHostName();
            }
            else
            {
                source = "UnknownPC";
            }
        }
        catch
        {
            source = "UnknownPC";
        }

        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
        builder.ApplicationName        = $"Suvari-{source}";
        builder.TrustServerCertificate = true;

        ConnectionString = builder.ConnectionString;
    }

    /// <summary>
    /// SQLKata'da yaşanan exceptionları detaylı şekilde döndürür.
    /// </summary>
    /// <param name="q">Hata yaşanan query</param>
    /// <param name="ex">Hata sırasında alınan exception</param>
    /// <returns>Düzenlenmiş exception nesnesi</returns>
    public Exception GetKataExceptionDetails(SqlKata.Query q, Exception ex, CommandType commandType = CommandType.Text)
    {
        try
        {
            q = q.FixDBNullValues();
            var sqlQuery = new SqlServerCompiler().Compile(q);

            string caller = string.Empty;

            try
            {
                caller = (new System.Diagnostics.StackTrace()).GetFrame(1).GetMethod().Name;
            }
            catch
            {
                caller = "Unknown";
            }

            if (commandType == CommandType.Text)
            {
                StringBuilder parameters = new StringBuilder();
                for (int i = 0; i < sqlQuery.Bindings.Count; i++)
                {
                    if (sqlQuery.Bindings[i].GetType() == DBNull.Value.GetType())
                    {
                        parameters.AppendLine($"DECLARE @p{i} {SqlHelper.GetDbType(sqlQuery.Bindings[i].GetType())} = NULL;");
                    }
                    else if (Text.IsNumeric(sqlQuery.Bindings[i].ToString()))
                    {
                        parameters.AppendLine($"DECLARE @p{i} {SqlHelper.GetDbType(sqlQuery.Bindings[i].GetType())} = {sqlQuery.Bindings[i]};");
                    }
                    else if (sqlQuery.Bindings[i].GetType().Name == "DateTime")
                    {
                        parameters.AppendLine($"DECLARE @p{i} {SqlHelper.GetDbType(sqlQuery.Bindings[i].GetType())} = '{((DateTime)sqlQuery.Bindings[i]).ToString("yyyy-MM-ddTHH:mm:ss")}';");
                    }
                    else
                    {
                        parameters.AppendLine($"DECLARE @p{i} {SqlHelper.GetDbType(sqlQuery.Bindings[i].GetType())} = '{sqlQuery.Bindings[i]}';");
                    }
                }

                return new Exception($"SQL Error{Environment.NewLine}Caller:{caller}{Environment.NewLine}Query: {sqlQuery.Sql}{Environment.NewLine}Parameters:{Environment.NewLine}{parameters.ToString()}", ex);
            }
            else
            {
                return new Exception($"SQL Error{Environment.NewLine}Caller:{caller}{Environment.NewLine}Query: {$"EXEC dbo.{((FromClause)q.Clauses[0]).Table} {GetSPParams(sqlQuery.Bindings)}"}", ex);
            }
        }
        catch
        {
            return ex;
        }
    }

    /// <summary>
    /// Verilen query'i çalıştırıp işlem sonucunda etkilenen satır sayısını verir.
    /// </summary>
    /// <param name="q">Query</param>
    /// <param name="cmdType">Query tipi</param>
    /// <returns>Etkilenen satır sayısı</returns>
    public int ExecuteNonQuery(Query q, CommandType cmdType = CommandType.Text)
    {
        int t = 0;

        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();

            try
            {
                var currentCulture = Thread.CurrentThread.CurrentCulture;

                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

                q = q.FixDBNullValues();
                SqlResult sqlResult = new SqlServerCompiler().Compile(q);

                Thread.CurrentThread.CurrentUICulture = currentCulture;
                Thread.CurrentThread.CurrentCulture = currentCulture;

                SqlCommand cmd = new SqlCommand(sqlResult.Sql, conn)
                {
                    CommandType = cmdType,
                    CommandTimeout = 0
                };

                if (cmdType == CommandType.StoredProcedure)
                {
                    cmd.CommandText = $"EXEC dbo.{((FromClause)q.Clauses[0]).Table} {GetSPParams(sqlResult.Bindings)}";
                    cmd.CommandType = CommandType.Text;
                }
                else
                {
                    cmd.CommandText = new SqlServerCompiler().Compile(q).Sql;

                    for (int i = 0; i < sqlResult.Bindings.Count; i++)
                    {
                        if (sqlResult.Bindings[i] == null)
                        {
                            sqlResult.Bindings[i] = DBNull.Value;
                        }

                        cmd.Parameters.AddWithValue("@p" + i, sqlResult.Bindings[i]);
                    }
                }

                t = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Exceptions.NewException(GetKataExceptionDetails(q, ex, cmdType));
            }
            finally
            {
                conn.Close();
            }
        }

        return t;
    }

    /// <summary>
    /// Verilen query'i çalıştırıp işlem sonucunda DataTable verir.
    /// </summary>
    /// <param name="q">Query</param>
    /// <param name="cmdType">Query tipi</param>
    /// <returns>SQL'den dönen tablo</returns>
    public DataTable ExecuteReader(SqlKata.Query q, CommandType cmdType = CommandType.Text, bool rawQuery = false)
    {
        DataTable dt = new DataTable();

        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();

            try
            {
                SqlCommand cmd = new SqlCommand
                {
                    CommandType = cmdType,
                    Connection = conn,
                    CommandTimeout = 0
                };

                var currentCulture = Thread.CurrentThread.CurrentCulture;

                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

                q = q.FixDBNullValues();
                SqlResult sqlResult = new SqlServerCompiler().Compile(q);

                Thread.CurrentThread.CurrentUICulture = currentCulture;
                Thread.CurrentThread.CurrentCulture = currentCulture;

                if (cmdType == CommandType.StoredProcedure)
                {
                    cmd.CommandText = string.Format("EXEC dbo.{0} {1}", ((SqlKata.FromClause)q.Clauses[0]).Table, GetSPParams(sqlResult.Bindings));
                    cmd.CommandType = CommandType.Text;
                }
                else
                {
                    if (!rawQuery)
                    {
                        cmd.CommandText = new SqlServerCompiler().Compile(q).Sql;
                        // Kata Datetime Bug
                        cmd.CommandText = cmd.CommandText.Replace("AS DATE", "AS DATETIME");
                    }
                    else
                    {
                        cmd.CommandText = ((SqlKata.RawFromClause)q.Clauses[0]).Expression;
                    }

                    for (int i = 0; i < sqlResult.Bindings.Count; i++)
                    {
                        cmd.Parameters.AddWithValue("@p" + i, sqlResult.Bindings[i]);
                    }
                }

                dt.Load(cmd.ExecuteReader());
            }
            catch (Exception ex)
            {
                Exceptions.NewException(GetKataExceptionDetails(q, ex, cmdType));
            }
            finally
            {
                conn.Close();
            }
        }

        return dt;
    }

    /// <summary>
    /// Verilen query'i çalıştırıp işlem sonucunda verilen veri modeline göre serialization işlemi gerçekleştirir.
    /// </summary>
    /// <typeparam name="T">Query'den dönecek tablonun modeli</typeparam>
    /// <param name="q">Query</param>
    /// <param name="cmdType">Query tipi</param>
    /// <returns>Verilen veri modeline göre List türünde serialize edilmiş SQL işlem sonucu</returns>
    public List<T> ExecuteReader<T>(SqlKata.Query q, CommandType cmdType = CommandType.Text)
    {
        List<T> result = new List<T>();

        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();

            try
            {
                SqlCommand cmd = new SqlCommand
                {
                    CommandType = cmdType,
                    Connection = conn,
                    CommandTimeout = 0
                };

                var currentCulture = Thread.CurrentThread.CurrentCulture;

                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

                q = q.FixDBNullValues();
                SqlResult sqlResult = new SqlServerCompiler().Compile(q);

                if (cmdType == CommandType.StoredProcedure)
                {
                    cmd.CommandText = string.Format("EXEC dbo.{0} {1}", ((FromClause)q.Clauses[0]).Table, GetSPParams(sqlResult.Bindings));
                    cmd.CommandType = CommandType.Text;
                }
                else
                {
                    cmd.CommandText = new SqlServerCompiler().Compile(q).Sql;

                    for (int i = 0; i < sqlResult.Bindings.Count; i++)
                    {
                        cmd.Parameters.AddWithValue("@p" + i, sqlResult.Bindings[i]);
                    }
                }

                Thread.CurrentThread.CurrentUICulture = currentCulture;
                Thread.CurrentThread.CurrentCulture = currentCulture;

                DataTable dt = new DataTable();
                dt.Load(cmd.ExecuteReader());

                result = ConvertFromDataTable<T>(dt);
            }
            catch (Exception ex)
            {
                Exceptions.NewException(GetKataExceptionDetails(q, ex, cmdType));
            }
            finally
            {
                conn.Close();
            }
        }

        return result;
    }

    /// <summary>
    /// DataTable'dan belirli bir nesneye veri dönüşümü sağlar.
    /// </summary>
    /// <typeparam name="T">Dönüşüm sağlanacak nesne</typeparam>
    /// <param name="dt">Tablo</param>
    /// <returns>Dönüştürülmüş liste</returns>
    private List<T> ConvertFromDataTable<T>(DataTable dt)
    {
        List<T> data = new List<T>();
        foreach (DataRow row in dt.Rows)
        {
            T item = GetItem<T>(row);

            data.Add(item);
        }
        return data;
    }

    /// <summary>
    /// DataRow'dan belirli bir nesneye veri dönüşümü sağlar.
    /// </summary>
    /// <typeparam name="T">Dönüşüm sağlanacak nesne</typeparam>
    /// <param name="dr">Satır</param>
    /// <returns>Dönüştürülmüş nesne</returns>
    private T GetItem<T>(DataRow dr)
    {
        Type temp = typeof(T);
        T obj = Activator.CreateInstance<T>();

        foreach (DataColumn column in dr.Table.Columns)
        {
            foreach (PropertyInfo pro in temp.GetProperties())
            {
                if (pro.Name == column.ColumnName)
                {
                    object tmpValue = dr[column.ColumnName];

                    if (tmpValue.GetType() != DBNull.Value.GetType())
                    {
                        try
                        {
                            pro.SetValue(obj, tmpValue, null);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"A data type mismatch was detected for the {column.ColumnName} column.", ex);
                        }
                    }
                    else
                    {
                        pro.SetValue(obj, null, null);
                    }
                }
                else
                    continue;
            }
        }
        return obj;
    }

    /// <summary>
    /// Stored Procedure parametrelerini döndürür.
    /// </summary>
    /// <param name="parameters">Parametreler</param>
    /// <returns>Düzenlenmiş parametreler</returns>
    private string GetSPParams(List<object> parameters)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < parameters.Count; i++)
        {
            // DBNull
            if (parameters[i].GetType() == DBNull.Value.GetType())
            {
                sb.AppendFormat("NULL, ");
            }
            else if (parameters[i].GetType().Name == "DateTime")
            {
                sb.AppendFormat("'{0}', ", ((DateTime)parameters[i]).ToString("yyyy-MM-ddTHH:mm:ss"));
            }
            else
            {
                sb.AppendFormat("'{0}', ", parameters[i]);
            }
        }

        return sb.ToString().Length == 0 ? string.Empty : sb.ToString().Substring(0, sb.ToString().Length - 2).Replace("'null'", "''").Replace("'NULL'", "''");
    }

    /// <summary>
    /// C# tarih nesnesini Kata'nın tarih formatına çevirir.
    /// </summary>
    /// <param name="dt">Tarih</param>
    /// <returns>Düzenlenmiş tarih</returns>
    public static string ConvertKataFormat(DateTime dt)
    {
        return dt.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
    }
}

public static class KataExtensions
{
    public static SqlKata.Query FixDBNullValues(this SqlKata.Query query)
    {
        for (int i = 0; i < query.Clauses.Count; i++)
        {
            if (query.Clauses[i].GetType() == typeof(SqlKata.NullCondition))
            {
                query.Clauses[i] = new SqlKata.BasicCondition
                {
                    Column = ((NullCondition)query.Clauses[i]).Column,
                    Operator = "=",
                    Value = DBNull.Value,
                    Component = "where"
                };
            }
        }

        return query;
    }
}