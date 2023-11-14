using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Destinet.Backup
{
  public interface IBackup
  {
    BackupConfig BackupConfig { get; set; }
  }
  class AccessToken
  {
    public string access_token { get; set; }
    public long expires_in { get; set; }

  }
  public class BackupConfig
  {
    public double FullIntervalHour { get; set; } = 24;
    public double IncrementalIntervalHour { get; set; } = 24;
    public string BackupDir { get; set; } = "";
    public string BackupDes { get; set; } = "";

    public string RefreshToken { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string DriveFolderID { get; set; } = "";
  }
  public class BackupLib : IBackup
  {
    System.Timers.Timer _incrementTimer;
    System.Timers.Timer _fullTimer;
    private static readonly HttpClient _httpClient = new HttpClient();

    long _lastBackUpTime { get; set; } = DateTime.UtcNow.Millisecond;
    long _lastTokenTime { get; set; } = -1;
    private string _access_token { get; set; } = "";
    public BackupConfig BackupConfig { get; set; }

    string[] _filesToCheck;
    public BackupLib(BackupConfig config)
    {
      BackupConfig = config;
      BackupConfig.BackupDes = Path.GetFullPath(BackupConfig.BackupDes);
      BackupConfig.BackupDir = Path.GetFullPath(BackupConfig.BackupDir);

      Directory.CreateDirectory(BackupConfig.BackupDes);

      GetAllFilesToCheck();

      _incrementTimer = new System.Timers.Timer(BackupConfig.IncrementalIntervalHour * 60 * 60 * 1000)
      {
        AutoReset = true,
      };
      _incrementTimer.Elapsed += IncrementalBackup;
      _incrementTimer.Start();

      _fullTimer = new System.Timers.Timer(BackupConfig.FullIntervalHour * 60 * 60 * 1000)
      {
        AutoReset = true,
      };
      _fullTimer.Elapsed += FullBackUp;
      _fullTimer.Start();

      //Run back up when start
      Task.Run(() =>
      {
        IncrementalBackup(null, null);
      });
      Task.Run(() =>
      {
        FullBackUp(null, null);
      });

    }



    public void GetAllFilesToCheck()
    {
      _filesToCheck = Directory.GetFiles(BackupConfig.BackupDir, "*.*", SearchOption.TopDirectoryOnly)
          .Where(name => !name.EndsWith(".exe"))
          .ToArray();
    }

    public void IncrementalBackup(Object source, System.Timers.ElapsedEventArgs e)
    {
      var modifiedFiles = _filesToCheck.Where((file) =>
      {
        var lastModified = File.GetLastWriteTimeUtc(file);
        return lastModified.Millisecond > _lastBackUpTime + (60 * 1000);
      }).ToList();

      var backupFolderName = $"{DateTime.UtcNow.Millisecond.GetHashCode()} {DateTime.UtcNow.ToString("yyyy-MM-dd")}";
      var backupPath = Path.Combine(BackupConfig.BackupDes, backupFolderName);

      Directory.CreateDirectory(backupPath);
      CopyFiles(modifiedFiles.ToArray(), backupPath);

      _lastBackUpTime = DateTime.UtcNow.Millisecond;
      var backupZip = $"{backupPath}.zip";
      ZipFolder(backupPath, backupZip, true);

      if (_lastTokenTime < DateTime.UtcNow.Second + (5 * 60))
      {
        Task.Run(async () => {
          await GetAccessToken();
          UploadToGoogleDrive(backupZip, _access_token,BackupConfig.DriveFolderID);
        });
      }
      else
      {
        UploadToGoogleDrive(backupZip, _access_token, BackupConfig.DriveFolderID);
      }
    }

    public void FullBackUp(Object source, System.Timers.ElapsedEventArgs e)
    {
      var backupFolderName = $"{DateTime.UtcNow.Millisecond.GetHashCode()} {DateTime.UtcNow.ToString("yyyy-MM-dd")} Full";
      var backupPath = Path.Combine(BackupConfig.BackupDes, backupFolderName);

      Directory.CreateDirectory(backupPath);
      CopyFiles(_filesToCheck, backupPath);

      var backupZip = $"{backupPath}.zip";
      ZipFolder(backupPath, backupZip, true);

      if(_lastTokenTime < DateTime.UtcNow.Second + (5 * 60))
      {
        Task.Run(async () => {
          await GetAccessToken();
          UploadToGoogleDrive(backupZip, _access_token, BackupConfig.DriveFolderID);
        });
      }
      else
      {
        UploadToGoogleDrive(backupZip, _access_token, BackupConfig.DriveFolderID);
      }
    }

    private void ZipFolder(string baseDir, string desDir, bool deleteAfterZip = false)
    {
      if (File.Exists(desDir))
      {
        File.Delete(desDir);
      }

      System.IO.Compression.ZipFile.CreateFromDirectory(baseDir, desDir);
      if (deleteAfterZip)
      {
        Directory.Delete(baseDir, true);
      }
    }

    private void CopyFiles(string[] baseDirFiles, string desDir)
    {
      for (int i = 0; i < baseDirFiles.Length; i++)
      {
        var fName = Path.GetFileName(baseDirFiles[i]);
        File.Copy(baseDirFiles[i], Path.Combine(desDir, fName), true);
      }
    }

    private async Task GetAccessToken()
    {
      var body = new Dictionary<string, string>()
      {
        {"client_id",BackupConfig.ClientId},
        {"client_secret",BackupConfig.ClientSecret },
        {"grant_type","refresh_token" },
        {"refresh_token",BackupConfig.RefreshToken },
      };
      var content = new FormUrlEncodedContent(body);
      var res = await BackupLib._httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
      var json = await res.Content.ReadAsStringAsync();
      var token = JsonConvert.DeserializeObject<AccessToken>(json);
      _lastBackUpTime = DateTime.UtcNow.Second + token.expires_in;
      _access_token = token.access_token;
    }
    

    private static void UploadToGoogleDrive(string filePath,string token,string folderId)
    {
      var credential = GoogleCredential.FromAccessToken(token)
        .CreateScoped(DriveService.ScopeConstants.DriveFile);
      
      var service = new DriveService(new BaseClientService.Initializer()
      {
        HttpClientInitializer = credential
      });

      var fileMetadata = new Google.Apis.Drive.v3.Data.File()
      {
        Name = Path.GetFileName(filePath),
        Parents = new List<string>() { folderId }
        
      };
      string uploadedFileId;
      using (var fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      {
        // Create a new file, with metadata and stream.
        var request = service.Files.Create(fileMetadata, fsSource, "application/zip");
        request.Fields = "*";
        var results = request.Upload();

        if (results.Status == UploadStatus.Failed)
        {
          Console.WriteLine($"Error uploading file: {results.Exception.Message}");
        }

        // the file id of the new file we created
        uploadedFileId = request.ResponseBody?.Id;
      }
    }
  }
}
