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
