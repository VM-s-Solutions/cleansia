```
Launch Azurite: "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe" --skipApiVersionCheck
```

```
Add Migration: Add-Migration Initial -Context StroytorgDbContext -Project '03 Infrastructure\Stroytorg.Infra.Database' -StartupProject '05 Web\Stroytorg.Host'
Update Database: Update-Database -Context StroytorgDbContext -Project '03 Infrastructure\Stroytorg.Infra.Database' -StartupProject '05 Web\Stroytorg.Host'
```

```
Login to Docker Postgres container: psql -h localhost -p 5432 -U postgres -d Stroytorg
```