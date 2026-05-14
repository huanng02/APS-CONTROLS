#if DEBUG
using System;
using System.IO;
using System.Threading.Tasks;
using QuanLyGiuXe.Services.Backup;

namespace QuanLyGiuXe.DebugTools.Simulations
{
    public class BackupRestoreTestService
    {
        public async Task TestRealBackup() => await RunBackupTestAsync();
        public async Task TestFakeBackup() => await CreateFakeBackupFileAsync();
        public async Task StressTestBackup(int iterations = 5) => await StressTestBackupAsync(iterations);

        public async Task<string> RunBackupTestAsync()
        {
            return await DatabaseBackupService.Instance.BackupNowAsync("QA_TEST");
        }

        public async Task<bool> RunVerifyTestAsync(string path)
        {
            return await DatabaseBackupService.Instance.VerifyBackupAsync(path);
        }

        public async Task CreateFakeBackupFileAsync()
        {
            string dir = DatabaseBackupService.Instance.GetCurrentBackupDirectory();
            string fakePath = Path.Combine(dir, "corrupted_test_backup.bak");
            await File.WriteAllTextAsync(fakePath, "THIS IS NOT A VALID SQL BACKUP FILE CONTENT");
        }

        public async Task StressTestBackupAsync(int iterations = 5)
        {
            for (int i = 0; i < iterations; i++)
            {
                await DatabaseBackupService.Instance.BackupNowAsync($"QA_STRESS_{i}");
                await Task.Delay(1000);
            }
        }
    }
}
#endif
