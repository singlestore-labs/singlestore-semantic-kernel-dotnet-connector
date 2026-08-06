# SingleStore Vector Store Conformance Tests

This project contains conformance tests for the SingleStore Vector Store implementation.

## Running the Tests

By default, the tests will automatically use a testcontainer to spin up a SingleStore instance. Docker must be running
on your machine for this to work.

### Using an External SingleStore Instance

If you want to run the tests against an external SingleStore instance (e.g., a cloud database, local SingleStore
deployment, or any other instance), you can provide a connection string through the `SingleStore__ConnectionString`
environment variable:

```bash
# Bash/Linux/macOS
export SingleStore__ConnectionString="Host=127.0.0.1;Port=3306;UserId=myuser;Password=mypassword;"

# PowerShell
$env:SingleStore__ConnectionString = "Host=127.0.0.1;Port=3306;UserId=myuser;Password=mypassword;"
```

## Benefits of Using an External Instance

Using an external SingleStore instance can be beneficial when:

- You want to avoid the overhead of spinning up Docker containers
- You need to test against a specific SingleStore version or configuration
- You want faster test execution (no container startup time)
- You're running tests in an environment where Docker is not available
- You need to test against cloud-hosted SingleStore

## Prerequisites for External Instances

The external SingleStore instance must have sufficient permissions for the connecting user to:

- Create database, tables and indexes
- Insert, update, and delete data
