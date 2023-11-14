# Destinet.Backup

Wrap it to config
```cs

using Destinet.Backup;
using Microsoft.Extensions.Configuration;
namespace <name_space>
{
  public class BackupService : IBackup
  {
    private BackupLib _backuplib;
    public BackupConfig BackupConfig { get; set; }

    public BackupService()
    {
      BackupConfig = new BackupConfig();
      Program.Configuration.GetSection("BackUp").Bind(BackupConfig);
      _backuplib = new BackupLib(BackupConfig);
    }
  }
}
```
```json
{
  "BackUp": {
    "FullIntervalHour": 0.08, //5mins for testing
    "IncrementalIntervalHour": 0.08, //5mins for testing
    "BackupDir": "./", //folder will be backup
    "BackupDes": "./Backups", //folder will write backup to,
    "RefreshToken": <refresh_token>,
    "ClientId": <google oauth2 client id>,
    "ClientSecret": <google oauth2 client secrete>,
    "DriveFolderID": <google drive folder>
  }
}
```
