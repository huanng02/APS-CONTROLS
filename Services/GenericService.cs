using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    // NOTE: This is a minimal reflection-based generic service.
    // It assumes table name = class name (LoaiXe -> LoaiXe) and column names = property names.
    // Only primitive properties are supported (int, string, decimal, DateTime, etc.)
    public class GenericService<T> : IGenericService<T> where T : class, new()
    {
        private readonly DatabaseService _db = new DatabaseService();
        private readonly string _tableName;

        public GenericService()
        {
            _tableName = typeof(T).Name;
        }

        public List<T> GetAll()
        {
            var list = new List<T>();
            string sql = $"SELECT * FROM dbo.[{_tableName}]";

            using (SqlConnection conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    while (r.Read())
                    {
                        var obj = new T();
                        foreach (var p in props)
                        {
                            try
                            {
                                if (!ColumnExists(r, p.Name)) continue;
                                var val = r[p.Name];
                                if (val == DBNull.Value) continue;
                                p.SetValue(obj, Convert.ChangeType(val, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType));
                            }
                            catch { }
                        }
                        list.Add(obj);
                    }
                }
            }

            return list;
        }

        private bool ColumnExists(SqlDataReader r, string name)
        {
            try { return r.GetOrdinal(name) >= 0; }
            catch { return false; }
        }

        public int Insert(T entity)
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)).ToArray();

            var cols = string.Join(", ", props.Select(p => p.Name));
            var pars = string.Join(", ", props.Select(p => "@" + p.Name));
            string sql = $"INSERT INTO dbo.[{_tableName}] ({cols}) VALUES ({pars}); SELECT SCOPE_IDENTITY();";

            using (SqlConnection conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    foreach (var p in props)
                    {
                        var val = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue("@" + p.Name, val);
                    }

                    var id = cmd.ExecuteScalar();
                    return Convert.ToInt32(id);
                }
            }
        }

        public void Update(T entity)
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)).ToArray();

            var set = string.Join(", ", props.Select(p => p.Name + "=@" + p.Name));
            string sql = $"UPDATE dbo.[{_tableName}] SET {set} WHERE Id=@Id";

            using (SqlConnection conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    foreach (var p in props)
                    {
                        var val = p.GetValue(entity) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue("@" + p.Name, val);
                    }
                    var idProp = typeof(T).GetProperty("Id");
                    var id = idProp.GetValue(entity);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int id)
        {
            string sql = $"DELETE FROM dbo.[{_tableName}] WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(_db.GetConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
