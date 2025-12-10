using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ModelsTask = Waterflow.Models.Task;

namespace Waterflow.Data
{
    /// <summary>
    /// 信息池 - SQLite 数据库访问层
    /// </summary>
    public class InfoPool
    {
        private static readonly Lazy<InfoPool> _instance = new Lazy<InfoPool>(() => new InfoPool());
        public static InfoPool Instance => _instance.Value;
        
        private readonly string _dbPath;
        private readonly string _connectionString;
        
        private InfoPool()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Waterflow"
            );
            
            Directory.CreateDirectory(appDataPath);
            _dbPath = Path.Combine(appDataPath, "waterflow.db");
            _connectionString = $"Data Source={_dbPath}";
            
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS Tasks (
                    Id TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    Deadline TEXT,
                    Category TEXT,
                    Status INTEGER NOT NULL
                )";
            
            using var command = new SqliteCommand(createTableSql, connection);
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// 异步插入任务
        /// </summary>
        public async Task InsertTaskAsync(ModelsTask task)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                var insertSql = @"
                    INSERT INTO Tasks (Id, Title, CreatedAt, Deadline, Category, Status)
                    VALUES (@Id, @Title, @CreatedAt, @Deadline, @Category, @Status)";
                
                using var command = new SqliteCommand(insertSql, connection);
                command.Parameters.AddWithValue("@Id", task.Id);
                command.Parameters.AddWithValue("@Title", task.Title);
                command.Parameters.AddWithValue("@CreatedAt", task.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@Deadline", task.Deadline?.ToString("O") ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Category", (object?)task.Category ?? DBNull.Value);
                command.Parameters.AddWithValue("@Status", (int)task.Status);
                
                command.ExecuteNonQuery();
            });
        }
    }
}

