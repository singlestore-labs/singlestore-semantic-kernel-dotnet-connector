using SingleStoreConnector;

namespace SingleStore.SemanticKernel.IntegrationTests;

public class DatabaseFixture : IDisposable
{
    public string Host { get; } = Environment.GetEnvironmentVariable("SINGLESTORE_HOST") ?? "127.0.0.1";

    public string Port { get; } = Environment.GetEnvironmentVariable("SINGLESTORE_PORT") ?? "3306";

    public string User { get; } = Environment.GetEnvironmentVariable("SINGLESTORE_USER") ?? "root";

    public string Password { get; } = Environment.GetEnvironmentVariable("SINGLESTORE_PASSWORD") ?? "1";

    public string Database { get; } = Environment.GetEnvironmentVariable("SINGLESTORE_DATABASE") ?? "testdb";

    public SingleStoreConnection Conn { get; }

    public DatabaseFixture()
    {
        string connStr = $"Server={Host};port={Port};User ID={User};Password={Password}";
        Conn = new SingleStoreConnection(connStr);
        Conn.Open();
        
        ExecuteNonQuery($"DROP DATABASE IF EXISTS {Database}");
        ExecuteNonQuery($"CREATE DATABASE {Database}");
    
        Conn.Close();

        connStr = $"Server={Host};port={Port};User ID={User};Password={Password};Database={Database}";
        Conn = new SingleStoreConnection(connStr);
        Conn.Open();
    }

    public void ExecuteNonQuery(string query)
    {
        using SingleStoreCommand command = new SingleStoreCommand(query, Conn);
        command.ExecuteNonQuery();
    }

    public SingleStoreDataReader ExecuteReader(string query)
    {
        using SingleStoreCommand command = new SingleStoreCommand(query, Conn);
        return command.ExecuteReader();
    }

    public void Dispose()
    {
        Conn.Close();
    }
}