```
Launch Azurite: "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe" --skipApiVersionCheck
```

```
Add Migration: Add-Migration Initial  -Context CleansiaDbContext -Project '03 Infrastructure\Cleansia.Infra.Database' -StartupProject '05 Web\Cleansia.Web.Partner'
Update Database: Update-Database -Context CleansiaDbContext -Project '03 Infrastructure\Cleansia.Infra.Database' -StartupProject '05 Web\Cleansia.Web.Partner'
```

```
Login to Docker Postgres container: psql -h localhost -p 5432 -U postgres -d Cleansia


Login to ASPIRE Docker Postgres container: psql -U postgres -d Cleansia => Enter the password from Aspire connection string
```
